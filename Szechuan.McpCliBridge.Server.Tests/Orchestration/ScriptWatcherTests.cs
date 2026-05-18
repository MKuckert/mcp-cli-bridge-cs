using Microsoft.Extensions.Logging;
using Moq;
using Szechuan.McpCliBridge.Server.Orchestration;
using Xunit;

namespace Szechuan.McpCliBridge.Server.Tests.Orchestration;

public class ScriptWatcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<ScriptDiscoverer>> _mockDiscovererLogger;
    private readonly Mock<ILogger<ScriptOrchestrator>> _mockOrchestratorLogger;
    private readonly Mock<ILogger<ScriptWatcher>> _mockWatcherLogger;
    private readonly ScriptOrchestrator _orchestrator;

    public ScriptWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _mockDiscovererLogger = new Mock<ILogger<ScriptDiscoverer>>();
        _mockOrchestratorLogger = new Mock<ILogger<ScriptOrchestrator>>();
        _mockWatcherLogger = new Mock<ILogger<ScriptWatcher>>();

        var discoverer = new ScriptDiscoverer(new DirectoryInfo(_tempDir), _mockDiscovererLogger.Object);
        _orchestrator = new ScriptOrchestrator(discoverer, _mockOrchestratorLogger.Object);
    }

    [Fact]
    public void Constructor_InitializesWatcher()
    {
        // Act
        var watcher = new ScriptWatcher(
            new DirectoryInfo(_tempDir),
            _orchestrator,
            _mockWatcherLogger.Object);

        // Assert
        Assert.NotNull(watcher);
    }

    [Fact]
    public void Start_EnablesWatcher()
    {
        // Arrange
        var watcher = new ScriptWatcher(
            new DirectoryInfo(_tempDir),
            _orchestrator,
            _mockWatcherLogger.Object);

        // Act
        watcher.Start();

        // Assert
        _mockWatcherLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Script watcher started")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Stop_DisablesWatcher()
    {
        // Arrange
        var watcher = new ScriptWatcher(
            new DirectoryInfo(_tempDir),
            _orchestrator,
            _mockWatcherLogger.Object);
        watcher.Start();

        // Act
        watcher.Stop();

        // Assert
        _mockWatcherLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Script watcher stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ToolsChanged_FiresWhenReloadSucceeds()
    {
        // Arrange
        var scriptContent = """
            var host = context.Host;
            host.Name("ReloadableTool");
            host.Description("Reloadable tool");
            host.OnExecute(async args => "result");
            var tool = host.BuildAndReset();
            if (tool != null) context.RegisterTool(tool);
            """;

        File.WriteAllText(Path.Combine(_tempDir, "tool.csx"), scriptContent);
        await _orchestrator.InitializeAsync(["*.csx"]);

        var watcher = new ScriptWatcher(
            new DirectoryInfo(_tempDir),
            _orchestrator,
            _mockWatcherLogger.Object);

        var toolsChangedFired = false;
        watcher.ToolsChanged += () => toolsChangedFired = true;

        // Act - manually trigger reload
        var scriptFile = new FileInfo(Path.Combine(_tempDir, "tool.csx"));
        await watcher.GetType()
            .GetMethod("ReloadScriptAfterDelayAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(watcher, new object[] { scriptFile.FullName, scriptFile.Name }) as Task
            ?? Task.CompletedTask;

        // Give event handler time to fire
        await Task.Delay(100);

        // Assert
        Assert.True(toolsChangedFired);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var watcher = new ScriptWatcher(
            new DirectoryInfo(_tempDir),
            _orchestrator,
            _mockWatcherLogger.Object);

        // Act
        watcher.Dispose();

        // Assert
        // If no exception is thrown, the test passes
        Assert.True(true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
