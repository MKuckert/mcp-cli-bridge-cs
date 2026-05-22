using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ModelContextProtocol.Server;
using Szechuan.McpCliBridge.Server.Cli;
using Szechuan.McpCliBridge.Server.Domain;
using Szechuan.McpCliBridge.Server.Orchestration;

// Parse and validate CLI arguments BEFORE creating the host
var cliOptions = CliBootstrapper.ParseAndValidate(args);
if (cliOptions == null)
{
    Environment.Exit(1);
}

// Create and configure the host with MCP server
var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging
    .AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Register CLI options as singleton for dependency injection
builder.Services.AddSingleton(cliOptions);

// Register orchestration components
builder.Services.AddSingleton(sp =>
{
    var scriptsDir = new DirectoryInfo(cliOptions.TargetDirectory.FullName);
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var discoverer = new ScriptDiscoverer(scriptsDir, loggerFactory.CreateLogger<ScriptDiscoverer>());
    return discoverer;
});

builder.Services.AddSingleton(sp =>
{
    var discoverer = sp.GetRequiredService<ScriptDiscoverer>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new ScriptOrchestrator(discoverer, loggerFactory.CreateLogger<ScriptOrchestrator>());
});

builder.Services.AddSingleton(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var orchestrator = sp.GetRequiredService<ScriptOrchestrator>();
    var server = sp.GetRequiredService<McpServer>();
    return new McpServerManager(
        loggerFactory.CreateLogger<McpServerManager>(),
        orchestrator,
        server);
});

// Register the MCP server with stdio transport
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

var host = builder.Build();

// Get services
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var orchestrator = host.Services.GetRequiredService<ScriptOrchestrator>();
var serverManager = host.Services.GetRequiredService<McpServerManager>();

try
{
    logger.LogInformation("Starting MCP CLI Bridge");
    logger.LogInformation("Target directory: {Directory}", cliOptions.TargetDirectory.FullName);
    logger.LogInformation("Script patterns: {Patterns}", string.Join(", ", cliOptions.ScriptPatterns));
    logger.LogInformation("Watch mode: {WatchMode}", cliOptions.WatchMode);

    // Initialize scripts: discover and compile
    // This will fatally halt if any script fails to compile
    logger.LogInformation("Initializing script orchestrator...");
    await orchestrator.InitializeAsync(cliOptions.ScriptPatterns);

    var toolCount = orchestrator.GetTools().Count;
    logger.LogInformation("Successfully loaded {Count} tool(s)", toolCount);

    // Register MCP tool handlers
    logger.LogInformation("Registering MCP tool handlers...");
    serverManager.RegisterToolHandlers();

    // Configure file watcher if watch mode is enabled
    if (cliOptions.WatchMode)
    {
        logger.LogInformation("Starting file system watcher for hot-reload...");
        var scriptsDir = new DirectoryInfo(cliOptions.TargetDirectory.FullName);
        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        var watcher = new ScriptWatcher(
            scriptsDir,
            orchestrator,
            loggerFactory.CreateLogger<ScriptWatcher>());

        // Connect watcher to MCP notifications
        watcher.OnToolsChangedAsync = async () =>
            await serverManager.NotifyToolsListChangedAsync();

        watcher.Start();
        logger.LogInformation("File watcher started");
    }

    logger.LogInformation("MCP server starting on stdio transport...");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Fatal error during initialization");
    Environment.Exit(1);
}

// Block here until client disconnects
await host.RunAsync();
