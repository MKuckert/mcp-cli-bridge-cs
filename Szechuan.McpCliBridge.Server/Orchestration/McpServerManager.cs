using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Szechuan.McpCliBridge.Server.Domain;
using System.Text.Json;

namespace Szechuan.McpCliBridge.Server.Orchestration;

/// <summary>
/// Manages MCP server configuration and tool schema mapping.
/// </summary>
public class McpServerManager
{
    private readonly ILogger<McpServerManager> _logger;
    private readonly ScriptOrchestrator _orchestrator;
    private readonly McpServer _server;

    public McpServerManager(
        ILogger<McpServerManager> logger,
        ScriptOrchestrator orchestrator,
        McpServer server)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <summary>
    /// Initializes MCP tool handlers registration.
    /// </summary>
    public void RegisterToolHandlers()
    {
        var tools = _orchestrator.GetTools();
        _logger.LogInformation("Tool handlers registration initialized with {Count} tools", tools.Count);
    }

    /// <summary>
    /// Maps a McpToolContainer to an MCP Tool schema.
    /// </summary>
    public Tool MapToMcpTool(McpToolContainer container)
    {
        var properties = new Dictionary<string, object>();
        foreach (var param in container.Parameters)
        {
            var propSchema = new Dictionary<string, object>
            {
                { "type", MapDotNetTypeToJsonSchemaType(param.Type) },
                { "description", param.Description }
            };

            if (param.DefaultValue != null)
            {
                propSchema["default"] = param.DefaultValue;
            }

            properties[param.Name] = propSchema;
        }

        var inputSchema = new Dictionary<string, object>
        {
            { "type", "object" },
            { "properties", properties },
            { "required", container.Parameters.Where(p => p.Required).Select(p => p.Name).ToList() }
        };

        // Convert dictionary to JsonElement
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonString = JsonSerializer.Serialize(inputSchema, jsonOptions);
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

        return new Tool
        {
            Name = container.Name,
            Description = container.Description,
            InputSchema = jsonElement
        };
    }

    /// <summary>
    /// Gets all available tools from the orchestrator.
    /// </summary>
    public IReadOnlyList<McpToolContainer> GetTools()
    {
        return _orchestrator.GetTools();
    }

    /// <summary>
    /// Finds a tool by name.
    /// </summary>
    public McpToolContainer? FindTool(string name)
    {
        return _orchestrator.GetTools().FirstOrDefault(t => t.Name == name);
    }

    /// <summary>
    /// Maps .NET type names to JSON Schema type strings.
    /// </summary>
    private string MapDotNetTypeToJsonSchemaType(string dotNetType)
    {
        return dotNetType.ToLower() switch
        {
            "string" => "string",
            "int" => "integer",
            "long" => "integer",
            "double" => "number",
            "float" => "number",
            "bool" => "boolean",
            "boolean" => "boolean",
            _ => "string" // Default to string for unknown types
        };
    }

    /// <summary>
    /// Notifies the MCP client that the tool list has changed.
    /// </summary>
    public async Task NotifyToolsListChangedAsync()
    {
        try
        {
            _logger.LogInformation("Notifying clients of tool list change");
            await _server.SendNotificationAsync("tools/list_changed", null, cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify tool list change");
        }
    }
}
