# BepMCP

Runtime bridge for AI control of compiled desktop Unity games through BepInEx.

> Alpha: the BepInEx bridge and stdio MCP server work on a real IL2CPP game.

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
- Runtime PNG screenshots
- Windows keyboard and mouse sequences
- Main-thread action execution
- Explicit action allowlist
- JSON bridge bound to `127.0.0.1`

## Architecture

```text
AI client
  -> BepMCP stdio server
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
| `GET` | `/screenshot` | Capture the current frame as PNG |
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

## MCP Tools

The Windows stdio server exposes:

| Tool | Purpose |
| --- | --- |
| `unity_snapshot` | Read scene and entity metadata |
| `unity_screenshot` | Return the current frame as MCP image content |
| `unity_act` | Execute allowlisted Unity-level actions |
| `unity_input` | Send a bounded keyboard/mouse sequence |
| `unity_cancel` | Cancel an active input sequence |

Input steps support `key_down`, `key_up`, `key_tap`, `mouse_move`,
`mouse_click`, `scroll`, and `wait`.

```json
{
  "actions": [
    { "type": "key_down", "key": "W" },
    { "type": "wait", "ms": 500 },
    { "type": "key_up", "key": "W" }
  ],
  "requestId": "move-forward",
  "timeoutMs": 2000
}
```

Input is sent only after the server identifies and focuses the game window
reported by `/healthz`. Any keys left down are released on completion,
cancellation, timeout, or failure.

## Compatibility

| Runtime | Target | Status |
| --- | --- | --- |
| IL2CPP | BepInEx 6 | Verified in a desktop game |
| Mono | BepInEx 5 | Implementation present; build and real-game verification pending |

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
dotnet run --project src/BepMcp.Server/BepMcp.Server.csproj -- --self-test
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

Build the MCP server:

```powershell
dotnet build src/BepMcp.Server/BepMcp.Server.csproj
```

Configure an MCP client to launch the built DLL:

```json
{
  "mcpServers": {
    "bepmcp": {
      "command": "dotnet",
      "args": [
        "C:\\path\\to\\BepMcp.Server.dll"
      ],
      "env": {
        "BEPMCP_BRIDGE_URL": "http://127.0.0.1:8765/"
      }
    }
  }
}
```

## Safety

- Listens on IPv4 loopback only.
- Rejects unknown action verbs.
- Limits HTTP headers to 16 KB and request bodies to 1 MB on IL2CPP.
- Limits input sequences to 100 steps and 30 seconds.
- Serializes input sequences and verifies the foreground process.
- Releases held keys after cancellation, timeout, or failure.
- Does not provide raw method invocation or arbitrary code execution.
- Does not include game binaries or generated interop assemblies.

Only use BepMCP with games and environments where modification is permitted.

## Roadmap

1. Stable entity IDs and delta snapshots.
2. Per-game action profiles.
3. Unity UI text, bounds, and interactable metadata.
4. Optional gamepad input for games without keyboard controls.
5. Release packaging for Mono and IL2CPP.

## Project Layout

```text
src/UnityMcp.Bridge/                    Shared wire models
src/UnityMcp.Core/                      Scheduler, profiles, action registry
src/BepMcp.Server/                      Windows stdio MCP server
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
- `/screenshot` returned a nonblank PNG frame
- `/act` validated an allowlisted action
- MCP completed a real mouse click and observed the changed frame
- `unity_cancel` stopped an active input sequence

The current source version is `0.2.0-alpha`.
