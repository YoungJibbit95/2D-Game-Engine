# TerrariaLike

This repository starts a small MonoGame-based 2D sandbox action-adventure engine.

## Projects

- `Game.Core`: engine and gameplay logic without MonoGame references.
- `Game.Client`: MonoGame DesktopGL client, game loop, states, rendering shell.
- `Game.Tests`: unit tests for core systems.

## Current Status

Milestone 1 and the first core world tasks are scaffolded:

- .NET 8 SDK-style solution.
- MonoGame DesktopGL client.
- `MainGame`, `IGameState`, `GameStateManager`, and `PlayingState`.
- Fixed-step update runner in `Game.Core`.
- FPS/debug text rendered without a content-pipeline font.
- Coordinate primitives: `TilePos`, `ChunkPos`, `RectI`, and `CoordinateUtils`.
- Chunked world model: `TileInstance`, `Chunk`, `World`, tile dirty flags, and solid checks.
- Deterministic `SimpleWorldGenerator` with FastNoiseLite-backed surface noise.
- JSON-driven tile definitions and registry.
- JSON-driven item definitions and registry.
- Inventory stack rules, split, merge, swap, and removal behavior.
- Tilemap rendering with visible chunk iteration.
- Camera follow.
- Player entity with keyboard movement, jumping, gravity, and tile collision.
- First HUD pass with hotbar slots and health bar.
- World save/load with readable metadata, MessagePack chunk payloads, and LZ4 compression.
- Greyscale lighting with sunlight, point lights, and render overlay.
- Spatial grid for reusable area/entity queries.
- Game content database loader for base data roots.
- Recipe registry and crafting core.
- Loot table registry and deterministic loot rolling support.
- Biome registry and biome map baseline.
- Mod/content-pack loader with `mod.json`, override reporting, and tile numeric-id conflict detection.
- Combat health component with damage types, damage info, and invulnerability timing.
- Projectile and entity definition registries in the content pipeline.
- JSON player save/load with inventory slot persistence.
- Autotile 4-bit neighbor mask logic.
- Runtime projectile entities with lifetime, gravity, tile collision, and factory creation.
- Runtime enemy entities with health, tile physics, AI behavior, and factory creation from data.
- Entity manager runtime id assignment and spatial query index.
- JSON entity save/load for enemies and projectiles.
- Dropped item entity and pickup system.
- Mining and building core systems with reach checks, tool power, drops, placement validation, and item consumption.
- Combat resolver for projectile-vs-enemy hits with loot drops.
- WorldTime day/night model.
- Cross-registry content reference validation with loader report errors.
- Data-driven spawn rules and SpawnSystem using biome, day/night, vertical range, ground checks, chance, and active caps.
- Modular advanced world generation pipeline with terrain, caves, ore veins, and trees.
- Core gameplay event bus with events for mining, pickups, projectile hits, deaths, and command execution.
- Engine-neutral debug command backend with parser, registry, dispatcher, and built-in `/give`, `/time`, and `/spawn` commands.
- Core settings service with JSON save/load validation for video, audio, gameplay, and debug settings.
- Client settings path resolution and startup application of resolution, fullscreen, and VSync settings.
- Runtime debug console overlay in the client, opened with F10, wired to inventory, world time, and entity spawning.
- `Game.Data` is copied to the client output so runtime content loading works from builds.
- Rider-ready shared `Game.Client` run configuration, `launchSettings.json`, and `global.json`.
- Rider build fallback for machines where Rider picks a `.NET 10` user-local SDK while the project targets `net8.0`.
- World generation result metadata with forest biome map and safe surface spawn point.
- Player health now uses the combat `HealthComponent`, including damage invulnerability and knockback.
- Runtime contact damage resolves enemy/player collisions and publishes player damage events.
- Dropped item entities are saved and loaded alongside enemies and projectiles.
- Spawn scheduler handles player-distance spawn bands, interval attempts, active caps, and despawning far enemies.
- `GameSimulation` centralizes a core tick for time, player update, entity update, projectile combat, contact damage, pickups, and spawning.
- `PlayerInventory` separates hotbar and main inventory while preserving stack merge/remove behavior.
- `MeleeAttackSystem` resolves player melee hitboxes, cooldowns, enemy damage, death events, and loot drops.
- `PlayerRespawnSystem` restores dead players at the world metadata spawn point after a delay.
- Chunk streaming planner computes load/unload boundaries around visible tile areas and keeps dirty chunks loaded.
- Structure templates and placer support reusable worldgen structures, with an initial surface shelter generation step.
- Advanced world generation now includes underground liquid pockets using tile liquid data.
- `PlayerItemUseSystem` routes selected hotbar items into mining, building, and melee actions.
- `WorldSkySystem` evaluates day/night sky color and sunlight intensity for lighting/rendering integration.

## Rider

Open `TerrariaLike.sln` in JetBrains Rider and run the shared `Game.Client` configuration. See `Docs/RIDER_SETUP.md` for the exact setup and runtime controls.

## Dependencies

- Game framework: MonoGame DesktopGL.
- Runtime: .NET 8.
- Data: `System.Text.Json`.
- Logging: Serilog with console sink in the client.
- Tests: xUnit.
- Math: `System.Numerics` in core.
- Debug UI foundation: ImGui.NET.
- Save foundations: MessagePack-CSharp and K4os.Compression.LZ4.
- Scripting foundation: MoonSharp.
- Worldgen noise: `Udun.FastNoiseLite`.

YAML is intentionally not included yet; base game and mod data start with JSON.

## Local Notes

This repository targets `net8.0`. If Rider selects `C:\Users\Administrator\.dotnet` and reports `NETSDK1127`, keep the shared run configuration and ensure `C:\Program Files\dotnet` contains the .NET 8 SDK/runtime. Then run:

```powershell
dotnet restore
dotnet test
dotnet run --project Game.Client
```
