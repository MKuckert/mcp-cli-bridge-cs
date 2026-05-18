namespace Szechuan.McpCliBridge.Server.Domain;

/// <summary>
/// Represents a parameter for an MCP tool.
/// </summary>
public class McpParameter
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Type { get; set; }
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
}
