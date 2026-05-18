using Szechuan.McpCliBridge.Server.Cli;
using Xunit;

namespace Szechuan.McpCliBridge.Server.Tests.Cli;

public class CliBootstrapperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalCwd;

    public CliBootstrapperTests()
    {
        _originalCwd = Directory.GetCurrentDirectory();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void CreateRootCommand_ReturnsRootCommandWithOptions()
    {
        // Act
        var rootCommand = CliBootstrapper.CreateRootCommand();

        // Assert
        Assert.NotNull(rootCommand);
        Assert.Equal(3, rootCommand.Options.Count);

        var dirOption = rootCommand.Options.OfType<System.CommandLine.Option<DirectoryInfo>>()
            .FirstOrDefault(o => o.Name == "dir");
        var scriptsOption = rootCommand.Options.OfType<System.CommandLine.Option<string[]>>()
            .FirstOrDefault(o => o.Name == "scripts");
        var watchOption = rootCommand.Options.OfType<System.CommandLine.Option<bool>>()
            .FirstOrDefault(o => o.Name == "watch");

        Assert.NotNull(dirOption);
        Assert.NotNull(scriptsOption);
        Assert.NotNull(watchOption);
        Assert.True(dirOption.IsRequired);
        Assert.False(scriptsOption.IsRequired);
        Assert.False(watchOption.IsRequired);
    }

    [Fact]
    public void ParseAndValidate_WithValidDirectory_ReturnsCliOptions()
    {
        // Arrange
        var args = new[] { "--dir", _tempDir };

        // Act
        var options = CliBootstrapper.ParseAndValidate(args);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(_tempDir, options.TargetDirectory.FullName);
        Assert.Contains("*.csx", options.ScriptPatterns);
        Assert.False(options.WatchMode);
    }

    [Fact]
    public void ParseAndValidate_WithNonexistentDirectory_ReturnsNull()
    {
        // Arrange
        var nonexistentDir = Path.Combine(_tempDir, "nonexistent");
        var args = new[] { "--dir", nonexistentDir };

        // Act
        var options = CliBootstrapper.ParseAndValidate(args);

        // Assert
        Assert.Null(options);
    }

    [Fact]
    public void ParseAndValidate_WithoutDirOption_ReturnsNull()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var options = CliBootstrapper.ParseAndValidate(args);

        // Assert
        Assert.Null(options);
    }

    [Fact]
    public void ParseAndValidate_WithCustomScriptsPattern_ReturnsCliOptionsWithPattern()
    {
        // Arrange
        var args = new[] { "--dir", _tempDir, "--scripts", "tool*.csx", "util*.csx" };

        // Act
        var options = CliBootstrapper.ParseAndValidate(args);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(2, options.ScriptPatterns.Length);
        Assert.Contains("tool*.csx", options.ScriptPatterns);
        Assert.Contains("util*.csx", options.ScriptPatterns);
    }

    [Fact]
    public void ParseAndValidate_WithWatchFlag_ReturnsCliOptionsWithWatchModeEnabled()
    {
        // Arrange
        var args = new[] { "--dir", _tempDir, "--watch" };

        // Act
        var options = CliBootstrapper.ParseAndValidate(args);

        // Assert
        Assert.NotNull(options);
        Assert.True(options.WatchMode);
    }

    [Fact]
    public void ParseAndValidate_SetsProcessWorkingDirectory()
    {
        // Arrange
        var args = new[] { "--dir", _tempDir };

        // Act
        var options = CliBootstrapper.ParseAndValidate(args);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(_tempDir, Directory.GetCurrentDirectory());
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
