# Agent Instructions

Guidance for AI agents working in this repository. Keep implementation changes
focused; `README.md` documents consumer-facing usage and `docs/` contains the
DocFX source.

## Repository overview

This is a .NET 10 repository containing the `Invex.Tools.ArtifactClean`
global tool. Its `artclean` command runs `dotnet clean`, recursively removes
`bin` and `obj` directories below a selected root, and runs `dotnet restore`
unless `--no-restore` is supplied. `_atom` contains the Atom build definition
used for CI, packaging, releases, and documentation publishing.

The solution is `Invex.Tools.slnx` and contains the ArtifactClean project, its
NUnit test project under `tests/Invex.Tools.ArtifactClean.Tests`, and the
`_atom` build project.

## Build, test, and cleanup

Use the SDK selected by `global.json`:

```powershell
dotnet --version
dotnet restore Invex.Tools.slnx
dotnet build Invex.Tools.slnx --configuration Release --no-restore
```

Run the tests with `dotnet test Invex.Tools.slnx` and keep the build
warning-free. CI runs them on `net10.0` through the Atom `TestProjects`
target:

```shell
dotnet run --project _atom/_atom.csproj -- TestProjects --skip --headless
```

The tests exercise the bin/obj traversal against temporary directories. Only
`Clean_WhenDotnetCleanFails_StillDeletesArtifactsAndReturnsFailure` calls
`Clean` on an existing path: it runs a real `dotnet clean` in an empty
temporary directory (so it fails) with restore disabled, which needs `dotnet`
on `PATH`. Link tests are ignored where the platform cannot create symbolic
links.

Do not run `artclean` from the repository root during validation: it
intentionally deletes `bin` and `obj` directories. Prefer the project and
solution build commands above.

After C# changes, run ReSharper cleanup over the solution:

```powershell
$sdk = dotnet --version
jb cleanupcode Invex.Tools.slnx --include="**.cs" --toolset-path="C:\Program Files\dotnet\sdk\$sdk\MSBuild.dll"
```

If `jb` is not installed, install it with:

```powershell
dotnet tool install -g JetBrains.ReSharper.GlobalTools
```

The `--toolset-path` argument is required. It makes `jb` use the MSBuild
selected by `global.json` instead of Visual Studio Build Tools, avoiding
`MSB4236` errors involving `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator`.
Cleanup honors `.editorconfig` and any repository DotSettings automatically.

## Build and language conventions

- `Nullable`, `ImplicitUsings`, C# 14, and `TreatWarningsAsErrors` are enabled
  in `Directory.Build.props`; builds must be warning-free.
- Use file-scoped namespaces and put project-wide shared usings in
  `src/Invex.Tools.ArtifactClean/_usings.cs`.
- Follow `.editorconfig`: four-space C# indentation, CRLF for normal source
  files, a final newline for C# files, and a 120-character line limit.
- Follow the existing analyzer-driven style, including `var` where the type is
  apparent, target-typed `new`, collection expressions, and trailing commas in
  multiline lists.
- Public types and members must have accurate XML documentation; `CS1591` is
  enforced in Release builds (where the documentation file is generated). Only
  the non-shipped `_atom` build project suppresses it, in its own `.csproj`. Add
  `[PublicAPI]` to new public API where the existing public-API analyzer
  conventions require it.
- Keep internal helpers `internal`; expose only the intended consumer surface.
- Preserve Native AOT compatibility. Avoid reflection and platform-specific
  APIs unless all configured runtime identifiers support them.

## ArtifactClean implementation guidance

- Preserve cross-platform traversal and the explicit reparse-point exclusion.
- Keep inaccessible-directory handling and deletion-failure reporting clear;
  do not turn failures into silent success.
- Preserve the `--path`, `--no-restore`, and `--verbose` command-line behavior.
- Update both `README.md` and `docs/artifactclean.md` when public CLI behavior,
  installation, supported platforms, or cleanup semantics change.

## Atom and generated workflows

The YAML files under `.github/workflows/` and the Dependabot configuration are
generated from `_atom/IBuild.cs`. Do not hand-edit generated workflow output.
When changing targets, workflow definitions, triggers, options, or parameters,
regenerate the files with:

```shell
atom gen
```

Commit the generated YAML together with the `_atom` change. The CI packaging
target can be run locally with:

```powershell
dotnet run --project _atom\_atom.csproj -- Pack --skip --headless
```

Generated build output under `.github\artifacts`, `.github\publish`, and
`_site` is not source and should not be edited manually.

## Versioning and commits

Use Conventional Commits. `GitVersion.yml` derives version changes from these
prefixes:

| Prefix | Version effect |
| --- | --- |
| `breaking:` or `major:` | Major |
| `feat:`, `feature:`, or `minor:` | Minor |
| `fix:` or `patch:` | Patch |
| `semver-none` or `semver-skip` | No version bump |

## Documentation

Docs follow the shared Invex documentation standard used by every Invex library:

- **README.md** is also the NuGet readme. Keep the fixed section order (title, badges, pitch, Features,
  Installation, Quick start, Usage, Documentation, Development, License) and use absolute links only
  (`https://github.com/Invex-Games/tools/blob/main/...` or the docs site), so they work on nuget.org.
- **DocFX site** (`docfx.json`, `toc.yml`, `index.md`, `docs/`, `api/index.md`), published to
  https://invex-games.github.io/tools/. `docs/introduction.md` and `docs/getting-started.md` come first; every page
  opens with one to three sentences on what it covers and ends with a "See also" or "Next steps" list. Link API
  types with `<xref:Namespace.Type>`, not file paths.
- **`api/index.md`** is the only hand-written file under `api/`; everything else there is generated. Put per-type
  detail in XML docs, not in hand-written API pages.
- **XML docs are enforced.** `CS1591` is not suppressed for shipped projects, so every public type and member needs
  `<summary>`, plus `<param>`, `<typeparam>`, `<returns>` and `<exception>` where they apply, `<remarks>` for
  non-obvious behaviour, and `<see cref>`/`<paramref>`/`<see langword>` for references.

## Change checklist

1. Follow existing patterns and make precise, surgical changes.
2. Keep public documentation and CLI behavior accurate.
3. Build the solution and run the tests.
4. Run ReSharper cleanup for C# changes.
5. Regenerate Atom workflow output when `_atom` changes affect it.
6. Do not commit generated build artifacts or secrets.
