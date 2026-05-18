using Szechuan.McpCliBridge.Server.Domain;
using Xunit;

namespace Szechuan.McpCliBridge.Server.Tests.Domain;

public class McpScriptHostRunShellTests
{
    [Fact]
    public async Task RunShell_WithEchoCommand_ReturnsOutput()
    {
        // Arrange
        var host = new McpScriptHost();
        var command = OperatingSystem.IsWindows() ? "echo test" : "echo test";

        // Act
        var result = await host.RunShell(command);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("test", result);
    }

    [Fact]
    public async Task RunShell_WithPwdCommand_ReturnsCurrentDirectory()
    {
        // Arrange
        var host = new McpScriptHost();
        var command = OperatingSystem.IsWindows() ? "cd" : "pwd";

        // Act
        var result = await host.RunShell(command);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task RunShell_WithNonexistentCommand_ThrowsInvalidOperation()
    {
        // Arrange
        var host = new McpScriptHost();
        var command = OperatingSystem.IsWindows()
            ? "nonexistent_cmd_12345"
            : "nonexistent_cmd_12345";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await host.RunShell(command));
    }

    [Fact]
    public async Task RunShell_WithTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var host = new McpScriptHost();
        var command = OperatingSystem.IsWindows()
            ? "timeout /t 5"
            : "sleep 5";

        // Act & Assert - 100ms timeout with a command that takes longer
        await Assert.ThrowsAsync<TimeoutException>(
            async () => await host.RunShell(command, timeoutMs: 100));
    }

    [Fact]
    public async Task RunShell_WithEmptyCommand_ThrowsArgumentException()
    {
        // Arrange
        var host = new McpScriptHost();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await host.RunShell(""));
    }

    [Fact]
    public async Task RunShell_WithMultilineOutput_ReturnsAllLines()
    {
        // Arrange
        var host = new McpScriptHost();
        var command = OperatingSystem.IsWindows()
            ? "(echo line1) & (echo line2) & (echo line3)"
            : "echo 'line1'; echo 'line2'; echo 'line3'";

        // Act
        var result = await host.RunShell(command);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("line1", result);
        Assert.Contains("line2", result);
        Assert.Contains("line3", result);
    }
}
