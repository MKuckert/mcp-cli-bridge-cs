using System.CommandLine;
using System.CommandLine.Invocation;

namespace Szechuan.McpCliBridge.Server.Cli;

/// <summary>
/// Responsible for setting up and parsing CLI arguments.
/// Handles validation and exits immediately on failure.
/// </summary>
public class CliBootstrapper
{
    /// <summary>
    /// Creates and configures the root command with all required options.
    /// </summary>
    public static RootCommand CreateRootCommand()
    {
        var dirOption = new Option<DirectoryInfo>("--dir")
        {
            Description = "Target directory where CLI programs are executed",
            Required = true
        };

        var scriptsOption = new Option<string[]>("--scripts")
        {
            Description = "Patterns for script discovery (e.g., *.csx)",
            Required = false,
            DefaultValueFactory = _ => new[] { "*.csx" },
            AllowMultipleArgumentsPerToken = true
        };

        var watchOption = new Option<bool>("--watch")
        {
            Description = "Enable watch mode for live script reloading",
            Required = false
        };

        return new RootCommand("MCP CLI Bridge - Dynamic C# Script Server")
        {
            dirOption,
            scriptsOption,
            watchOption
        };
    }

    /// <summary>
    /// Parses and validates CLI arguments.
    /// Returns null and exits with code 1 if validation fails.
    /// </summary>
    public static CliOptions? ParseAndValidate(string[] args)
    {
        var rootCommand = CreateRootCommand();

        var dirOption = rootCommand.Options.OfType<Option<DirectoryInfo>>().First();
        var scriptsOption = rootCommand.Options.OfType<Option<string[]>>().First();
        var watchOption = rootCommand.Options.OfType<Option<bool>>().First();

        rootCommand.SetAction((parseResult) =>
        {
            var targetDir = parseResult.GetValue(dirOption);

            // Validate directory exists and is readable
            if (targetDir == null || !targetDir.Exists)
            {
                Console.Error.WriteLine($"FATAL: Target directory does not exist: {targetDir?.FullName}");
                return 1;
            }

            if (!HasReadPermission(targetDir))
            {
                Console.Error.WriteLine($"FATAL: Target directory is not readable: {targetDir.FullName}");
                return 1;
            }

            var scripts = parseResult.GetValue(scriptsOption) ?? ["*.csx"];
            var watch = parseResult.GetValue(watchOption);

            // Set process working directory
            try
            {
                Directory.SetCurrentDirectory(targetDir.FullName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FATAL: Cannot set working directory: {ex.Message}");
                return 1;
            }

            // Store parsed options in a static context for the main execution
            CurrentOptions = new CliOptions
            {
                TargetDirectory = targetDir,
                ScriptPatterns = scripts,
                WatchMode = watch
            };

            return 0; // Success; execution continues
        });

        var exitCode = rootCommand.Parse(args).Invoke();
        return exitCode == 0 ? CurrentOptions : null;
    }

    /// <summary>
    /// Gets the last successfully parsed CLI options.
    /// </summary>
    public static CliOptions? CurrentOptions { get; private set; }

    /// <summary>
    /// Checks if the current process has read permission on the directory.
    /// </summary>
    private static bool HasReadPermission(DirectoryInfo directory)
    {
        try
        {
            _ = Directory.GetFileSystemEntries(directory.FullName).FirstOrDefault();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
