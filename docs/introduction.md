# Introduction

Invex .NET Tools is a home for small, single-purpose command-line tools that the Invex team uses while
developing .NET code. Each tool is published as its own .NET global tool package. Today the repository ships one
tool, ArtifactClean (`artclean`).

## What it is

| Tool | Command | Package | Purpose |
|------|---------|---------|---------|
| ArtifactClean | `artclean` | `Invex.Tools.ArtifactClean` | Reset a .NET workspace: `dotnet clean`, delete `bin` / `obj`, `dotnet restore`. |

## When to use ArtifactClean

- Builds behave strangely after switching branches, SDKs or target frameworks, and `dotnet clean` alone does not
  help because stale files sit in `obj` (for example `project.assets.json` or generated sources).
- You want to free disk space taken by build output across many projects under one folder.
- You want a repeatable "clean slate" step in a local script, with an exit code that says whether it worked.

### When not to use it

- **To clean a single project in place.** `dotnet clean` (or deleting that one `bin` / `obj` folder) is enough.
- **When `bin` or `obj` holds something you need.** The tool deletes every directory with those names, case-insensitively,
  below the root, including ones that are not build output.
- **On a directory you did not mean to select.** There is no confirmation prompt and deletion is permanent; prefer
  `--path` with an explicit workspace root in scripts.

## Key concepts at a glance

| Concept | Meaning |
|---------|---------|
| **Root path** | The directory passed with `--path`, or the current directory. All three steps run against it. |
| **Artifact directory** | Any directory named `bin` or `obj` (any casing) below the root. It is deleted with all of its contents. |
| **Reparse point** | A symbolic link, junction or similar. The tool never follows one, so it cannot escape the root or loop. |
| **Exit code** | `0` when every step succeeded, `1` otherwise. |

## Design principles

- **Do one job well.** Each tool covers one routine task with as few options as possible.
- **Keep going, then report.** A failing step does not abort the rest; the failure shows up in the output and the exit code.
- **Stay inside the root.** Links and junctions are skipped so a cleanup never reaches outside the selected tree.
- **Quiet by default.** Normal runs print a short summary; detail is opt-in with `--verbose`.
- **Start fast.** Packages include Native AOT builds so a short-lived command does not pay JIT start-up cost.

## See also

- [Getting Started](getting-started.md)
- [ArtifactClean](artifactclean.md)
