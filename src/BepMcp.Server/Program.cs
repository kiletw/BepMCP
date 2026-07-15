using System;
using System.Linq;
using BepMcp.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    WindowsInput.SelfTest();
    UnityBridgeClient.SelfTest();
    Console.WriteLine("BepMcp.Server self-test ok");
    return;
}

var bridgeUrl = Environment.GetEnvironmentVariable("BEPMCP_BRIDGE_URL")
    ?? "http://127.0.0.1:8765/";

if (args.Contains("--bridge-self-test", StringComparer.Ordinal))
{
    using var bridge = new UnityBridgeClient(new Uri(bridgeUrl));
    await bridge.HealthAsync(default);
    var snapshot = await bridge.WaitForSnapshotAsync("Simulator", false, 2000, 100, default);
    if (!snapshot.Contains("bepmcp-simulator", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Bridge snapshot is not the BepMCP simulator.");
    }

    Console.WriteLine("BepMcp.Server bridge self-test ok");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(new UnityBridgeClient(new Uri(bridgeUrl)));
builder.Services.AddSingleton<WindowsInput>();
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "bepmcp",
            Title = "BepMCP",
            Version = "0.3.0-alpha"
        };
        options.ServerInstructions =
            "Observe with unity_screenshot or unity_snapshot before acting. " +
            "Use unity_wait to verify expected state changes. " +
            "Use bounded unity_input sequences and always release held keys.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
