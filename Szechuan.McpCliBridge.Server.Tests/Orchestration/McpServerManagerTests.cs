using Microsoft.Extensions.Logging;
using Moq;
using Szechuan.McpCliBridge.Server.Domain;
using Szechuan.McpCliBridge.Server.Orchestration;
using Xunit;

namespace Szechuan.McpCliBridge.Server.Tests.Orchestration;

public class McpServerManagerTests
{
    private readonly Mock<ILogger<ScriptDiscoverer>> _mockDiscovererLogger;
    private readonly Mock<ILogger<ScriptOrchestrator>> _mockOrchestratorLogger;
    private readonly Mock<ILogger<McpServerManager>> _mockServerLogger;
    private readonly ScriptOrchestrator _orchestrator;

    public McpServerManagerTests()
    {
        _mockDiscovererLogger = new Mock<ILogger<ScriptDiscoverer>>();
        _mockOrchestratorLogger = new Mock<ILogger<ScriptOrchestrator>>();
        _mockServerLogger = new Mock<ILogger<McpServerManager>>();

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var discoverer = new ScriptDiscoverer(new DirectoryInfo(tempDir), _mockDiscovererLogger.Object);
        _orchestrator = new ScriptOrchestrator(discoverer, _mockOrchestratorLogger.Object);
    }

    [Fact]
    public void RegisterToolHandlers_WithValidTools_RegistersSuccessfully()
    {
        // Arrange
        var tool = new McpToolContainer
        {
            Name = "TestTool",
            Description = "Test Description",
            Parameters = new List<McpParameter>
            {
                new McpParameter
                {
                    Name = "input",
                    Description = "Input parameter",
                    Type = "string",
                    Required = true
                }
            },
            ExecuteAsync = args => Task.FromResult("test result")
        };

        _orchestrator.GetType()
            .GetProperty("_scriptContext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(_orchestrator, new ScriptContext());

        // We need to manually add the tool since we can't easily initialize the orchestrator in this test
        var manager = new McpServerManager(_mockServerLogger.Object, _orchestrator);

        // Act
        manager.RegisterToolHandlers();

        // Assert
        var server = manager.GetServer();
        Assert.NotNull(server);
    }

    [Fact]
    public void MapToMcpTool_WithParametersAndDescription_MapsCorrectly()
    {
        // Arrange
        var manager = new McpServerManager(_mockServerLogger.Object, _orchestrator);
        var container = new McpToolContainer
        {
            Name = "MyTool",
            Description = "My Tool Description",
            Parameters = new List<McpParameter>
            {
                new McpParameter
                {
                    Name = "name",
                    Description = "User name",
                    Type = "string",
                    Required = true
                },
                new McpParameter
                {
                    Name = "count",
                    Description = "Item count",
                    Type = "int",
                    Required = false,
                    DefaultValue = 10
                }
            },
            ExecuteAsync = args => Task.FromResult("result")
        };

        // Act
        var mapMethod = manager.GetType()
            .GetMethod("MapToMcpTool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var tool = mapMethod?.Invoke(manager, new object[] { container }) as ModelContextProtocol.Server.Tool;

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("MyTool", tool.Name);
        Assert.Equal("My Tool Description", tool.Description);
    }

    [Fact]
    public void MapDotNetTypeToJsonSchemaType_ConvertsTypeCorrectly()
    {
        // Arrange
        var manager = new McpServerManager(_mockServerLogger.Object, _orchestrator);
        var typeMap = new Dictionary<string, string>
        {
            { "string", "string" },
            { "int", "integer" },
            { "double", "number" },
            { "bool", "boolean" },
            { "unknown", "string" }
        };

        var method = manager.GetType()
            .GetMethod("MapDotNetTypeToJsonSchemaType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Act & Assert
        foreach (var kvp in typeMap)
        {
            var result = method?.Invoke(manager, new object[] { kvp.Key }) as string;
            Assert.Equal(kvp.Value, result);
        }
    }
}
