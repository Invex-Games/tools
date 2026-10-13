# ArtifactClean

This is the reference for the `artclean` command. `artclean` runs `dotnet clean`, recursively removes `bin` and `obj` directories
from a .NET workspace, and runs `dotnet restore` afterward by default. It skips
symbolic links, junctions, and other reparse points while traversing. Use this page to look up options, behavior and exit codes.

## Installation

```shell
dotnet tool install --global Invex.Tools.ArtifactClean
```

Update an existing installation:

```shell
dotnet tool update --global Invex.Tools.ArtifactClean
```

ArtifactClean requires .NET 10 and the `dotnet` command on `PATH`. Published
packages include Native AOT assets for Windows, Linux, and macOS on x64 and
ARM64. The workspaces it cleans can target any .NET version.

## Usage

Run from the root of the workspace to clean:

```shell
artclean
```

Provide a root path to clean a different directory:

```shell
artclean --path "path/to/workspace"
```

## Options

| Option | Description |
| --- | --- |
| `-p`, `--path <directory>` | Root directory to clean. Defaults to the current directory. |
| `-n`, `--no-restore` | Do not run `dotnet restore` after cleaning. |
| `-v`, `--verbose` | Show each deleted directory and detailed `dotnet` output. |

Skip the restore step, or show each deleted directory and the output of the `dotnet` commands:

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

## Behavioral notes

- **Order.** `dotnet clean` runs first, then the `bin` / `obj` deletion, then `dotnet restore` (unless `--no-restore`).
  Both `dotnet` commands run with the selected directory as their working directory.
- **Matching.** Directory names are compared to `bin` and `obj` case-insensitively. A matched directory is deleted with all
  of its contents and is not searched further.
- **Traversal.** Symbolic links, junctions and other reparse points are skipped. Directories that cannot be read are
  skipped silently, or with a message when `--verbose` is set.
- **Failures.** A directory that cannot be deleted is always reported on standard error. Without `--verbose`, the
  output of `dotnet clean` and `dotnet restore` is hidden, but a failing command is reported with its exit code.

## Exit codes

The tool exits with code `0` on success and `1` when the path does not exist,
`dotnet clean` or `dotnet restore` fails, or a `bin` or `obj` directory cannot
be deleted. A failed `dotnet clean` does not stop the cleanup; the remaining
steps still run and the tool reports the failure through its exit code.

> [!WARNING]
> ArtifactClean permanently deletes all `bin` and `obj` directories below the selected path. It skips symbolic links and junctions while traversing the directory tree.

## See also

- [Getting Started](getting-started.md)
- [Introduction](introduction.md)
