# BepMCP

Runtime bridge for AI control of compiled desktop Unity games through BepInEx.

> Alpha: the BepInEx bridge works on a real IL2CPP game. The external MCP
> server is not implemented yet.

BepMCP targets shipped games, not the Unity Editor. It injects a small,
localhost-only bridge into a game and keeps MCP protocol handling in a separate
process.

## Why BepMCP?

Most Unity MCP projects automate the Unity Editor and require the original
project. BepMCP is for desktop games where only the compiled game and a
compatible BepInEx installation are available.

- BepInEx 6 IL2CPP plugin, verified on Unity 2021.3.45f2
- BepInEx 5 Mono plugin target
- Runtime scene snapshots
- Main-thread action execution
- Explicit action allowlist
- JSON bridge bound to `127.0.0.1`

## Architecture

```text
AI client
  -> MCP server (planned)
  -> localhost JSON bridge
  -> BepInEx plugin
  -> Unity runtime
```

The game plugin does not expose arbitrary reflection, code execution, component
mutation, or file access. Games can add explicit actions through a profile or
plugin code as support is implemented.

## Current API

The bridge currently provides:

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/healthz` | Check bridge availability |
| `GET` | `/snapshot` | Read the active scene and up to 200 GameObjects |
| `POST` | `/act` | Validate or execute allowlisted actions |

The first action is `set_time_scale`.

```json
{
  "schema": "unity-mcp.bridge/1",
  "requestId": "example-1",
  "type": "act",
  "payload": {
    "dryRun": true,
    "actions": [
      {
        "verb": "set_time_scale",
        "args": {
          "timeScale": 1
        }
      }
    ]
  }
}
```

All Unity reads and mutations are scheduled onto the Unity main thread.

## Compatibility

| Runtime | Target | Status |
| --- | --- | --- |
| IL2CPP | BepInEx 6 | Verified in a desktop game |
| Mono | BepInEx 5 | Builds; real-game verification pending |

BepInEx and Unity assemblies are referenced from a local game installation.
They are not included in this repository.

## Build

Requirements:

- .NET SDK
- A game with the matching BepInEx runtime
- Generated `BepInEx/interop` assemblies for IL2CPP

See [docs/building.md](docs/building.md) for build commands and required
reference paths.

Run the shared check:

```powershell
dotnet run --project tests/UnityMcp.Core.Tests/UnityMcp.Core.Tests.csproj
```

## Install

Build the plugin for the game's scripting backend, then copy its output to:

```text
<GameRoot>/BepInEx/plugins/
```

For BepInEx 6 IL2CPP, copy:

```text
UnityMcp.Plugin.BepInEx6.Il2Cpp.dll
UnityMcp.Plugin.BepInEx6.Il2Cpp.deps.json
UnityMcp.Bridge.dll
UnityMcp.Core.dll
```

Start the game and test the default endpoint:

```powershell
Invoke-WebRequest http://127.0.0.1:8765/healthz
```

The port can be changed in:

```text
BepInEx/config/dev.unitymcp.bridge.cfg
```

## Safety

- Listens on IPv4 loopback only.
- Rejects unknown action verbs.
- Limits HTTP headers to 16 KB and request bodies to 1 MB on IL2CPP.
- Does not provide raw method invocation or arbitrary code execution.
- Does not include game binaries or generated interop assemblies.

Only use BepMCP with games and environments where modification is permitted.

## Roadmap

1. External MCP server with `unity_snapshot`, `unity_act`, and `unity_cancel`.
2. Per-game action profiles.
3. Screenshot capture for vision models.
4. Additional input and UI actions.
5. Release packaging for Mono and IL2CPP.

## Project Layout

```text
src/UnityMcp.Bridge/                    Shared wire models
src/UnityMcp.Core/                      Scheduler, profiles, action registry
src/UnityMcp.Plugin.BepInEx5.Mono/      Mono plugin
src/UnityMcp.Plugin.BepInEx6.Il2Cpp/    IL2CPP plugin
tests/UnityMcp.Core.Tests/              Minimal shared checks
examples/profiles/                      Example game profiles
docs/                                   Build and publishing notes
```

## Project Status

The first IL2CPP smoke test passed:

- Plugin loaded through BepInEx 6
- `/healthz` returned HTTP 200
- `/snapshot` returned 200 scene entities
- `/act` validated an allowlisted action

This repository is ready for an `0.1.0-alpha` source release. It is not yet a
complete MCP server.
