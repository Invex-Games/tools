# Invex .NET Tools

A collection of small, focused .NET developer tools. The repository currently
ships **ArtifactClean**, exposed as the `artclean` .NET global tool.

## ArtifactClean

`artclean` cleans a .NET workspace by:

1. Running `dotnet clean` in the selected directory.
2. Recursively deleting directories named `bin` and `obj`.
3. Running `dotnet restore` unless restoration is disabled.

It skips symbolic links, junctions, and other reparse points while traversing
the directory tree. Inaccessible directories are skipped, while failures to
delete a matching directory are reported. Deletion is permanent, so select the
root path carefully.

### Installation

Install the latest published version as a global tool:

```shell
dotnet tool install --global Invex.Tools.ArtifactClean
```

Update an existing installation:

```shell
dotnet tool update --global Invex.Tools.ArtifactClean
```

### Usage

Clean the current directory:

```shell
artclean
```

Clean a specific workspace:

```shell
artclean --path "path/to/workspace"
```

Show each deleted directory and the output from the `dotnet` commands:

```shell
artclean --verbose
```

Skip the restore step after cleaning:

```shell
artclean --no-restore
```

The complete command syntax is:

```text
artclean [options]
  -p, --path <directory>  Root directory to clean (default: current directory)
  -n, --no-restore        Do not run dotnet restore after cleaning
  -v, --verbose           Show detailed cleanup and dotnet command output
```

`dotnet clean` and `dotnet restore` are run with the selected directory as
their working directory. The tool requires the `dotnet` command to be
available on `PATH`.

The tool exits with code `0` on success and `1` when the path does not exist,
`dotnet clean` or `dotnet restore` fails, or a `bin` or `obj` directory cannot
be deleted. A failed `dotnet clean` does not stop the cleanup; the remaining
steps still run and the tool reports the failure through its exit code.

### Supported platforms

The tool targets .NET 8 and .NET 10. Published .NET 10 packages include
Native AOT assets for:

- Windows x64 and ARM64
- Linux x64 and ARM64
- macOS x64 and ARM64

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/Invex.Tools.ArtifactClean` | Source for the `artclean` tool |
| `_atom` | Atom build definition used to generate CI and release workflows |
| `docs` | DocFX source for the project documentation |
| `Invex.Tools.slnx` | Repository solution |

## License

This project is available under the [MIT License](LICENSE.txt).
