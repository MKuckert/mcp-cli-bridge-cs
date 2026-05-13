using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using CliWrap;

namespace Szechuan.McpCliBridge.Server;

public class ToolRegistry
{
    private readonly string _scriptsDir;
    private readonly string _targetDir;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, McpToolContainer> _tools = new();

    public ToolRegistry(string scriptsDir, string targetDir, ILogger logger)
    {
        _scriptsDir = scriptsDir;
        _targetDir = targetDir;
        _logger = logger;
    }

    public IEnumerable<McpToolContainer> Tools => _tools.Values;

    public async Task DiscoverToolsAsync()
    {
        if (!Directory.Exists(_scriptsDir))
        {
            _logger.LogWarning("Scripts directory does not exist: {ScriptsDir}", _scriptsDir);
            return;
        }

        var files = Directory.GetFiles(_scriptsDir, "*.csx");
        foreach (var file in files)
        {
            await LoadToolAsync(file);
        }
    }

    public async Task<bool> LoadToolAsync(string filePath)
    {
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var host = new McpScriptHost { TargetWorkDir = _targetDir };
            
            var options = ScriptOptions.Default
                .WithReferences(typeof(Cli).Assembly, typeof(Task).Assembly)
                .WithImports("System", "System.Threading.Tasks", "CliWrap", "CliWrap.Buffered");

            var state = await CSharpScript.RunAsync(code, options, globals: host);

            if (string.IsNullOrWhiteSpace(host.ToolName) || host.ExecutionLogic == null)
            {
                _logger.LogWarning("Script {FilePath} is missing Name() or OnExecute(). Skipping.", filePath);
                return false;
            }

            var container = new McpToolContainer
            {
                Host = host,
                CompiledScriptState = state,
                FilePath = filePath
            };

            _tools[host.ToolName] = container;
            _logger.LogInformation("Loaded tool: {ToolName} from {FilePath}", host.ToolName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script {FilePath}", filePath);
            return false;
        }
    }

    public McpToolContainer? GetTool(string name)
    {
        _tools.TryGetValue(name, out var container);
        return container;
    }

    public void RemoveTool(string filePath)
    {
        var toolToRemove = _tools.FirstOrDefault(t => t.Value.FilePath == filePath);
        if (toolToRemove.Key != null)
        {
            _tools.TryRemove(toolToRemove.Key, out _);
            _logger.LogInformation("Removed tool: {ToolName} (file deleted: {FilePath})", toolToRemove.Key, filePath);
        }
    }
}
