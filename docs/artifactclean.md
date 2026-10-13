---
title: ArtifactClean
description: Clean .NET build artifacts with the artclean command.
---

# ArtifactClean (`artclean`)

`artclean` runs `dotnet clean`, recursively removes `bin` and `obj` directories
from a .NET workspace, and runs `dotnet restore` afterward by default. It skips
symbolic links, junctions, and other reparse points while traversing.

## Installation

```bash
dotnet tool install --global Invex.Tools.ArtifactClean
```

ArtifactClean requires .NET 10. Published packages include Native AOT assets
for Windows, Linux, and macOS on x64 and ARM64. The workspaces it cleans can
target any .NET version.

## Usage

Run from the root of the workspace to clean:

```bash
artclean
```

Provide a root path to clean a different directory:

```bash
artclean --path "path/to/workspace"
```

## Options

| Option | Description |
| --- | --- |
| `-n`, `--no-restore` | Do not run `dotnet restore` after cleaning. |
| `-v`, `--verbose` | Show each deleted directory and detailed `dotnet` output. |

## Exit codes

The tool exits with code `0` on success and `1` when the path does not exist,
`dotnet clean` or `dotnet restore` fails, or a `bin` or `obj` directory cannot
be deleted. A failed `dotnet clean` does not stop the cleanup; the remaining
steps still run and the tool reports the failure through its exit code.

> [!WARNING]
> ArtifactClean permanently deletes all `bin` and `obj` directories below the selected path. It skips symbolic links and junctions while traversing the directory tree.
