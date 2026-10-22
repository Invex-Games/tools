using Invex.RepoUtils.Atom.Module.Extensions;
using Invex.RepoUtils.Atom.Module.Helpers;
using Invex.RepoUtils.Atom.Module.Targets;
using Invex.StructuredText.GithubActions.DependabotConfigModel.Model;

namespace Atom;

[BuildDefinition]
[GenerateEntryPoint]
[GenerateSolutionModel]
internal interface IBuild : IWorkflowBuildDefinition,
    IGitHubWorkflows,
    INugetPackageUnlistHelper,
    IApproveDependabotPr,
    IGitVersion,
    IDotnetPackHelper,
    IDotnetTestHelper,
    INugetHelper,
    IGitHubReleaseHelper,
    IDocFxHelper,
    IWaitForCopilotReview
{
    static readonly string[] PlatformNames =
    [
        WorkflowLabels.GitHub.RunsOn.Windows_Latest,
        WorkflowLabels.GitHub.RunsOn.Windows_11_Arm,
        WorkflowLabels.GitHub.RunsOn.Ubuntu_Latest,
        WorkflowLabels.GitHub.RunsOn.Ubuntu_24_04_Arm,
        WorkflowLabels.GitHub.RunsOn.MacOS_15_Intel,
        WorkflowLabels.GitHub.RunsOn.MacOS_Latest,
    ];

    static readonly string[] ToolsToPack = [Projects.Invex_Tools_ArtifactClean.Name];

    static readonly string[] ProjectsToTest = [Projects.Invex_Tools_ArtifactClean_Tests.Name];

    [ParamDefinition("test-framework", "Test framework to use for unit tests")]
    string TestFramework => GetParam(() => TestFramework, "net10.0");

    [ParamDefinition("nuget-push-feed", "The Nuget feed to push to.")]
    string NugetFeed => GetParam(() => NugetFeed, "https://api.nuget.org/v3/index.json");

    [SecretDefinition("nuget-push-api-key", "The API key to use to push to Nuget.")]
    string NugetApiKey => GetParam(() => NugetApiKey)!;

    [ParamDefinition("prerelease-cleanup-below-version", "Unlist all prerelease packages below this stable version.")]
    string PrereleaseCleanupBelowVersion => GetParam(() => PrereleaseCleanupBelowVersion)!;

    Target Pack =>
        t => t
            .DescribedAs("Packs the tools into nuget packages")
            .ProducesArtifacts(ToolsToPack)
            .Executes(async cancellationToken =>
            {
                var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;

                Logger.LogInformation("Packing AOT tools for runtime {RuntimeIdentifier}", runtimeIdentifier);

                foreach (var tool in ToolsToPack)
                    await DotnetPackAndStage(tool,
                        new()
                        {
                            PackOptions = new()
                            {
                                Runtime = runtimeIdentifier,
                                Property = new Dictionary<string, string>
                                {
                                    { "PublishAot", "true" },
                                },
                            },
                        },
                        cancellationToken);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Logger.LogInformation("Packing non-AOT tools");

                    foreach (var tool in ToolsToPack)
                        await DotnetPackAndStage(tool,
                            new()
                            {
                                ClearPublishDirectory = false,
                            },
                            cancellationToken);
                }
            });

    Target TestProjects =>
        t => t
            .DescribedAs("Tests the projects")
            .RequiresParam(nameof(TestFramework))
            .ProducesArtifacts(ProjectsToTest)
            .Executes(async cancellationToken =>
            {
                var exitCode = 0;

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var project in ProjectsToTest)
                    exitCode += await DotnetTestAndStage(project,
                        new()
                        {
                            TestOptions = new()
                            {
                                Framework = TestFramework,
                            },
                        },
                        cancellationToken);

                if (exitCode != 0)
                    throw new StepFailedException("One or more unit tests failed");
            });

    Target PushToNuget =>
        d => d
            .DescribedAs("Pushes packages to Nuget")
            .RequiresParam(nameof(NugetFeed), nameof(NugetApiKey))
            .ConsumesArtifacts(nameof(Pack), ToolsToPack, PlatformNames)
            .DependsOn(nameof(TestProjects))
            .Executes(async cancellationToken =>
            {
                // Push Atom tool package - platform-specific + multi-targeted

                foreach (var tool in ToolsToPack)
                foreach (var toolPackagePath in RootedFileSystem.Directory.GetFiles(
                             RootedFileSystem.AtomArtifactsDirectory / tool,
                             "*.nupkg",
                             SearchOption.AllDirectories))
                    await PushPackageToNuget(RootedFileSystem.AtomArtifactsDirectory / tool / toolPackagePath,
                        NugetFeed,
                        NugetApiKey,
                        cancellationToken: cancellationToken);
            });

    Target PushToRelease =>
        d => d
            .DescribedAs("Pushes artifacts to a GitHub release")
            .RequiresParam(nameof(GitHubToken))
            .ConsumesVariable(nameof(SetupBuildInfo), nameof(BuildVersion))
            .ConsumesArtifacts(nameof(Pack), ToolsToPack, PlatformNames)
            .DependsOn(nameof(TestProjects))
            .Executes(async () =>
            {
                foreach (var tool in ToolsToPack)
                    await UploadArtifactToRelease(tool, $"v{BuildVersion}");
            });

    Target UnlistSupersededPrereleases =>
        t => t
            .DescribedAs("Unlists prerelease packages superseded by the just-published version.")
            .RequiresParam(nameof(NugetFeed), nameof(NugetApiKey))
            .ConsumesVariable(nameof(SetupBuildInfo), nameof(BuildVersion))
            .DependsOn(nameof(PushToNuget))
            .Executes(async cancellationToken =>
            {
                var packages = ToolsToPack.ToArray();

                if (packages.Length is 0)
                {
                    Logger.LogInformation("No packages configured for prerelease unlisting. Skipping.");

                    return;
                }

                await UnlistSupersededPrereleasesForPackages(NugetFeed,
                    NugetApiKey,
                    packages,
                    BuildVersion,
                    cancellationToken);
            });

    Target UnlistOldPrereleases =>
        t => t
            .DescribedAs("Unlists all prerelease packages below the configured stable version.")
            .RequiresParam(nameof(NugetFeed), nameof(NugetApiKey), nameof(PrereleaseCleanupBelowVersion))
            .Executes(async cancellationToken =>
            {
                var packages = ToolsToPack.ToArray();

                if (packages.Length is 0)
                {
                    Logger.LogInformation("No packages configured for prerelease cleanup. Skipping.");

                    return;
                }

                if (!SemVer.TryParse(PrereleaseCleanupBelowVersion, out var belowVersion))
                    throw new StepFailedException(
                        $"'{PrereleaseCleanupBelowVersion}' is not a valid version for {nameof(PrereleaseCleanupBelowVersion)}.");

                await UnlistPrereleasesBelowVersionForPackages(NugetFeed,
                    NugetApiKey,
                    packages,
                    belowVersion,
                    cancellationToken);
            });

    Target BuildDocs =>
        t => t
            .DescribedAs("Builds the DocFX documentation.")
            .ProducesArtifact(GeneratedDocsArtifactName)
            .Executes(cancellationToken => BuildDocFxDocs(cancellationToken: cancellationToken));

    Target ServeDocs =>
        t => t
            .DescribedAs("Serves the DocFX documentation.")
            .DependsOn(nameof(BuildDocs))
            .Executes(ServeDocFxDocs);

    Target PublishDocs =>
        t => t
            .DescribedAs("Publishes the DocFX documentation to Github Pages.")
            .RequiresParam(nameof(GitHubToken))
            .ConsumesArtifact(nameof(BuildDocs), GeneratedDocsArtifactName)
            .DependsOn(nameof(SetupBuildInfo))
            .Executes(cancellationToken =>
                PublishDocFxDocsToGithub(GitHubToken, GeneratedDocsArtifactName, cancellationToken));

    IReadOnlyList<IBuildOption> IBuildDefinition.Options =>
    [
        BuildOptions.GitVersion.ProvideBuildId,
        BuildOptions.GitVersion.ProvideBuildVersion,
        BuildOptions.Steps.SetupDotnet.Dotnet100X(),
    ];

    IReadOnlyList<WorkflowDefinition> IWorkflowBuildDefinition.Workflows =>
    [
        new("Validate")
        {
            Triggers = [WorkflowTriggers.Manual, WorkflowTriggers.PullIntoMain],
            Targets =
            [
                new(nameof(SetupBuildInfo)),
                new(nameof(Pack))
                {
                    Options = [BuildOptions.Target.SuppressArtifactPublishing, BuildOptions.GitHub.RunsOn.SetByMatrix],
                    MatrixDimensions =
                    [
                        new(nameof(JobRunsOn))
                        {
                            Values = [..PlatformNames],
                        },
                    ],
                },
                new(nameof(TestProjects))
                {
                    Options = [BuildOptions.Target.SuppressArtifactPublishing],
                },
                new(nameof(WaitForCopilotReview))
                {
                    Options =
                    [
                        BuildOptions.Inject.Secret(nameof(GitHubToken)),
                        BuildOptions.Inject.Github.PullRequestNumber,
                    ],
                },
            ],
            Types = [WorkflowTypes.GitHub.Action],
        },
        new("Build")
        {
            Triggers =
            [
                WorkflowTriggers.Manual,
                new GitPushTrigger
                {
                    IncludedBranches = ["main", "feature/**", "patch/**"],
                },
                new GitHubTrigger(new On.Release([On.Release.ReleaseType.released])),
            ],
            Targets =
            [
                new(nameof(SetupBuildInfo)),
                new(nameof(Pack))
                {
                    Options = [BuildOptions.GitHub.RunsOn.SetByMatrix],
                    MatrixDimensions =
                    [
                        new(nameof(JobRunsOn))
                        {
                            Values = [..PlatformNames],
                        },
                    ],
                },
                new(nameof(TestProjects)),
                new(nameof(PushToNuget))
                {
                    Options = [BuildOptions.Inject.Secret(nameof(NugetApiKey))],
                },
                new(nameof(PushToRelease))
                {
                    Options =
                    [
                        BuildOptions.Inject.Secret(nameof(GitHubToken)),
                        new GitHubTokenPermissionsOption(new Permissions.Exact(new()
                        {
                            Contents = PermissionsLevel.Write,
                        })),
                        BuildOptions.Target.RunIfWorkflowCondition(
                            !Tex.ParamOutput(this, nameof(SetupBuildInfo), nameof(BuildVersion)).Contains("-")),
                    ],
                },
                new(nameof(PublishDocs))
                {
                    Options =
                    [
                        BuildOptions.Inject.Secret(nameof(GitHubToken)),
                        new GitHubTokenPermissionsOption(new Permissions.Exact(new()
                        {
                            Contents = PermissionsLevel.Write,
                        })),
                        BuildOptions.Target.RunIfWorkflowCondition(
                            !Tex.ParamOutput(this, nameof(SetupBuildInfo), nameof(BuildVersion)).Contains("-")),
                    ],
                },
            ],
            Types = [WorkflowTypes.GitHub.Action],
        },
        new("Dependabot Enable auto-merge")
        {
            Triggers = [WorkflowTriggers.PullIntoMain],
            Targets =
            [
                new(nameof(ApproveDependabotPr))
                {
                    Options =
                    [
                        BuildOptions.Inject.Github.PullRequestNumber,
                        BuildOptions.Inject.Github.DependabotEnableAutoMergePat,
                        BuildOptions.Target.RunIfWorkflowCondition(
                            Tex.GitHub.Actor.Eq("dependabot[bot]")),
                    ],
                },
            ],
            Types = [WorkflowTypes.GitHub.Action],
        },
        new("Cleanup Prereleases")
        {
            Triggers =
            [
                WorkflowTriggers.ManualWithInputs(
                    ManualStringInput.ForParam(ParamDefinitions[nameof(PrereleaseCleanupBelowVersion)])),
            ],
            Targets =
            [
                new(nameof(UnlistOldPrereleases))
                {
                    Options =
                    [
                        BuildOptions.Target.SuppressArtifactPublishing,
                        BuildOptions.Inject.Secret(nameof(NugetApiKey)),
                    ],
                },
            ],
            Types = [WorkflowTypes.GitHub.Action],
        },
        WorkflowPresets.GitHub.Dependabot(new()
        {
            Registries = new Dictionary<string, DependabotRegistry>
            {
                ["nuget"] = new()
                {
                    Type = RegistryType.NugetFeed,
                    Url = WorkflowLabels.GitHub.Dependabot.NugetUrl,
                },
            },
            Updates =
            [
                new()
                {
                    Directory = "/",
                    PackageEcosystem = WorkflowLabels.GitHub.Dependabot.NugetEcosystem,
                    Registries = new DependabotRegistries.Named("nuget"),
                    Groups = new Dictionary<string, DependabotGroup>
                    {
                        ["nuget-deps"] = new DependabotGroup.FromPatterns
                        {
                            Patterns = ["*"],
                        },
                    },
                    Schedule = new()
                    {
                        Interval = ScheduleInterval.Weekly,
                        Day = ScheduleDay.Saturday,
                        Time = "00:00",
                        Timezone = "Australia/Brisbane",
                    },
                    TargetBranch = "main",
                    OpenPullRequestsLimit = 10,
                },
            ],
        }),
    ];
}
