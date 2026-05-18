using Microsoft.Extensions.Logging;

namespace Szechuan.McpCliBridge.Server.Orchestration;

/// <summary>
/// Monitors the scripts directory for changes and triggers hot-reloads.
/// Debounces rapid file saves and integrates with MCP notifications.
/// </summary>
public class ScriptWatcher : IDisposable
{
    private readonly DirectoryInfo _scriptsDirectory;
    private readonly ScriptOrchestrator _orchestrator;
    private readonly ILogger<ScriptWatcher> _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, DateTime> _lastChangeTime;
    private readonly object _debounceLocker = new();
    private const int DebounceDelayMs = 500;

    /// <summary>
    /// Fired when tools are successfully reloaded.
    /// </summary>
    public event Action? ToolsChanged;

    /// <summary>
    /// Handler for MCP server notification of tool changes.
    /// </summary>
    public Func<Task>? OnToolsChangedAsync { get; set; }

    public ScriptWatcher(
        DirectoryInfo scriptsDirectory,
        ScriptOrchestrator orchestrator,
        ILogger<ScriptWatcher> logger)
    {
        _scriptsDirectory = scriptsDirectory ?? throw new ArgumentNullException(nameof(scriptsDirectory));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lastChangeTime = new Dictionary<string, DateTime>();

        _watcher = new FileSystemWatcher(_scriptsDirectory.FullName)
        {
            Filter = "*.csx",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = false // Start disabled; caller must call Start()
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Error += OnWatcherError;
    }

    /// <summary>
    /// Starts watching for changes.
    /// </summary>
    public void Start()
    {
        _logger.LogInformation("Script watcher started for directory: {Directory}", _scriptsDirectory.FullName);
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Stops watching for changes.
    /// </summary>
    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
        _logger.LogInformation("Script watcher stopped");
    }

    /// <summary>
    /// Handles file change events with debouncing.
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath == null)
        {
            return;
        }

        lock (_debounceLocker)
        {
            // Check if we've already processed this file recently (debounce)
            if (_lastChangeTime.TryGetValue(e.FullPath, out var lastTime))
            {
                var timeSinceLastChange = DateTime.UtcNow - lastTime;
                if (timeSinceLastChange.TotalMilliseconds < DebounceDelayMs)
                {
                    _logger.LogDebug("Ignoring duplicate change event for {File} (debounce)", e.Name);
                    return;
                }
            }

            _lastChangeTime[e.FullPath] = DateTime.UtcNow;
        }

        // Schedule the reload after debounce delay
        _ = Task.Run(async () => await ReloadScriptAfterDelayAsync(e.FullPath, e.Name ?? "unknown"));
    }

    /// <summary>
    /// Handles file deletion events.
    /// </summary>
    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Script deleted: {Script}", e.Name);
        ToolsChanged?.Invoke();
        _ = OnToolsChangedAsync?.Invoke();
    }

    /// <summary>
    /// Handles watcher errors.
    /// </summary>
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        if (e.GetException() is Exception ex)
        {
            _logger.LogError(ex, "File system watcher error");
        }
    }

    /// <summary>
    /// Reloads a script after the debounce delay.
    /// </summary>
    private async Task ReloadScriptAfterDelayAsync(string filePath, string fileName)
    {
        try
        {
            // Wait for debounce period
            await Task.Delay(DebounceDelayMs);

            // Check if file still exists (it might have been deleted)
            if (!File.Exists(filePath))
            {
                _logger.LogInformation("File no longer exists, skipping reload: {File}", fileName);
                return;
            }

            _logger.LogInformation("Reloading script (debounce completed): {Script}", fileName);

            var scriptFile = new FileInfo(filePath);
            var reloadSuccess = await _orchestrator.ReloadScriptAsync(scriptFile);

            if (reloadSuccess)
            {
                _logger.LogInformation("Script reloaded successfully: {Script}", fileName);
                ToolsChanged?.Invoke();
                _ = OnToolsChangedAsync?.Invoke();
            }
            else
            {
                _logger.LogWarning("Script reload failed, keeping last good state: {Script}", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during script reload for {Script}", fileName);
        }
    }

    /// <summary>
    /// Disposes the watcher and releases resources.
    /// </summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        GC.SuppressFinalize(this);
    }
}
