using Microsoft.Extensions.Logging;
using Moq;
using Szechuan.McpCliBridge.Server.Domain;
using Szechuan.McpCliBridge.Server.Orchestration;
using Xunit;

namespace Szechuan.McpCliBridge.Server.Tests.Orchestration;

public class ScriptDiscovererTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<ScriptDiscoverer>> _mockLogger;

    public ScriptDiscovererTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _mockLogger = new Mock<ILogger<ScriptDiscoverer>>();
    }

    [Fact]
    public void DiscoverScripts_WithCsxFiles_ReturnsMatchingFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "tool1.csx"), "// tool1");
        File.WriteAllText(Path.Combine(_tempDir, "tool2.csx"), "// tool2");
        File.WriteAllText(Path.Combine(_tempDir, "other.txt"), "// other");

        var discoverer = new ScriptDiscoverer(new DirectoryInfo(_tempDir), _mockLogger.Object);

        // Act
        var scripts = discoverer.DiscoverScripts(["*.csx"]);

        // Assert
        Assert.Equal(2, scripts.Count);
        Assert.All(scripts, f => Assert.Equal(".csx", f.Extension));
    }

    [Fact]
    public void DiscoverScripts_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "// test");
        var discoverer = new ScriptDiscoverer(new DirectoryInfo(_tempDir), _mockLogger.Object);

        // Act
        var scripts = discoverer.DiscoverScripts(["*.csx"]);

        // Assert
        Assert.Empty(scripts);
    }

    [Fact]
    public void DiscoverScripts_WithMultiplePatterns_ReturnsCombinedResults()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "tool1.csx"), "// tool1");
        File.WriteAllText(Path.Combine(_tempDir, "script_util.csx"), "// util");

        var discoverer = new ScriptDiscoverer(new DirectoryInfo(_tempDir), _mockLogger.Object);

        // Act
        var scripts = discoverer.DiscoverScripts(["tool*.csx", "script_*.csx"]);

        // Assert
        Assert.Equal(2, scripts.Count);
    }

    [Fact]
    public void DiscoverScripts_RemovesDuplicates()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "tool.csx"), "// tool");

        var discoverer = new ScriptDiscoverer(new DirectoryInfo(_tempDir), _mockLogger.Object);

        // Act
        var scripts = discoverer.DiscoverScripts(["*.csx", "tool.csx"]);

        // Assert
        Assert.Single(scripts);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}

public class ScriptOrchestratorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<ScriptDiscoverer>> _mockDiscovererLogger;
    private readonly Mock<ILogger<ScriptOrchestrator>> _mockOrchestratorLogger;
    private readonly ScriptDiscoverer _discoverer;

    public ScriptOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _mockDiscovererLogger = new Mock<ILogger<ScriptDiscoverer>>();
        _mockOrchestratorLogger = new Mock<ILogger<ScriptOrchestrator>>();
        _discoverer = new ScriptDiscoverer(new DirectoryInfo(_tempDir), _mockDiscovererLogger.Object);
    }

    [Fact]
    public async Task InitializeAsync_WithValidScript_LoadsToolSuccessfully()
    {
        // Arrange
        var scriptContent = """
            var host = context.Host;
            host.Name("TestTool");
            host.Description("A test tool");
            host.Param("name", "Input name");
            host.OnExecute(async args => "Hello, " + (args.GetValueOrDefault("name") ?? "World"));
            var tool = host.BuildAndReset();
            if (tool != null) context.RegisterTool(tool);
            """;

        File.WriteAllText(Path.Combine(_tempDir, "test.csx"), scriptContent);

        var orchestrator = new ScriptOrchestrator(_discoverer, _mockOrchestratorLogger.Object);

        // Act
        await orchestrator.InitializeAsync(["*.csx"]);

        // Assert
        var tools = orchestrator.GetTools();
        Assert.Single(tools);
        Assert.Equal("TestTool", tools[0].Name);
    }

    [Fact]
    public async Task InitializeAsync_WithNoScripts_LoadsSuccessfully()
    {
        // Arrange
        var orchestrator = new ScriptOrchestrator(_discoverer, _mockOrchestratorLogger.Object);

        // Act
        await orchestrator.InitializeAsync(["*.csx"]);

        // Assert
        var tools = orchestrator.GetTools();
        Assert.Empty(tools);
    }

    [Fact]
    public async Task InitializeAsync_WithCompilationError_ThrowsException()
    {
        // Arrange
        var scriptContent = "this is not valid c#!";
        File.WriteAllText(Path.Combine(_tempDir, "broken.csx"), scriptContent);

        var orchestrator = new ScriptOrchestrator(_discoverer, _mockOrchestratorLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await orchestrator.InitializeAsync(["*.csx"]));
    }

    [Fact]
    public async Task ReloadScriptAsync_WithValidScript_UpdatesTools()
    {
        // Arrange
        var initialScript = """
            var host = context.Host;
            host.Name("OriginalTool");
            host.Description("Original");
            host.OnExecute(async args => "original");
            var tool = host.BuildAndReset();
            if (tool != null) context.RegisterTool(tool);
            """;

        File.WriteAllText(Path.Combine(_tempDir, "tool.csx"), initialScript);

        var orchestrator = new ScriptOrchestrator(_discoverer, _mockOrchestratorLogger.Object);
        await orchestrator.InitializeAsync(["*.csx"]);

        var scriptFile = new FileInfo(Path.Combine(_tempDir, "tool.csx"));
        var initialTools = orchestrator.GetTools();
        Assert.Single(initialTools);

        // Update the script
        var updatedScript = """
            var host = context.Host;
            host.Name("UpdatedTool");
            host.Description("Updated");
            host.OnExecute(async args => "updated");
            var tool = host.BuildAndReset();
            if (tool != null) context.RegisterTool(tool);
            """;

        File.WriteAllText(scriptFile.FullName, updatedScript);

        // Act
        var reloadSuccess = await orchestrator.ReloadScriptAsync(scriptFile);

        // Assert
        Assert.True(reloadSuccess);
        var updatedTools = orchestrator.GetTools();
        Assert.NotEmpty(updatedTools);
    }

    [Fact]
    public async Task ReloadScriptAsync_WithCompilationError_KeepsLastGoodState()
    {
        // Arrange
        var initialScript = """
            var host = context.Host;
            host.Name("GoodTool");
            host.Description("Good");
            host.OnExecute(async args => "good");
            var tool = host.BuildAndReset();
            if (tool != null) context.RegisterTool(tool);
            """;

        File.WriteAllText(Path.Combine(_tempDir, "tool.csx"), initialScript);

        var orchestrator = new ScriptOrchestrator(_discoverer, _mockOrchestratorLogger.Object);
        await orchestrator.InitializeAsync(["*.csx"]);

        var scriptFile = new FileInfo(Path.Combine(_tempDir, "tool.csx"));
        var initialTools = orchestrator.GetTools();
        var initialToolName = initialTools[0].Name;

        // Update with broken script
        File.WriteAllText(scriptFile.FullName, "this is broken!");

        // Act
        var reloadSuccess = await orchestrator.ReloadScriptAsync(scriptFile);

        // Assert
        Assert.False(reloadSuccess);
        var currentTools = orchestrator.GetTools();
        Assert.Single(currentTools);
        Assert.Equal(initialToolName, currentTools[0].Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
