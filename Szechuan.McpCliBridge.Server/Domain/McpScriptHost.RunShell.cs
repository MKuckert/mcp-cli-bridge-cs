using CliWrap;
using CliWrap.EventStream;

namespace Szechuan.McpCliBridge.Server.Domain;

/// <summary>
/// Extended McpScriptHost with CLI execution capabilities via CliWrap.
/// </summary>
public partial class McpScriptHost
{
    private readonly ILogger<McpScriptHost>? _logger;

    /// <summary>
    /// Initialize the host with optional logger for diagnostics.
    /// </summary>
    public McpScriptHost(ILogger<McpScriptHost>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes a shell command with timeout support and comprehensive error handling.
    /// Captures stdout, stderr, and exit codes.
    /// </summary>
    public async Task<string> RunShell(string command, int timeoutMs = 30000)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be empty", nameof(command));
        }

        _logger?.LogInformation("Executing command: {Command}", command);

        try
        {
            var result = await ExecuteCommandWithTimeoutAsync(command, timeoutMs);
            return result;
        }
        catch (OperationCanceledException)
        {
            var errorMsg = $"Command timed out after {timeoutMs}ms: {command}";
            _logger?.LogError(errorMsg);
            throw new TimeoutException(errorMsg);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Command execution failed: {Command}", command);
            throw;
        }
    }

    /// <summary>
    /// Executes a command with timeout handling.
    /// Returns stdout on success or throws on timeout/non-zero exit.
    /// </summary>
    private async Task<string> ExecuteCommandWithTimeoutAsync(string command, int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);

        var stdoutBuffer = new List<string>();
        var stderrBuffer = new List<string>();

        try
        {
            // Use cmd.exe on Windows, /bin/sh on Unix
            var (shell, args) = OperatingSystem.IsWindows()
                ? ("cmd.exe", new[] { "/c", command })
                : ("/bin/sh", new[] { "-c", command });

            var result = await Cli.Wrap(shell)
                .WithArguments(args)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => stdoutBuffer.Add(line)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line => stderrBuffer.Add(line)))
                .ExecuteAsync(cts.Token);

            // Check for non-zero exit code
            if (result.ExitCode != 0)
            {
                var stderr = string.Join(Environment.NewLine, stderrBuffer);
                var errorMsg = $"Command returned exit code {result.ExitCode}: {command}\n{stderr}";
                _logger?.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            return string.Join(Environment.NewLine, stdoutBuffer);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Command execution cancelled (timeout): {Command}", command);
            throw;
        }
    }
}
