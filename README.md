# Invex .NET Tools

[![Build](https://github.com/Invex-Games/tools/actions/workflows/Build.yml/badge.svg)](https://github.com/Invex-Games/tools/actions/workflows/Build.yml)
[![NuGet](https://img.shields.io/nuget/v/Invex.Tools.ArtifactClean.svg)](https://www.nuget.org/packages/Invex.Tools.ArtifactClean)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Invex-Games/tools/blob/main/LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

A collection of small, focused .NET developer tools. The repository currently ships **ArtifactClean**, the
`artclean` .NET global tool, which resets a .NET workspace to a clean state in one command: `dotnet clean`,
delete every `bin` and `obj` directory, then `dotnet restore`.

## Features

- **One-step reset.** Runs `dotnet clean`, removes `bin` and `obj` directories recursively, and runs `dotnet restore`.
- **Safe traversal.** Skips symbolic links, junctions and other reparse points, and skips directories it cannot read.
- **Script-friendly.** Exits with `0` on success and `1` on any failure, while still finishing the remaining steps.
- **Quiet by default.** Prints a short summary; `--verbose` shows each deleted directory and the `dotnet` output.
- **Fast startup.** Published packages include Native AOT assets for Windows, Linux and macOS on x64 and ARM64.

## Installation

Install the latest published version as a global tool:

```shell
dotnet tool install --global Invex.Tools.ArtifactClean
```

Update an existing installation:

```shell
dotnet tool update --global Invex.Tools.ArtifactClean
```

The tool targets .NET 10 and needs the `dotnet` command on `PATH`. The workspaces it cleans can target any
.NET version.

## Quick start

From the root of the workspace you want to clean:

```shell
artclean
```

```text
'dotnet clean' completed successfully.
Deleted 12 'bin' / 'obj' directories.
'dotnet restore' completed successfully.
```

> [!WARNING]
> ArtifactClean permanently deletes every `bin` and `obj` directory below the selected path. Select the root path carefully.

## Usage

### Clean a different directory

```shell
artclean --path "path/to/workspace"
```

`dotnet clean` and `dotnet restore` run with the selected directory as their working directory. See
[ArtifactClean](https://github.com/Invex-Games/tools/blob/main/docs/artifactclean.md#usage).

### Skip the restore or see more detail

```shell
artclean --no-restore
artclean --verbose
```

The complete command syntax is:

```text
artclean [options]
  -p, --path <directory>  Root directory to clean (default: current directory)
  -n, --no-restore        Do not run dotnet restore after cleaning
  -v, --verbose           Show detailed cleanup and dotnet command output
```

See [ArtifactClean options](https://github.com/Invex-Games/tools/blob/main/docs/artifactclean.md#options).

### Use it in scripts

The tool exits with code `1` when the path does not exist, `dotnet clean` or `dotnet restore` fails, or a
`bin` or `obj` directory cannot be deleted. A failed `dotnet clean` does not stop the cleanup. See
[Exit codes](https://github.com/Invex-Games/tools/blob/main/docs/artifactclean.md#exit-codes).

## Documentation

Full documentation: [https://invex-games.github.io/tools/](https://invex-games.github.io/tools/)

| Guide | Contents |
|-------|----------|
| [Introduction](https://github.com/Invex-Games/tools/blob/main/docs/introduction.md) | What the tools are for, when to use ArtifactClean and when not to |
| [Getting Started](https://github.com/Invex-Games/tools/blob/main/docs/getting-started.md) | Install `artclean` and run your first cleanup |
| [ArtifactClean](https://github.com/Invex-Games/tools/blob/main/docs/artifactclean.md) | Command reference: options, behavior, exit codes, platforms |

## Development

The repository requires the .NET 10 SDK (see `global.json`).

```shell
dotnet build Invex.Tools.slnx
dotnet test Invex.Tools.slnx
```

> [!WARNING]
> Do not run `artclean` from the repository root while developing: it deletes the build output you are testing.

Preview the documentation site with the Atom build definition:

```shell
atom BuildDocs
atom ServeDocs
# equivalently
dotnet run --project _atom/_atom.csproj -- ServeDocs
```

### Repository layout

| Path | Purpose |
| --- | --- |
| `src/Invex.Tools.ArtifactClean` | Source for the `artclean` tool |
| `tests/Invex.Tools.ArtifactClean.Tests` | NUnit tests for the cleanup logic |
| `_atom` | Atom build definition used to generate CI and release workflows |
| `docs` | DocFX source for the project documentation |
| `Invex.Tools.slnx` | Repository solution |

## License

This project is available under the [MIT License](https://github.com/Invex-Games/tools/blob/main/LICENSE.txt).
