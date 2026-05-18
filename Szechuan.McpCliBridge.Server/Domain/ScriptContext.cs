namespace Szechuan.McpCliBridge.Server.Domain;

/// <summary>
/// The script context that holds the globals object and manages tool collection.
/// This is passed to Roslyn for script execution.
/// </summary>
public class ScriptContext
{
    /// <summary>
    /// The host object exposed to scripts via the Roslyn globals.
    /// </summary>
    public McpScriptHost Host { get; }

    /// <summary>
    /// Collection of tools discovered and built from scripts.
    /// </summary>
    public List<McpToolContainer> Tools { get; }

    public ScriptContext()
    {
        Host = new McpScriptHost();
        Tools = [];
    }

    /// <summary>
    /// Adds a tool to the collection after script execution.
    /// </summary>
    public void RegisterTool(McpToolContainer tool)
    {
        Tools.Add(tool);
    }

    /// <summary>
    /// Retrieves all registered tools.
    /// </summary>
    public IReadOnlyList<McpToolContainer> GetTools()
    {
        return Tools.AsReadOnly();
    }
}
