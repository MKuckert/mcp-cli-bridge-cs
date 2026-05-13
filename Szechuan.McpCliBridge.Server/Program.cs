using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Szechuan.McpCliBridge.Server;

var dirOption = new Option<string>(
    name: "--dir",
    description: "The target working directory (CWD) where tools will be executed.") { IsRequired = true };

var scriptsOption = new Option<string>(
    name: "--scripts",
    description: "The folder containing .csx tool definitions.") { IsRequired = true };

var watchOption = new Option<bool>(
    name: "--watch",
    description: "Enables a FileSystemWatcher for live tool updates.");

var ipOption = new Option<string>(
    name: "--ip",
    description: "Host binding for http transport mode.",
    getDefaultValue: () => "127.0.0.1");

var portOption = new Option<int>(
    name: "--port",
    description: "Port for http transport mode.",
    getDefaultValue: () => 5000);

var rootCommand = new RootCommand("MCP CLI-Bridge");
rootCommand.AddOption(dirOption);
rootCommand.AddOption(scriptsOption);
rootCommand.AddOption(watchOption);
rootCommand.AddOption(ipOption);
rootCommand.AddOption(portOption);

rootCommand.SetHandler(async (dir, scripts, watch, ip, port) =>
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    // 1. Discovery
    var absoluteScriptsPath = Path.GetFullPath(scripts);
    var absoluteTargetPath = Path.GetFullPath(dir);
    
    var registry = new ToolRegistry(absoluteScriptsPath, absoluteTargetPath, builder.Logging.CreateLogger<ToolRegistry>());
    await registry.DiscoverToolsAsync();

    // 2. Hard Switch
    Directory.SetCurrentDirectory(absoluteTargetPath);

    builder.Services.AddSingleton(registry);
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolHandler<DynamicMcpServer>();

    var host = builder.Build();
    
    if (watch)
    {
        StartWatcher(registry, absoluteScriptsPath, host.Services.GetRequiredService<ILogger<ToolRegistry>>());
    }

    await host.RunAsync();
}, dirOption, scriptsOption, watchOption, ipOption, portOption);

await rootCommand.InvokeAsync(args);

void StartWatcher(ToolRegistry registry, string path, ILogger logger)
{
    var watcher = new FileSystemWatcher(path, "*.csx");
    var timer = new Dictionary<string, Timer>();

    watcher.Changed += (s, e) => Debounce(e.FullPath);
    watcher.Created += (s, e) => Debounce(e.FullPath);
    watcher.Deleted += (s, e) => 
    {
        // Simple registry doesn't support easy removal by path yet, 
        // but re-discovery would handle it if we cleared or if we use FilePath as key.
        // For now, let's just log.
        logger.LogInformation("File deleted: {Path}. Registry update not fully implemented for deletions.", e.FullPath);
    };

    watcher.EnableRaisingEvents = true;
    logger.LogInformation("Watching {Path} for changes...", path);

    void Debounce(string filePath)
    {
        if (timer.TryGetValue(filePath, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        timer[filePath] = new Timer(async _ =>
        {
            logger.LogInformation("File changed, reloading: {Path}", filePath);
            await registry.LoadToolAsync(filePath);
            // Note: In a real implementation, we should also send notifications/tools/list_changed
        }, null, 500, Timeout.Infinite);
    }
}
