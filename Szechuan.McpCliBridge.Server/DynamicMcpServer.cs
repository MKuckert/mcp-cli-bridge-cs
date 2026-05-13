using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Szechuan.McpCliBridge.Server;

public class DynamicMcpServer : IToolHandler
{
    private readonly ToolRegistry _registry;
    private readonly ILogger<DynamicMcpServer> _logger;

    public DynamicMcpServer(ToolRegistry registry, ILogger<DynamicMcpServer> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public Task<IEnumerable<Tool>> ListToolsAsync(CancellationToken cancellationToken)
    {
        var tools = _registry.Tools.Select(t => new Tool
        {
            Name = t.Host.ToolName,
            Description = t.Host.ToolDescription,
            InputSchema = GenerateSchema(t.Host)
        });

        return Task.FromResult(tools);
    }

    public async Task<CallToolResult> CallToolAsync(string name, JsonObject? arguments, CancellationToken cancellationToken)
    {
        var container = _registry.GetTool(name);
        if (container == null)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = { new TextContent { Text = $"Tool '{name}' not found." } }
            };
        }

        try
        {
            // Inject parameters
            if (arguments != null)
            {
                foreach (var param in container.Host.Parameters)
                {
                    if (arguments.TryGetPropertyValue(param.Name, out var node) && node != null)
                    {
                        var value = node.Deserialize(param.Type);
                        if (value == null && param.Type == typeof(string)) value = string.Empty;
                        container.Host.SetParamValue(param.Name, value);
                    }
                }
            }

            if (container.Host.ExecutionLogic == null)
            {
                throw new InvalidOperationException("No execution logic defined for tool.");
            }

            var result = await container.Host.ExecutionLogic();
            return new CallToolResult
            {
                Content = { new TextContent { Text = result } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", name);
            return new CallToolResult
            {
                IsError = true,
                Content = { new TextContent { Text = $"Error: {ex.Message}\n{ex.StackTrace}" } }
            };
        }
    }


    private JsonObject GenerateSchema(McpScriptHost host)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var param in host.Parameters)
        {
            var prop = new JsonObject
            {
                ["type"] = GetJsonType(param.Type),
                ["description"] = param.Description
            };

            if (param.DefaultValue != null)
            {
                prop["default"] = JsonSerializer.SerializeToNode(param.DefaultValue);
            }

            properties[param.Name] = prop;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private string GetJsonType(Type type)
    {
        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return "number";
        if (type == typeof(bool))
            return "boolean";
        return "string";
    }
}
