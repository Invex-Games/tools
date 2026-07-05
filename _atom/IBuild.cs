using Invex.RepoUtils.Atom.Module.Extensions;
using Invex.RepoUtils.Atom.Module.Helpers;
using Invex.RepoUtils.Atom.Module.Targets;
using Invex.StructuredText.GithubActions.DependabotConfigModel.Model;

namespace Atom;

[BuildDefinition]
[GenerateEntryPoint]
[GenerateSolutionModel]
internal interface IBuild : IWorkflowBuildDefinition,
    IGithubWorkflows,
    INugetPackageUnlistHelper,
    IApproveDependabotPr,
    IGitVersion,
    IDotnetPackHelper,
    IDotnetTestHelper,
    INugetHelper,
    IGithubReleaseHelper,
    IDocFxHelper,
    IWaitForCopilotReview
{
    [ParamDefinition("test-framework", "Test framework to use for unit tests")]
    string TestFramework => GetParam(() => TestFramework, "net10.0");

    [ParamDefinition("nuget-push-feed", "The Nuget feed to push to.")]
    string NugetFeed => GetParam(() => NugetFeed, "https://api.nuget.org/v3/index.json");

    [SecretDefinition("nuget-push-api-key", "The API key to use to push to Nuget.")]
    string NugetApiKey => GetParam(() => NugetApiKey)!;

    [ParamDefinition("prerelease-cleanup-below-version", "Unlist all prerelease packages below this stable version.")]
    string PrereleaseCleanupBelowVersion => GetParam(() => PrereleaseCleanupBelowVersion)!;

    static readonly string[] PlatformNames =
    [
        WorkflowLabels.Github.RunsOn.Windows_Latest,
        WorkflowLabels.Github.RunsOn.Windows_11_Arm,
        WorkflowLabels.Github.RunsOn.Ubuntu_Latest,
        WorkflowLabels.Github.RunsOn.Ubuntu_24_04_Arm,
        WorkflowLabels.Github.RunsOn.MacOs_15_Intel,
        WorkflowLabels.Github.RunsOn.MacOs_Latest,
    ];

    static readonly string[] ToolsToPack = [Projects.Invex_Tools_ArtifactClean.Name];

    IReadOnlyList<IBuildOption> IBuildDefinition.Options =>
    [
        BuildOptions.GitVersion.ProvideBuildId,
        BuildOptions.GitVersion.ProvideBuildVersion,
        BuildOptions.Steps.SetupDotnet.Dotnet100X(),
    ];

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

    Target PushToNuget =>
        d => d
            .DescribedAs("Pushes packages to Nuget")
            .RequiresParam(nameof(NugetFeed), nameof(NugetApiKey))
            .ConsumesArtifacts(nameof(Pack), ToolsToPack, PlatformNames)
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
            .RequiresParam(nameof(GithubToken))
            .ConsumesVariable(nameof(SetupBuildInfo), nameof(BuildVersion))
            .ConsumesArtifacts(nameof(Pack), ToolsToPack, PlatformNames)
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
            .RequiresParam(nameof(GithubToken))
            .ConsumesArtifact(nameof(BuildDocs), GeneratedDocsArtifactName)
            .DependsOn(nameof(SetupBuildInfo))
            .Executes(cancellationToken =>
                PublishDocFxDocsToGithub(GithubToken, GeneratedDocsArtifactName, cancellationToken));

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
                    Options = [BuildOptions.Target.SuppressArtifactPublishing, BuildOptions.Github.RunsOn.SetByMatrix],
                    MatrixDimensions =
                    [
                        new(nameof(JobRunsOn))
                        {
                            Values = PlatformNames,
                        },
                    ],
                },
                new(nameof(WaitForCopilotReview))
                {
                    Options =
                    [
                        BuildOptions.Inject.Secret(nameof(GithubToken)),
                        BuildOptions.Inject.Github.PullRequestNumber,
                    ],
                },
            ],
            Types = [WorkflowTypes.Github.Action],
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
                new GithubTrigger(new On.Release([On.Release.ReleaseType.released])),
            ],
            Targets =
            [
                new(nameof(SetupBuildInfo)),
                new(nameof(Pack))
                {
                    Options = [BuildOptions.Github.RunsOn.SetByMatrix],
                    MatrixDimensions =
                    [
                        new(nameof(JobRunsOn))
                        {
                            Values = PlatformNames,
                        },
                    ],
                },
                new(nameof(PushToNuget))
                {
                    Options = [BuildOptions.Inject.Secret(nameof(NugetApiKey))],
                },
                new(nameof(PushToRelease))
                {
                    Options =
                    [
                        BuildOptions.Inject.Secret(nameof(GithubToken)),
                        new GithubTokenPermissionsOption(new Permissions.Exact(new()
                        {
                            Contents = PermissionsLevel.Write,
                        })),
                        BuildOptions.Target.RunIfWorkflowCondition(TextExpressions
                            .Target
                            .ParamOutput(this, nameof(SetupBuildInfo), nameof(BuildVersion))
                            .Contains("-")
                            .EqualTo(false)),
                    ],
                },
                new(nameof(PublishDocs))
                {
                    Options =
                    [
                        BuildOptions.Inject.Secret(nameof(GithubToken)),
                        new GithubTokenPermissionsOption(new Permissions.Exact(new()
                        {
                            Contents = PermissionsLevel.Write,
                        })),
                        BuildOptions.Target.RunIfWorkflowCondition(TextExpressions
                            .Target
                            .ParamOutput(this, nameof(SetupBuildInfo), nameof(BuildVersion))
                            .Contains("-")
                            .EqualTo(false)),
                    ],
                },
            ],
            Types = [WorkflowTypes.Github.Action],
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
                            TextExpressions.Github.GithubActor.EqualToString("dependabot[bot]")),
                    ],
                },
            ],
            Types = [WorkflowTypes.Github.Action],
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
            Types = [WorkflowTypes.Github.Action],
        },
        WorkflowPresets.Github.Dependabot(new()
        {
            Registries = new Dictionary<string, DependabotRegistry>
            {
                ["nuget"] = new()
                {
                    Type = RegistryType.NugetFeed,
                    Url = WorkflowLabels.Github.Dependabot.NugetUrl,
                },
            },
            Updates =
            [
                new()
                {
                    Directory = "/",
                    PackageEcosystem = WorkflowLabels.Github.Dependabot.NugetEcosystem,
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
