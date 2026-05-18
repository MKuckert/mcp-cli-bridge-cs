namespace Szechuan.McpCliBridge.Server.Domain;

/// <summary>
/// The global host object exposed to C# scripts.
/// Provides the DSL for defining tools and executing shell commands.
/// </summary>
public class McpScriptHost
{
    private string? _currentToolName;
    private string? _currentToolDescription;
    private List<McpParameter>? _currentToolParameters;
    private Func<Dictionary<string, object?>, Task<string>>? _currentToolExecute;

    /// <summary>
    /// Registers the name of the current tool being defined.
    /// </summary>
    public void Name(string name)
    {
        _currentToolName = name;
    }

    /// <summary>
    /// Registers the description of the current tool being defined.
    /// </summary>
    public void Description(string description)
    {
        _currentToolDescription = description;
    }

    /// <summary>
    /// Adds a parameter to the current tool being defined.
    /// </summary>
    public void Param(
        string name,
        string description,
        string type = "string",
        bool required = false,
        object? defaultValue = null)
    {
        _currentToolParameters ??= [];
        _currentToolParameters.Add(new McpParameter
        {
            Name = name,
            Description = description,
            Type = type,
            Required = required,
            DefaultValue = defaultValue
        });
    }

    /// <summary>
    /// Registers the execution logic for the current tool.
    /// </summary>
    public void OnExecute(Func<Dictionary<string, object?>, Task<string>> execute)
    {
        _currentToolExecute = execute;
    }

    /// <summary>
    /// Builds and returns the configured tool container.
    /// Resets internal state for the next tool definition.
    /// </summary>
    public McpToolContainer? BuildAndReset()
    {
        if (string.IsNullOrEmpty(_currentToolName) ||
            string.IsNullOrEmpty(_currentToolDescription) ||
            _currentToolExecute == null)
        {
            return null;
        }

        var tool = new McpToolContainer
        {
            Name = _currentToolName,
            Description = _currentToolDescription,
            Parameters = _currentToolParameters ?? [],
            ExecuteAsync = _currentToolExecute
        };

        Reset();
        return tool;
    }

    /// <summary>
    /// Resets the host state for the next tool definition.
    /// </summary>
    private void Reset()
    {
        _currentToolName = null;
        _currentToolDescription = null;
        _currentToolParameters = null;
        _currentToolExecute = null;
    }

    /// <summary>
    /// Executes a shell command with the given command line and optional timeout.
    /// </summary>
    public async Task<string> RunShell(string command, int timeoutMs = 30000)
    {
        throw new NotImplementedException("RunShell will be implemented in Task 5");
    }
}
