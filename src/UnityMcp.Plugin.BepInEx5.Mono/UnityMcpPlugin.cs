using BepInEx;
using BepInEx.Configuration;
using UnityMcp.Core;

namespace UnityMcp.Plugin.BepInEx5.Mono;

[BepInPlugin("dev.unitymcp.bridge", "Unity MCP Bridge", "0.1.0")]
public sealed class UnityMcpPlugin : BaseUnityPlugin
{
    private readonly MainThreadScheduler _scheduler = new MainThreadScheduler();
    private BridgeHttpServer? _server;
    private ConfigEntry<int>? _port;
    private ConfigEntry<int>? _maxEntities;

    private void Awake()
    {
        _port = Config.Bind("Bridge", "Port", 8765, "Localhost bridge HTTP port.");
        _maxEntities = Config.Bind("Snapshot", "MaxEntities", 200, "Maximum GameObjects returned by /snapshot.");

        _server = new BridgeHttpServer(_port.Value, _scheduler, Logger, () => _maxEntities.Value);
        _server.Start();
        Logger.LogInfo("Unity MCP Bridge listening on http://127.0.0.1:" + _port.Value);
    }

    private void Update()
    {
        _scheduler.RunPending(32);
    }

    private void OnDestroy()
    {
        _server?.Dispose();
    }
}
