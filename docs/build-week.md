# OpenAI Build Week

## Track

Developer Tools.

## Project

BepMCP lets Codex safely inspect and control compiled Unity desktop
applications through BepInEx without the original Unity project or Editor. It
targets runtime QA, debugging, accessibility prototyping, mod development, and
agent-based testing rather than arbitrary code execution.

## Codex And GPT-5.6

Codex was used to inspect the original bridge alpha, implement the stdio MCP
server, add bounded Windows input, capture runtime screenshots and semantic UI
metadata, design stable entity IDs, build the standalone simulator, and run
real IL2CPP smoke tests. GPT-5.6 helped prioritize the security boundary:
localhost only, explicit action allowlists, bounded input, main-thread Unity
access, cancellation, and no arbitrary reflection invocation.

## Reproducible Demo

1. Start `BepMcp.Simulator`.
2. Connect `BepMcp.Server` to `http://127.0.0.1:8765/`.
3. Call `unity_snapshot` and locate Player, Door, Treasure, and status UI.
4. Call `unity_act` with `interact` targeting `treasure`.
5. Call `unity_wait` for `Treasure collected` and inspect the returned snapshot.
6. Show the same observe-act-observe loop in a permitted IL2CPP desktop game.

The simulator covers judge reproducibility; the game demo proves the BepInEx
runtime integration.
