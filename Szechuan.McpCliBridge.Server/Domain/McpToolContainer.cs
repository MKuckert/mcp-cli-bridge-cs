namespace Szechuan.McpCliBridge.Server.Domain;

/// <summary>
/// Represents a tool container with metadata and execution logic.
/// </summary>
public class McpToolContainer
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required List<McpParameter> Parameters { get; set; }
    public required Func<Dictionary<string, object?>, Task<string>> ExecuteAsync { get; set; }
}
