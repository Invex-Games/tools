namespace Invex.Tools.ArtifactClean;

public sealed class Commands
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
    };

    /// <summary>
    ///     Runs 'dotnet clean', then recursively deletes 'bin' and 'obj' directories from the specified path and optionally
    ///     restores the project.
    /// </summary>
    /// <param name="path">-p, The root path to start cleaning from. [Default: Current directory]</param>
    /// <param name="noRestore">-n, If true, skips any restore operations. [Default: false]</param>
    [PublicAPI]
    [Command("")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Used by ConsoleAppFramework")]
    public void Clean([HideDefaultValue] string? path = null, bool noRestore = false)
    {
        path ??= Directory.GetCurrentDirectory();

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        DotnetClean();
        CleanRecursive(path);

        if (noRestore)
            return;

        var restoreProcessStartInfo = new ProcessStartInfo("dotnet", "restore")
        {
            WorkingDirectory = path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(restoreProcessStartInfo);

        if (process == null)
        {
            Console.Error.WriteLine("Failed to start 'dotnet restore' process.");

            return;
        }

        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            Console.WriteLine("'dotnet restore' completed successfully.");
        }
        else
        {
            Console.Error.WriteLine($"'dotnet restore' failed with exit code {process.ExitCode}.");
            Console.Error.WriteLine(process.StandardError.ReadToEnd());
        }
    }

    private static void DotnetClean()
    {
        var cleanProcessStartInfo = new ProcessStartInfo("dotnet", "clean")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(cleanProcessStartInfo);

        if (process == null)
        {
            Console.Error.WriteLine("Failed to start 'dotnet clean' process.");

            return;
        }

        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            Console.WriteLine("'dotnet clean' completed successfully.");
        }
        else
        {
            Console.Error.WriteLine($"'dotnet clean' failed with exit code {process.ExitCode}.");
            Console.Error.WriteLine(process.StandardError.ReadToEnd());
        }
    }

    private static void CleanRecursive(string path)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(path, "*", EnumerationOptions))
            {
                var name = Path.GetFileName(directory.AsSpan());

                // Fast comparison using Span
                if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                    DeleteDirectory(directory);
                else
                    CleanRecursive(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore directories that cannot be accessed
        }
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
            Console.WriteLine($"Deleted: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to delete {path}: {ex.Message}");
        }
    }
}
