# Getting Started

This guide installs ArtifactClean and runs a first cleanup. It takes a couple of minutes and needs only the .NET SDK.

## Prerequisites

- The .NET 10 SDK (`dotnet tool install`, `dotnet clean` and `dotnet restore` are SDK commands)
- The `dotnet` command on `PATH` (ArtifactClean calls `dotnet clean` and `dotnet restore`)

The workspaces you clean can target any .NET version.

## Install

```shell
dotnet tool install --global Invex.Tools.ArtifactClean
```

Check that the command is available:

```shell
artclean --help
```

To pick up a newer version later, run `dotnet tool update --global Invex.Tools.ArtifactClean`.

## Run your first cleanup

> [!WARNING]
> ArtifactClean permanently deletes every `bin` and `obj` directory below the selected path. Run it from, or point it
> at, the root of the workspace you mean to clean.

Change to the root of a .NET workspace (the folder with your `.sln`, `.slnx` or project files) and run:

```shell
artclean
```

The tool prints one line per step:

```text
'dotnet clean' completed successfully.
Deleted 12 'bin' / 'obj' directories.
'dotnet restore' completed successfully.
```

To clean another directory without changing to it, pass `--path`:

```shell
artclean --path "path/to/workspace"
```

## Next steps

- [ArtifactClean](artifactclean.md): every option, the exact behavior and the exit codes
- [Introduction](introduction.md): when the tool is the right choice and when it is not
