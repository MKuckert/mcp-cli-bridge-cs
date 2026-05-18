using Szechuan.McpCliBridge.Server.Domain;
using Xunit;

namespace Szechuan.McpCliBridge.Server.Tests.Domain;

public class McpScriptHostTests
{
    [Fact]
    public void BuildAndReset_WithValidToolDefinition_ReturnsToolContainer()
    {
        // Arrange
        var host = new McpScriptHost();
        host.Name("TestTool");
        host.Description("A test tool");
        host.Param("input", "Input parameter", "string", required: true);
        host.OnExecute(args => Task.FromResult("result"));

        // Act
        var tool = host.BuildAndReset();

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("TestTool", tool.Name);
        Assert.Equal("A test tool", tool.Description);
        Assert.Single(tool.Parameters);
        Assert.Equal("input", tool.Parameters[0].Name);
    }

    [Fact]
    public void BuildAndReset_WithoutName_ReturnsNull()
    {
        // Arrange
        var host = new McpScriptHost();
        host.Description("A test tool");
        host.OnExecute(args => Task.FromResult("result"));

        // Act
        var tool = host.BuildAndReset();

        // Assert
        Assert.Null(tool);
    }

    [Fact]
    public void BuildAndReset_WithoutDescription_ReturnsNull()
    {
        // Arrange
        var host = new McpScriptHost();
        host.Name("TestTool");
        host.OnExecute(args => Task.FromResult("result"));

        // Act
        var tool = host.BuildAndReset();

        // Assert
        Assert.Null(tool);
    }

    [Fact]
    public void BuildAndReset_WithoutOnExecute_ReturnsNull()
    {
        // Arrange
        var host = new McpScriptHost();
        host.Name("TestTool");
        host.Description("A test tool");

        // Act
        var tool = host.BuildAndReset();

        // Assert
        Assert.Null(tool);
    }

    [Fact]
    public void BuildAndReset_ResetsStateForNextTool()
    {
        // Arrange
        var host = new McpScriptHost();
        host.Name("FirstTool");
        host.Description("First description");
        host.Param("param1", "description");
        host.OnExecute(args => Task.FromResult("result"));

        // Act
        var firstTool = host.BuildAndReset();

        // Now define a second tool - should only have its own params
        host.Name("SecondTool");
        host.Description("Second description");
        host.OnExecute(args => Task.FromResult("result2"));
        var secondTool = host.BuildAndReset();

        // Assert
        Assert.NotNull(firstTool);
        Assert.NotNull(secondTool);
        Assert.Single(firstTool.Parameters);
        Assert.Empty(secondTool.Parameters);
    }
}

public class ScriptContextTests
{
    [Fact]
    public void Constructor_InitializesHostAndTools()
    {
        // Act
        var context = new ScriptContext();

        // Assert
        Assert.NotNull(context.Host);
        Assert.NotNull(context.Tools);
        Assert.Empty(context.Tools);
    }

    [Fact]
    public void RegisterTool_AddsTool()
    {
        // Arrange
        var context = new ScriptContext();
        var tool = new McpToolContainer
        {
            Name = "TestTool",
            Description = "Test",
            Parameters = [],
            ExecuteAsync = args => Task.FromResult("result")
        };

        // Act
        context.RegisterTool(tool);

        // Assert
        Assert.Single(context.Tools);
        Assert.Equal("TestTool", context.Tools[0].Name);
    }

    [Fact]
    public void GetTools_ReturnsReadOnlyList()
    {
        // Arrange
        var context = new ScriptContext();
        var tool = new McpToolContainer
        {
            Name = "TestTool",
            Description = "Test",
            Parameters = [],
            ExecuteAsync = args => Task.FromResult("result")
        };
        context.RegisterTool(tool);

        // Act
        var tools = context.GetTools();

        // Assert
        Assert.Single(tools);
        Assert.Throws<NotSupportedException>(() => tools.Add(null!));
    }
}
