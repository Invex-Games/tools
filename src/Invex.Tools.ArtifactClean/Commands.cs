namespace Invex.Tools.ArtifactClean;

public sealed class Commands
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        // Handle recursion manually for better control
        RecurseSubdirectories = false,

        // Skip symlinks/junctions to avoid infinite loops
        AttributesToSkip = FileAttributes.ReparsePoint,

        // Continue on permission errors (handled in catch block)
        IgnoreInaccessible = true,

        // Skip "." and ".." entries
        ReturnSpecialDirectories = false,

        // Faster matching since we use "*" pattern (not complex wildcards)
        MatchType = MatchType.Simple,
    };

    /// <summary>
    ///     Runs 'dotnet clean', then recursively deletes 'bin' and 'obj' directories from the specified path, then
    ///     optionally restores the project.
    /// </summary>
    /// <param name="path">-p, The root path to start cleaning from. [Default: Current directory]</param>
    /// <param name="noRestore">-n, If true, skips any restore operations. [Default: false]</param>
    /// <param name="verbose">-v, If true, outputs detailed information about the cleaning process. [Default: false]</param>
    [PublicAPI]
    [Command("")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Required by ConsoleAppFramework")]
    public void Clean([HideDefaultValue] string? path = null, bool noRestore = false, bool verbose = false)
    {
        path ??= Directory.GetCurrentDirectory();

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Console.Error.WriteLine($"Invalid or non-existent path: {path}");

            return;
        }

        RunDotnetCommand("clean", path, verbose);

        // Track deleted directories across recursive calls using ref parameter
        var deletedDirectoryCount = 0;

        CleanRecursive(path, verbose, ref deletedDirectoryCount);
        Console.WriteLine($"Deleted {deletedDirectoryCount} 'bin' / 'obj' directories.");

        if (!noRestore)
            RunDotnetCommand("restore", path, verbose);
    }

    private static void RunDotnetCommand(string command, string path, bool verbose)
    {
        var processStartInfo = new ProcessStartInfo("dotnet", command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = path,
        };

        using var process = new Process();
        process.StartInfo = processStartInfo;

        // Always subscribe to output events, even in non-verbose mode
        // This is required because we always call BeginOutputReadLine below
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null && verbose)
                Console.WriteLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null && verbose)
                Console.Error.WriteLine(e.Data);
        };

        process.Start();

        // Should always drain output streams to prevent buffer deadlock
        // When RedirectStandardOutput/Error = true but streams aren't consumed,
        // the process buffer can fill and hang. BeginOutputReadLine() drains asynchronously.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        if (process.ExitCode != 0)
            Console.Error.WriteLine($"'dotnet {command}' failed with exit code {process.ExitCode}.");
        else if (!verbose)
            Console.WriteLine($"'dotnet {command}' completed successfully.");
    }

    private static void CleanRecursive(string path, bool verbose, ref int deletedDirectoryCount)
    {
        try
        {
            var directories = Directory
                .EnumerateDirectories(path, "*", EnumerationOptions)
                .ToArray();

            var directoriesToDelete = new List<string>(4);

            // First pass: identify bin/obj directories
            foreach (var directory in directories)
            {
                // Use Span<char> to avoid string allocation when getting directory name
                var name = Path.GetFileName(directory.AsSpan());

                if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                    directoriesToDelete.Add(directory);
            }

            // Delete bin/obj directories in parallel for better performance
            if (directoriesToDelete.Count > 0)
            {
                // Use local counter for thread-safe parallel increments
                var localDeleteCount = 0;

                Parallel.ForEach(directoriesToDelete,
                    directory =>
                    {
                        try
                        {
                            // Delete recursively (true parameter) to remove all contents
                            Directory.Delete(directory, true);
                            Interlocked.Increment(ref localDeleteCount);

                            if (verbose)
                                Console.WriteLine($"Deleted: {directory}");
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            // Always report deletion failures (unlike traversal errors, user should know)
                            Console.Error.WriteLine($"Failed to delete {directory}: {ex.Message}");
                        }
                    });

                // Add local count to the total
                deletedDirectoryCount += localDeleteCount;
            }

            // Second pass: recurse into other directories (sequential to avoid race conditions)
            // Skip if we already marked this for deletion in first pass
            foreach (var directory in directories)
                if (!directoriesToDelete.Contains(directory))
                    CleanRecursive(directory, verbose, ref deletedDirectoryCount);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Catch specific exceptions: permission denied, path too long, etc.
            // Silently continue unless verbose mode (don't interrupt bulk cleaning)
            if (verbose)
                Console.Error.WriteLine($"Skipped inaccessible directory: {path} - {ex.Message}");
        }
    }
}
