using System.Collections.Generic;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace Szechuan.McpCliBridge.Server;

public class ParamValue<T>
{
    private readonly McpParameter _parameter;
    public T Value { get; set; }

    public ParamValue(McpParameter parameter)
    {
        _parameter = parameter;
        Value = (T)parameter.DefaultValue;
    }
}

public record McpParameter(string Name, Type Type, object DefaultValue, string Description);

public record ShellResult(int ExitCode, string StdOut, string StdErr);

public class McpScriptHost
{
    // Metadata extracted during discovery
    public string ToolName { get; private set; } = string.Empty;
    public string ToolDescription { get; private set; } = string.Empty;
    public List<McpParameter> Parameters { get; } = new();
    private readonly Dictionary<string, object> _paramValues = new();
    public Func<Task<string>>? ExecutionLogic { get; private set; }
    public string TargetWorkDir { get; init; } = string.Empty;

    // DSL Methods used inside .csx
    public void Name(string name) => ToolName = name;
    public void Description(string desc) => ToolDescription = desc;

    public McpScriptHost Clone()
    {
        var clone = new McpScriptHost
        {
            ToolName = ToolName,
            ToolDescription = ToolDescription,
            ExecutionLogic = ExecutionLogic,
            TargetWorkDir = TargetWorkDir
        };
        foreach (var p in Parameters)
        {
            clone.Param(p.Name, p.DefaultValue, p.Description);
        }
        return clone;
    }

    public ParamValue<T> Param<T>(string name, T defaultValue, string description)
    {
        var p = new McpParameter(name, typeof(T), defaultValue!, description);
        if (!Parameters.Any(existing => existing.Name == name))
        {
            Parameters.Add(p);
        }
        var pv = new ParamValue<T>(p);
        _paramValues[name] = pv;
        return pv;
    }

    public void SetParamValue(string name, object? value)
    {
        if (_paramValues.TryGetValue(name, out var pv))
        {
            var prop = pv.GetType().GetProperty("Value");
            prop?.SetValue(pv, value);
        }
    }

    public void OnExecute(Func<Task<string>> logic) => ExecutionLogic = logic;

    // Shell Abstraction using CliWrap
    public async Task<ShellResult> RunShell(string cmd, params string[] args)
    {
        var result = await Cli.Wrap(cmd)
            .WithArguments(args)
            .WithWorkingDirectory(string.IsNullOrEmpty(TargetWorkDir) ? Directory.GetCurrentDirectory() : TargetWorkDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        return new ShellResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
