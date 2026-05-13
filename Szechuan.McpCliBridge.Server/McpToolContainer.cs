using Microsoft.CodeAnalysis.Scripting;

namespace Szechuan.McpCliBridge.Server;

public class McpToolContainer
{
    public required McpScriptHost Host { get; init; }
    public required ScriptState CompiledScriptState { get; init; }
}
