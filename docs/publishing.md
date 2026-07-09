# Publishing Checklist

Before making the repository public:

- Choose a license.
- Keep BepInEx, Unity, and game DLLs out of the repo.
- Keep game-specific profiles under `examples/` unless the game owner permits
  shipping them.
- Document that the bridge binds to `127.0.0.1` only.
- Tag the first public version after one real game loads the plugin and returns
  `/healthz` plus `/snapshot`.

Skipped: CI and release packaging. Add them after the first manual install works.
