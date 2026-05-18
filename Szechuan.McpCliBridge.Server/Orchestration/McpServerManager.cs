using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Szechuan.McpCliBridge.Server.Domain;

namespace Szechuan.McpCliBridge.Server.Orchestration;

/// <summary>
/// Manages MCP server configuration, handler registration, and tool schema mapping.
/// </summary>
public class McpServerManager
{
    private readonly McpServer _server;
    private readonly ILogger<McpServerManager> _logger;
    private readonly ScriptOrchestrator _orchestrator;

    public McpServerManager(
        ILogger<McpServerManager> logger,
        ScriptOrchestrator orchestrator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));

        _server = new McpServer("mcp-cli-bridge");
    }

    /// <summary>
    /// Registers all tool handlers with the MCP server.
    /// Must be called before Start().
    /// </summary>
    public void RegisterToolHandlers()
    {
        var tools = _orchestrator.GetTools();
        _logger.LogInformation("Registering {Count} tools with MCP server", tools.Count);

        // Register tools/list handler
        _server.SetRequestHandler<Tool>(
            "tools/list",
            async (request, cancellationToken) =>
            {
                _logger.LogDebug("Received tools/list request");
                var currentTools = _orchestrator.GetTools();
                var toolList = currentTools.Select(MapToMcpTool).ToList();
                return new ToolListResult { Tools = toolList };
            });

        // Register tools/call handler
        _server.SetRequestHandler<ToolResult>(
            "tools/call",
            async (request, cancellationToken) =>
            {
                try
                {
                    _logger.LogDebug("Received tools/call request for tool: {ToolName}", request.Name);

                    var tool = _orchestrator.GetTools()
                        .FirstOrDefault(t => t.Name == request.Name);

                    if (tool == null)
                    {
                        _logger.LogWarning("Tool not found: {ToolName}", request.Name);
                        return new ToolResult
                        {
                            Content = new List<Content>
                            {
                                new TextContent { Text = $"Tool not found: {request.Name}" }
                            },
                            IsError = true
                        };
                    }

                    // Convert request arguments to dictionary
                    var args = new Dictionary<string, object?>();
                    if (request.Arguments is not null)
                    {
                        foreach (var kvp in request.Arguments)
                        {
                            args[kvp.Key] = kvp.Value;
                        }
                    }

                    // Execute the tool
                    var result = await tool.ExecuteAsync(args);

                    return new ToolResult
                    {
                        Content = new List<Content>
                        {
                            new TextContent { Text = result }
                        },
                        IsError = false
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool execution failed");
                    return new ToolResult
                    {
                        Content = new List<Content>
                        {
                            new TextContent { Text = $"Error: {ex.Message}" }
                        },
                        IsError = true
                    };
                }
            });

        _logger.LogInformation("Tool handlers registered successfully");
    }

    /// <summary>
    /// Maps a McpToolContainer to an MCP Tool schema.
    /// </summary>
    private Tool MapToMcpTool(McpToolContainer container)
    {
        var inputSchema = new Dictionary<string, object>
        {
            { "type", "object" },
            { "properties", BuildPropertySchema(container.Parameters) },
            { "required", container.Parameters.Where(p => p.Required).Select(p => p.Name).ToList() }
        };

        return new Tool
        {
            Name = container.Name,
            Description = container.Description,
            InputSchema = inputSchema
        };
    }

    /// <summary>
    /// Builds the JSON Schema properties for tool parameters.
    /// </summary>
    private Dictionary<string, object> BuildPropertySchema(List<McpParameter> parameters)
    {
        var properties = new Dictionary<string, object>();

        foreach (var param in parameters)
        {
            properties[param.Name] = new Dictionary<string, object>
            {
                { "type", MapDotNetTypeToJsonSchemaType(param.Type) },
                { "description", param.Description }
            };

            if (param.DefaultValue != null)
            {
                properties[param.Name]["default"] = param.DefaultValue;
            }
        }

        return properties;
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
    /// Gets the underlying MCP server instance.
    /// </summary>
    public McpServer GetServer()
    {
        return _server;
    }
}
