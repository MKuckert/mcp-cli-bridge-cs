# PLAN.md: MCP CLI-Bridge Implementation

## 1. Project Overview

The **MCP CLI-Bridge** is an MCP (Model Context Protocol) server designed to dynamically load C# scripts (`.csx`) and expose them as structured tools to LLMs. It allows controlled execution of CLI programs within a specific target directory, supporting hot-reloading and robust parameter mapping.

## 2. CLI Interface & Execution Flow

The server application must handle the following command-line arguments:

* `--dir <path>`: The target working directory (CWD) where tools will be executed.
* `--scripts <path>`: The folder containing `.csx` tool definitions.
* `--watch`: (Flag) Enables a `FileSystemWatcher` for live tool updates.
* `--ip <address>`: Host binding for http transport mode (Default: `127.0.0.1`).
* `--port <number>`: Port for http transport mode (Default: `5000`).

**Bootstrapping Sequence:**

1. **Start:** Parse arguments.
2. **Discovery:** Scan the `--scripts` folder and perform a "Discovery Run" on all `.csx` files while still in the original execution directory.
3. **Hard Switch:** Change the process CWD to the path provided in `--dir`.
4. **Serve:** Initialize the MCP server (Stdio or http transport, depending on `--ip` and `--port` flags).

## 3. Core Architecture & Interfaces

### A. The Script Host (`McpScriptHost.cs`)

The "Globals" object injected into the Roslyn scripting environment. It defines the DSL.

```csharp
public class McpScriptHost {
    // Metadata extracted during discovery
    public string ToolName { get; private set; }
    public string ToolDescription { get; private set; }
    public List<McpParameter> Parameters { get; } = new();
    public Func<Task<string>> ExecutionLogic { get; private set; }
    public string TargetWorkDir { get; init; }

    // DSL Methods used inside .csx
    public void Name(string name) => ToolName = name;
    public void Description(string desc) => ToolDescription = desc;
    
    public ParamValue<T> Param<T>(string name, T defaultValue, string description) {
        var p = new McpParameter(name, typeof(T), defaultValue, description);
        Parameters.Add(p);
        return new ParamValue<T>(p); // Wrapper to hold runtime values
    }

    public void OnExecute(Func<Task<string>> logic) => ExecutionLogic = logic;

    // Shell Abstraction using CliWrap
    public async Task<ShellResult> RunShell(string cmd, params string[] args) {
        // Must handle stdout/stderr and return a ShellResult object
    }
}

```

### B. Tool Registry & Container

Encapsulates a loaded tool and its compiled state.

```csharp
public record McpParameter(string Name, Type Type, object DefaultValue, string Description);

public class McpToolContainer {
    public McpScriptHost Host { get; init; }
    public Script CompiledScript { get; init; }
    
    // Generates the JSON Schema required for the MCP 'tools/list' response
    public object GetJsonSchema() { ... } 
}

```

## 4. Technical Phases

### Phase 1: Discovery Engine

* **File Scanning:** Identify all `*.csx` files in the script directory.
* **Discovery Execution:** Use `CSharpScript.RunAsync(code, globals: host)` to execute the script once.
* **Validation:** A script is only registered as a tool if `Name()` and `OnExecute()` were successfully called during the discovery run.
* **Isolation:** If a script fails to compile or run during discovery, catch the exception, log it to `Console.Error`, and skip the file.

### Phase 2: Runtime Execution

* **Mapping:** When a `tools/call` request arrives, locate the `McpToolContainer` by name.
* **Parameter Injection:** Update the `Value` properties of the `ParamValue<T>` objects inside the host using the arguments provided by the LLM.
* **Execution:** Invoke the `ExecutionLogic` delegate.
* **Error Handling:** Catch runtime exceptions. Return them to the MCP client with `isError: true` and include the stack trace for LLM troubleshooting.

### Phase 3: Hot-Reloading (Optional `--watch`)

* **Watcher:** Implement `FileSystemWatcher` on the script directory.
* **Debouncing:** Use a `System.Threading.Timer` (500ms delay) to prevent multiple triggers from rapid file-saves.
* **Updates:** On change, re-run the Discovery phase for the specific file. If successful, update the registry and send a `notifications/tools/list_changed` notification to the MCP client.

### Phase 4: CLI Integration

* **CliWrap:** Use `CliWrap` for all shell executions to ensure safety and avoid command injection.
* **Streaming:** Implement `IProgress<string>` within the tool methods. Use it to send real-time `stdout` lines as MCP progress notifications to the client UI.

## 5. Reference DSL Usage (`cmake.csx`)

```csharp
Name("cmake_prepare");
Description("Configures the CMake project.");

var type = Param("config", "Debug", "Build configuration (Debug/Release)");

OnExecute(async () => {
    var result = await RunShell("cmake", "-B", "build", $"-DCMAKE_BUILD_TYPE={type.Value}");
    
    if (result.ExitCode != 0)
        return $"Config failed: {result.StdErr}";
        
    return $"Config successful: {result.StdOut}";
});

```

## 6. Security & Stability

* **Process Integrity:** Scripts run in-process. Ensure they cannot accidentally terminate the host (e.g., catching `Environment.Exit`).
* **Timeouts:** Implement a default timeout (e.g., 5-10 minutes) for CLI processes to prevent zombie builds.
* **Path Safety:** Since the server performs a `SetCurrentDirectory`, ensure all scripts use relative paths to remain within the "sandbox".
