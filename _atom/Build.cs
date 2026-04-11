namespace Atom;

[BuildDefinition]
[GenerateEntryPoint]
[GenerateSolutionModel]
internal partial class Build : BuildDefinition, IGithubWorkflows, IGitVersion, ITargets
{
    public static readonly string[] PlatformNames =
    [
        IJobRunsOn.WindowsLatestTag,
        "windows-11-arm",
        IJobRunsOn.UbuntuLatestTag,
        "ubuntu-24.04-arm",
        "macos-15-intel",
        IJobRunsOn.MacOsLatestTag,
    ];

    public static readonly string[] FrameworkNames = ["net8.0", "net9.0", "net10.0"];

    public override IReadOnlyList<IWorkflowOption> GlobalWorkflowOptions =>
    [
        UseGitVersionForBuildId.Enabled, new SetupDotnetStep("10.0.x"),
    ];

    public override IReadOnlyList<WorkflowDefinition> Workflows =>
    [
        new("Validate")
        {
            Triggers = [ManualTrigger.Empty, GitPullRequestTrigger.IntoMain],
            Targets =
            [
                WorkflowTargets.SetupBuildInfo.WithSuppressedArtifactPublishing,
                WorkflowTargets.Pack.WithSuppressedArtifactPublishing.WithGithubRunnerMatrix(PlatformNames),
            ],
            WorkflowTypes = [Github.WorkflowType],
            Options = [GithubTokenPermissionsOption.NoneAll],
        },
        new("Build")
        {
            Triggers =
            [
                ManualTrigger.Empty,
                new GitPushTrigger
                {
                    IncludedBranches = ["main", "feature/**", "patch/**"],
                },
                GithubReleaseTrigger.OnReleased,
            ],
            Targets =
            [
                WorkflowTargets.SetupBuildInfo,
                WorkflowTargets.Pack.WithGithubRunnerMatrix(PlatformNames),
                WorkflowTargets.PushToNuget.WithOptions(WorkflowSecretInjection.Create(Params.NugetApiKey)),
                WorkflowTargets
                    .PushToRelease
                    .WithGithubTokenInjection(new()
                    {
                        Contents = GithubTokenPermission.Write,
                    })
                    .WithOptions(GithubIf.Create(new ConsumedVariableExpression(nameof(ISetupBuildInfo.SetupBuildInfo),
                            ParamDefinitions[nameof(ISetupBuildInfo.BuildVersion)].ArgName)
                        .Contains(new StringExpression("-"))
                        .EqualTo("false"))),
            ],
            WorkflowTypes = [Github.WorkflowType],
            Options = [GithubTokenPermissionsOption.NoneAll],
        },
        Github.DependabotDefaultWorkflow(),
    ];
}
