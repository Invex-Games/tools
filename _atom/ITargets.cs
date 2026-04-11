namespace Atom;

[PublicAPI]
internal interface ITargets : IDotnetPackHelper, IDotnetTestHelper, INugetHelper, IGithubReleaseHelper, ISetupBuildInfo
{
    static readonly string[] ToolsToPack = [Projects.Invex_Tools_ArtifactClean.Name];

    [ParamDefinition("nuget-push-feed", "The Nuget feed to push to.")]
    string NugetFeed => GetParam(() => NugetFeed, "https://api.nuget.org/v3/index.json");

    [SecretDefinition("nuget-push-api-key", "The API key to use to push to Nuget.")]
    string NugetApiKey => GetParam(() => NugetApiKey)!;

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
                foreach (var toolPackagePath in FileSystem.Directory.GetFiles(FileSystem.AtomArtifactsDirectory / tool,
                             "*.nupkg",
                             SearchOption.AllDirectories))
                    await PushPackageToNuget(FileSystem.AtomArtifactsDirectory / tool / toolPackagePath,
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
}
