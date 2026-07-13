---
title: ArtifactClean
description: Clean .NET build artifacts with the artclean command.
---

# ArtifactClean (`artclean`)

`artclean` recursively removes `bin` and `obj` directories from a .NET workspace. By default, it restores the workspace afterward so it is ready for the next build.

## Installation

```bash
dotnet tool install --global Invex.Tools.ArtifactClean
```

## Usage

Run from the root of the workspace to clean:

```bash
artclean
```

Provide a root path to clean a different directory:

```bash
artclean path\to\workspace
```

## Options

| Option | Description |
| --- | --- |
| `-n`, `--no-restore` | Do not run `dotnet restore` after cleaning. |
| `-v`, `--verbose` | Show each deleted directory and detailed `dotnet` output. |

> [!WARNING]
> ArtifactClean permanently deletes all `bin` and `obj` directories below the selected path. It skips symbolic links and junctions while traversing the directory tree.
