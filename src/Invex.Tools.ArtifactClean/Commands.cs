namespace Invex.Tools.ArtifactClean;

/// <summary>
///     The <c>artclean</c> command set registered with ConsoleAppFramework, which cleans a .NET workspace by running
///     <c>dotnet clean</c>, deleting every <c>bin</c> and <c>obj</c> directory, and optionally running
///     <c>dotnet restore</c>.
/// </summary>
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
    /// <returns>
    ///     0 on success; 1 if the path is invalid, 'dotnet clean' or 'dotnet restore' fails, or a 'bin' / 'obj'
    ///     directory could not be deleted.
    /// </returns>
    [PublicAPI]
    [Command("")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Required by ConsoleAppFramework")]
    public int Clean([HideDefaultValue] string? path = null, bool noRestore = false, bool verbose = false)
    {
        path ??= Directory.GetCurrentDirectory();

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Console.Error.WriteLine($"Invalid or non-existent path: {path}");

            return 1;
        }

        // Keep cleaning after a failed 'dotnet clean' so artifacts are still removed, but report the failure
        var succeeded = RunDotnetCommand("clean", path, verbose);

        // Track deleted and failed directories across recursive calls using ref parameters
        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        CleanRecursive(path, verbose, ref deletedDirectoryCount, ref failedDirectoryCount);
        Console.WriteLine($"Deleted {deletedDirectoryCount} 'bin' / 'obj' directories.");

        if (failedDirectoryCount > 0)
        {
            Console.Error.WriteLine($"Failed to delete {failedDirectoryCount} 'bin' / 'obj' directories.");
            succeeded = false;
        }

        if (!noRestore && !RunDotnetCommand("restore", path, verbose))
            succeeded = false;

        return succeeded
            ? 0
            : 1;
    }

    private static bool RunDotnetCommand(string command, string path, bool verbose)
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
        {
            Console.Error.WriteLine($"'dotnet {command}' failed with exit code {process.ExitCode}.");

            return false;
        }

        if (!verbose)
            Console.WriteLine($"'dotnet {command}' completed successfully.");

        return true;
    }

    internal static void CleanRecursive(
        string path,
        bool verbose,
        ref int deletedDirectoryCount,
        ref int failedDirectoryCount)
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
                var localFailureCount = 0;

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
                            Interlocked.Increment(ref localFailureCount);
                            Console.Error.WriteLine($"Failed to delete {directory}: {ex.Message}");
                        }
                    });

                // Add local count to the total
                deletedDirectoryCount += localDeleteCount;
                failedDirectoryCount += localFailureCount;
            }

            // Second pass: recurse into other directories (sequential to avoid race conditions)
            // Skip if we already marked this for deletion in first pass
            foreach (var directory in directories)
                if (!directoriesToDelete.Contains(directory))
                    CleanRecursive(directory, verbose, ref deletedDirectoryCount, ref failedDirectoryCount);
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
