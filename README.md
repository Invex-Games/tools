# Invex.Dotnet.Tools

A collection of useful .NET tools for developers.

## Tools

### ArtifactClean (`artclean`)

`artclean` is a high-performance, recursive cleaning tool for .NET projects. It identifies and removes `bin` and `obj` directories across your workspace to reclaim disk space or ensure a clean state for your builds. 

By default, it automatically executes `dotnet restore` after cleaning to bring your projects back to a ready-to-build state.

#### Key Features
- **Fast Recursion**: Efficiently scans directories while ignoring reparse points.
- **Deep Clean**: Removes both `bin` and `obj` folders.
- **Auto-Restore**: Automatically runs `dotnet restore` to minimize downtime (can be disabled).
- **Native AOT**: Built with Native AOT for minimal startup time and zero dependencies.

#### Installation

```bash
dotnet tool install --global artclean
```

#### Usage

```bash
artclean [path] [options]
```

**Arguments:**
- `path`: The root directory to begin the recursive search. [Default: current directory]

**Options:**
- `-n, --no-restore`: Skips the `dotnet restore` operation after cleaning.

---

## Repository Structure

- `Invex.Tools.ArtifactClean`: Source code for the `artclean` tool.
- `_atom`: The **Atom** build system project used for CI/CD and automation.

## License

This project is licensed under the [MIT License](LICENSE.txt).
