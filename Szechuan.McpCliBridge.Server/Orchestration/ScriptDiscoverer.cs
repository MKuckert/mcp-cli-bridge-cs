using Microsoft.Extensions.Logging;

namespace Szechuan.McpCliBridge.Server.Orchestration;

/// <summary>
/// Discovers C# script files in a given directory matching specified patterns.
/// </summary>
public class ScriptDiscoverer
{
    private readonly DirectoryInfo _directory;
    private readonly ILogger<ScriptDiscoverer> _logger;

    public ScriptDiscoverer(DirectoryInfo directory, ILogger<ScriptDiscoverer> logger)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discovers all scripts matching the given patterns.
    /// </summary>
    public List<FileInfo> DiscoverScripts(string[] patterns)
    {
        var scripts = new List<FileInfo>();

        foreach (var pattern in patterns ?? ["*.csx"])
        {
            try
            {
                var files = _directory.EnumerateFiles(pattern)
                    .Where(f => f.Extension == ".csx")
                    .ToList();

                scripts.AddRange(files);
                _logger.LogInformation("Discovered {Count} scripts matching pattern: {Pattern}", files.Count, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering scripts with pattern: {Pattern}", pattern);
            }
        }

        // Remove duplicates
        return scripts.DistinctBy(f => f.FullName).ToList();
    }
}
