# Building

## Shared Checks

```powershell
dotnet run --project tests/UnityMcp.Core.Tests/UnityMcp.Core.Tests.csproj
```

## BepInEx 5 Mono Plugin

Set reference paths to a game that already has BepInEx installed:

```powershell
dotnet build src/UnityMcp.Plugin.BepInEx5.Mono/UnityMcp.Plugin.BepInEx5.Mono.csproj `
  -p:BepInExRoot="C:\Games\Example\BepInEx" `
  -p:UnityManagedPath="C:\Games\Example\Example_Data\Managed"
```

Output:

```text
src/UnityMcp.Plugin.BepInEx5.Mono/bin/Debug/UnityMcp.Plugin.BepInEx5.Mono.dll
```

Copy the DLL to:

```text
<GameRoot>/BepInEx/plugins/
```

## BepInEx 6 IL2CPP Plugin

Set reference paths to a game that already has BepInEx 6 IL2CPP installed and
has generated `BepInEx/interop` assemblies:

```powershell
dotnet build src/UnityMcp.Plugin.BepInEx6.Il2Cpp/UnityMcp.Plugin.BepInEx6.Il2Cpp.csproj `
  -p:BepInExRoot="C:\Games\Example\BepInEx" `
  -p:UnityInteropPath="C:\Games\Example\BepInEx\interop"
```

Copy these files to `<GameRoot>/BepInEx/plugins/`:

```text
UnityMcp.Plugin.BepInEx6.Il2Cpp.dll
UnityMcp.Plugin.BepInEx6.Il2Cpp.deps.json
UnityMcp.Bridge.dll
UnityMcp.Core.dll
```

The plugin starts:

```text
http://127.0.0.1:8765/healthz
http://127.0.0.1:8765/snapshot
http://127.0.0.1:8765/act
```

Minimal action request:

```json
{
  "schema": "unity-mcp.bridge/1",
  "requestId": "dev-1",
  "type": "act",
  "payload": {
    "dryRun": true,
    "actions": [
      {
        "verb": "set_time_scale",
        "args": { "timeScale": 1 }
      }
    ]
  }
}
```

Skipped: vendoring BepInEx or Unity DLLs. Keep public repos free of game files
and third-party binaries.
