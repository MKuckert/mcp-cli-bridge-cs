namespace Szechuan.McpCliBridge.Server.Cli;

/// <summary>
/// Represents the parsed CLI arguments.
/// </summary>
public class CliOptions
{
    public required DirectoryInfo TargetDirectory { get; set; }
    public string[] ScriptPatterns { get; set; } = ["*.csx"];
    public bool WatchMode { get; set; }
}
