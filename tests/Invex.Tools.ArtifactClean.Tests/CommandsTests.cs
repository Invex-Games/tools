namespace Invex.Tools.ArtifactClean.Tests;

[TestFixture]
[NonParallelizable]
internal sealed class CommandsTests
{
    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(),
            "artclean-tests",
            Guid
                .NewGuid()
                .ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(_root))
            return;

        // Remove links first so deleting the tree never follows them into their targets
        foreach (var directory in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories))
            if (new DirectoryInfo(directory).LinkTarget is not null)
                Directory.Delete(directory);

        Directory.Delete(_root, true);
    }

    private string _root = null!;

    [Test]
    public void CleanRecursive_DeletesBinAndObjDirectories_AndCountsThem()
    {
        // Arrange
        CreateFile("ProjectA", "bin", "Debug", "a.dll");
        CreateFile("ProjectA", "obj", "project.assets.json");
        CreateFile("ProjectA", "ProjectA.csproj");
        CreateFile("src", "ProjectB", "bin", "b.dll");
        CreateFile("src", "ProjectB", "obj", "b.obj");
        CreateFile("src", "ProjectB", "Program.cs");

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(_root, false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(4);
        failedDirectoryCount.ShouldBe(0);

        Directory
            .Exists(PathOf("ProjectA", "bin"))
            .ShouldBeFalse();

        Directory
            .Exists(PathOf("ProjectA", "obj"))
            .ShouldBeFalse();

        Directory
            .Exists(PathOf("src", "ProjectB", "bin"))
            .ShouldBeFalse();

        Directory
            .Exists(PathOf("src", "ProjectB", "obj"))
            .ShouldBeFalse();

        File
            .Exists(PathOf("ProjectA", "ProjectA.csproj"))
            .ShouldBeTrue();

        File
            .Exists(PathOf("src", "ProjectB", "Program.cs"))
            .ShouldBeTrue();
    }

    [Test]
    public void CleanRecursive_DeletesBinAndObjDirectoriesDirectlyUnderRoot()
    {
        // Arrange
        CreateFile("bin", "a.dll");
        CreateFile("obj", "a.obj");

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(_root, false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(2);
        failedDirectoryCount.ShouldBe(0);

        Directory
            .Exists(PathOf("bin"))
            .ShouldBeFalse();

        Directory
            .Exists(PathOf("obj"))
            .ShouldBeFalse();

        Directory
            .Exists(_root)
            .ShouldBeTrue();
    }

    [Test]
    public void CleanRecursive_MatchesNamesCaseInsensitively()
    {
        // Arrange
        CreateFile("ProjectA", "BIN", "a.dll");
        CreateFile("ProjectB", "Obj", "b.obj");

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(_root, false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(2);
        failedDirectoryCount.ShouldBe(0);

        Directory
            .Exists(PathOf("ProjectA", "BIN"))
            .ShouldBeFalse();

        Directory
            .Exists(PathOf("ProjectB", "Obj"))
            .ShouldBeFalse();
    }

    [Test]
    public void CleanRecursive_CountsNestedBinAndObjInsideDeletedDirectoryOnce()
    {
        // Arrange
        CreateFile("Project", "bin", "obj", "nested.obj");
        CreateFile("Project", "bin", "bin", "nested.dll");

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(_root, false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(1);
        failedDirectoryCount.ShouldBe(0);

        Directory
            .Exists(PathOf("Project", "bin"))
            .ShouldBeFalse();
    }

    [Test]
    public void CleanRecursive_LeavesSimilarlyNamedDirectoriesAndFiles()
    {
        // Arrange
        CreateFile("binaries", "a.dll");
        CreateFile("objects", "a.obj");
        CreateFile("Project", "bin.txt");
        CreateFile("Project", "obj");

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(_root, false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(0);
        failedDirectoryCount.ShouldBe(0);

        Directory
            .Exists(PathOf("binaries"))
            .ShouldBeTrue();

        Directory
            .Exists(PathOf("objects"))
            .ShouldBeTrue();

        File
            .Exists(PathOf("Project", "bin.txt"))
            .ShouldBeTrue();

        File
            .Exists(PathOf("Project", "obj"))
            .ShouldBeTrue();
    }

    [Test]
    public void CleanRecursive_WithNoArtifacts_DeletesNothing()
    {
        // Arrange
        CreateFile("src", "Project", "Program.cs");

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(_root, false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(0);
        failedDirectoryCount.ShouldBe(0);

        File
            .Exists(PathOf("src", "Project", "Program.cs"))
            .ShouldBeTrue();
    }

    [Test]
    public void CleanRecursive_AddsToExistingCount()
    {
        // Arrange
        CreateFile("Project", "bin", "a.dll");

        var deletedDirectoryCount = 3;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(_root, false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(4);
        failedDirectoryCount.ShouldBe(0);
    }

    [Test]
    public void CleanRecursive_DoesNotFollowLinkedDirectories()
    {
        // Arrange
        var outside = PathOf("outside");
        CreateFile("outside", "Project", "bin", "a.dll");
        CreateFile("root", "Project", "obj", "b.obj");
        CreateDirectoryLink(PathOf("root", "Linked"), outside);

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(PathOf("root"), false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(1);
        failedDirectoryCount.ShouldBe(0);

        Directory
            .Exists(PathOf("root", "Project", "obj"))
            .ShouldBeFalse();

        File
            .Exists(Path.Combine(outside, "Project", "bin", "a.dll"))
            .ShouldBeTrue();
    }

    [Test]
    public void CleanRecursive_DoesNotDeleteLinkNamedBin()
    {
        // Arrange
        var target = PathOf("target");
        CreateFile("target", "keep.txt");
        CreateDirectoryLink(PathOf("root", "Project", "bin"), target);

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;

        // Act
        Commands.CleanRecursive(PathOf("root"), false, ref deletedDirectoryCount, ref failedDirectoryCount);

        // Assert
        deletedDirectoryCount.ShouldBe(0);
        failedDirectoryCount.ShouldBe(0);

        Directory
            .Exists(PathOf("root", "Project", "bin"))
            .ShouldBeTrue();

        File
            .Exists(Path.Combine(target, "keep.txt"))
            .ShouldBeTrue();
    }

    [Test]
    public void CleanRecursive_Verbose_ReportsEachDeletedDirectory()
    {
        // Arrange
        CreateFile("Project", "bin", "a.dll");

        var deletedDirectoryCount = 0;
        var failedDirectoryCount = 0;
        using var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        // Act
        try
        {
            Commands.CleanRecursive(_root, true, ref deletedDirectoryCount, ref failedDirectoryCount);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Assert
        deletedDirectoryCount.ShouldBe(1);
        failedDirectoryCount.ShouldBe(0);

        output
            .ToString()
            .ShouldContain($"Deleted: {PathOf("Project", "bin")}");
    }

    [Test]
    public void Clean_WithNonExistentPath_ReportsErrorAndReturnsFailure()
    {
        // Arrange
        var missing = PathOf("missing");
        using var error = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(error);

        // Act
        try
        {
            new Commands().Clean(missing, true);
        }
        finally
        {
            Console.SetError(originalError);
        }

        // Assert
        error
            .ToString()
            .ShouldContain($"Invalid or non-existent path: {missing}");

        Directory
            .Exists(missing)
            .ShouldBeFalse();
    }

    [Test]
    public void Clean_WhenDotnetCleanFails_StillDeletesArtifactsAndReturnsFailure()
    {
        // Arrange - no project or solution here, so 'dotnet clean' exits non-zero
        CreateFile("Project", "bin", "a.dll");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        Console.SetOut(output);
        Console.SetError(error);

        // Act
        int exitCode;

        try
        {
            exitCode = new Commands().Clean(_root, true);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldContain("'dotnet clean' failed with exit code");
        output.ToString().ShouldContain("Deleted 1 'bin' / 'obj' directories.");
        Directory.Exists(PathOf("Project", "bin")).ShouldBeFalse();
    }

    private string PathOf(params string[] parts) =>
        Path.Combine([_root, .. parts]);

    private void CreateFile(params string[] parts)
    {
        var path = PathOf(parts);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    private static void CreateDirectoryLink(string linkPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);

        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Windows needs Developer Mode or elevation to create symbolic links
            Assert.Ignore($"Symbolic links are not available: {ex.Message}");
        }
    }
}
