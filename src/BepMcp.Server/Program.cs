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
    Console.WriteLine("BepMcp.Server self-test ok");
    return;
}

var bridgeUrl = Environment.GetEnvironmentVariable("BEPMCP_BRIDGE_URL")
    ?? "http://127.0.0.1:8765/";

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
            Version = "0.2.0-alpha"
        };
        options.ServerInstructions =
            "Observe with unity_screenshot or unity_snapshot before acting. " +
            "Use bounded unity_input sequences and always release held keys.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
