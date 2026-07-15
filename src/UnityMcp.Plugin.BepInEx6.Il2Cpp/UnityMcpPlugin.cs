using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityMcp.Core;

namespace UnityMcp.Plugin.BepInEx6.Il2Cpp;

[BepInPlugin("dev.unitymcp.bridge", "Unity MCP Bridge", "0.3.0")]
public sealed class UnityMcpPlugin : BasePlugin
{
    private readonly MainThreadScheduler _scheduler = new MainThreadScheduler();
    private BridgeHttpServer? _server;

    public override void Load()
    {
        ConfigEntry<int> port = Config.Bind("Bridge", "Port", 8765, "Localhost bridge HTTP port.");
        ConfigEntry<int> maxEntities = Config.Bind("Snapshot", "MaxEntities", 200, "Maximum GameObjects returned by /snapshot.");

        BridgeRunner.Scheduler = _scheduler;
        AddComponent<BridgeRunner>();

        _server = new BridgeHttpServer(port.Value, _scheduler, Log, () => maxEntities.Value);
        _server.Start();
        Log.LogInfo("Unity MCP Bridge listening on http://127.0.0.1:" + port.Value);
    }

    public override bool Unload()
    {
        _server?.Dispose();
        return true;
    }
}

public sealed class BridgeRunner : MonoBehaviour
{
    public static MainThreadScheduler? Scheduler;

    public BridgeRunner(nint pointer) : base(pointer)
    {
    }

    private void Awake()
    {
        Object.DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        Scheduler?.RunPending(32);
    }
}
