using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Szechuan.McpCliBridge.Server.Domain;

namespace Szechuan.McpCliBridge.Server.Orchestration;

/// <summary>
/// Orchestrates script discovery, compilation, and execution via Roslyn.
/// Manages the ScriptState and provides compiled tools.
/// </summary>
public class ScriptOrchestrator
{
    private readonly ScriptDiscoverer _discoverer;
    private readonly ILogger<ScriptOrchestrator> _logger;
    private ScriptState<ScriptContext>? _scriptState;
    private ScriptContext? _scriptContext;
    private readonly object _stateLock = new();

    public ScriptOrchestrator(ScriptDiscoverer discoverer, ILogger<ScriptOrchestrator> logger)
    {
        _discoverer = discoverer ?? throw new ArgumentNullException(nameof(discoverer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the orchestrator by discovering and executing scripts.
    /// Fails loudly and halts if any script fails to compile.
    /// </summary>
    public async Task InitializeAsync(string[] scriptPatterns)
    {
        lock (_stateLock)
        {
            _scriptContext = new ScriptContext();
        }

        var scripts = _discoverer.DiscoverScripts(scriptPatterns);
        _logger.LogInformation("Discovered {Count} script(s) to execute", scripts.Count);

        foreach (var script in scripts)
        {
            await ExecuteScriptAsync(script);
        }

        _logger.LogInformation("Successfully loaded {Count} tool(s)", _scriptContext.Tools.Count);
    }

    /// <summary>
    /// Executes a single script and registers tools from it.
    /// Throws an exception on compilation failure (fatal startup error).
    /// </summary>
    private async Task ExecuteScriptAsync(FileInfo scriptFile)
    {
        try
        {
            var scriptContent = await File.ReadAllTextAsync(scriptFile.FullName);
            _logger.LogInformation("Loading script: {Script}", scriptFile.Name);

            // Create script state with globals
            var contextType = typeof(ScriptContext);
            var options = ScriptOptions.Default
                .WithReferences(typeof(object).Assembly, contextType.Assembly)
                .WithImports(
                    "System",
                    "System.Collections.Generic",
                    "System.Threading.Tasks",
                    contextType.Namespace!
                );

            lock (_stateLock)
            {
                if (_scriptState == null)
                {
                    _scriptState = CSharpScript.RunAsync<ScriptContext>(
                        scriptContent,
                        options,
                        _scriptContext).GetAwaiter().GetResult();
                }
                else
                {
                    _scriptState = _scriptState.ContinueWithAsync<ScriptContext>(
                        scriptContent,
                        options).GetAwaiter().GetResult();
                }

                _scriptContext = _scriptState.ReturnValue ?? _scriptContext;
            }

            _logger.LogInformation("Successfully executed script: {Script}", scriptFile.Name);
        }
        catch (CompilationErrorException ex)
        {
            var diagnostics = string.Join(Environment.NewLine,
                ex.Diagnostics.Select(d =>
                {
                    var line = d.Location.GetLineSpan().StartLinePosition.Line + 1;
                    return $"  at line {line}: {d.GetMessage()}";
                }));

            _logger.LogCritical(ex, "FATAL: Script compilation failed: {Script}\n{Diagnostics}", scriptFile.Name, diagnostics);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FATAL: Script execution failed: {Script}", scriptFile.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets the current list of registered tools.
    /// </summary>
    public IReadOnlyList<McpToolContainer> GetTools()
    {
        lock (_stateLock)
        {
            return _scriptContext?.GetTools() ?? [];
        }
    }

    /// <summary>
    /// Re-executes a single script for hot-reloading.
    /// On compilation failure, keeps the last good state and returns false.
    /// </summary>
    public async Task<bool> ReloadScriptAsync(FileInfo scriptFile)
    {
        lock (_stateLock)
        {
            if (_scriptContext == null)
            {
                _logger.LogWarning("Cannot reload script: context not initialized");
                return false;
            }
        }

        try
        {
            var scriptContent = await File.ReadAllTextAsync(scriptFile.FullName);
            _logger.LogInformation("Reloading script: {Script}", scriptFile.Name);

            var options = ScriptOptions.Default
                .WithReferences(typeof(object).Assembly)
                .WithImports(
                    "System",
                    "System.Collections.Generic",
                    "System.Threading.Tasks",
                    "Szechuan.McpCliBridge.Server.Domain");

            ScriptState<ScriptContext>? newState;
            ScriptContext? newContext;

            lock (_stateLock)
            {
                if (_scriptState == null)
                {
                    _logger.LogWarning("Cannot reload: script state is null");
                    return false;
                }

                newState = _scriptState.ContinueWithAsync<ScriptContext>(
                    scriptContent,
                    options).GetAwaiter().GetResult();

                newContext = newState.ReturnValue ?? _scriptContext;
            }

            // If we got here, update the state
            lock (_stateLock)
            {
                _scriptState = newState;
                _scriptContext = newContext;
            }

            _logger.LogInformation("Successfully reloaded script: {Script}", scriptFile.Name);
            return true;
        }
        catch (CompilationErrorException ex)
        {
            _logger.LogError(ex, "Script reload failed (keeping last good state): {Script}", scriptFile.Name);
            foreach (var diagnostic in ex.Diagnostics)
            {
                _logger.LogError("  {Message}", diagnostic.GetMessage());
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script reload failed (keeping last good state): {Script}", scriptFile.Name);
            return false;
        }
    }
}
