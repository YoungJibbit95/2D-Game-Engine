# Engine Inventory

Last updated: 2026-05-10

This file is a contributor-facing snapshot of the current project shape. Open work still lives in the checklist files.

## Tech Stack

| Area | Current Choice |
| --- | --- |
| Language | C# |
| Runtime | .NET 8, pinned through `global.json` |
| Client Framework | MonoGame DesktopGL |
| Core Math | `System.Numerics` |
| Data Format | JSON through `System.Text.Json` |
| Logging | Serilog console sink in the client |
| Tests | xUnit |
| Save Payloads | MessagePack-CSharp chunk payloads |
| Save Compression | K4os.Compression.LZ4 |
| Debug UI Foundation | ImGui.NET dependency present, custom debug overlays active |
| Scripting Foundation | MoonSharp dependency present for later Lua mod scripting |
| Worldgen Noise | Udun.FastNoiseLite |
| IDE Setup | Rider-ready solution, launch profile, and setup doc |

## Size Snapshot

The count excludes `bin`, `obj`, `.git`, and `.vs`.

| Area | Files | Lines |
| --- | ---: | ---: |
| `Game.Core` | 286 | 12,998 |
| `Game.Client` | 45 | 4,235 |
| `Game.Tests` | 75 | 6,582 |
| `Game.Data` | 57 | 1,397 |
| `Docs` | 8 | 616 |

| Extension | Files | Lines |
| --- | ---: | ---: |
| `.cs` | 402 | 23,752 |
| `.json` | 59 | 1,416 |
| `.md` | 9 | 769 |
| Project/solution/config files | 17 | 342 |

Total tracked workspace snapshot: 487 files, 26,279 lines.

## Core Engine Features

- Chunked tile world with finite vertical bounds and optional horizontally infinite X.
- Negative tile/chunk coordinate support through shared coordinate utilities.
- Runtime `TileInstance` data with tile id, wall id, liquid amount, light, and flags.
- Dirty chunk flags for save, mesh rebuild, and lighting work.
- Chunk metadata for active liquids, lit tiles, tile entity counts, and save ticks.
- Batch tile mutation through `World.ApplyTileEdits`.
- World region clamping that preserves infinite horizontal coordinates.
- Packed region-file chunk storage plus legacy loose chunk fallback.
- Coordinated save/load services for world, player, inventory, runtime entities, and tile entities.
- Session-level autosave and session-level load orchestration.
- Deterministic finite and streamed world generation.
- Profile-driven generation for dimensions, terrain, caves, ores, water pockets, trees, and vertical dimension bands.
- Pass-through mineable tree tiles for Terraria-style movement and mining.
- Chunk streaming planner and lifecycle service with load/generate/save/unload events.
- World simulation scheduler for dirty liquid, render, and light regions.
- Baseline liquid simulation with active-region stepping.
- Greyscale lighting with sunlight, point lights, and underground falloff.
- Core event bus with typed subscriptions and global observation.
- Bounded event journal for debugging, profiling, and future replay/replication.
- Data-driven registries for tiles, items, recipes, loot, biomes, entities, projectiles, status effects, spawns, sprites, and worldgen profiles.
- Mod/content-pack loader with base/mod merge and validation reports.
- Inventory, hotbar, cursor-item foundation, stack merge/split/swap, pickup logic, equipment, and stat calculation.
- Crafting query model with known recipes, station checks, ingredient checks, categories, and sort order.
- Combat health, damage info, invulnerability, contact damage, projectile damage, melee attacks, loot drops, and status effects.
- Entity manager with runtime id assignment and spatial query index.
- Shared world queries for raycasts, line of sight, shape queries, and tile flood fill.
- Core settings model with video, rendering, UI, audio, gameplay, world, input, and debug sections.
- Engine-neutral UI animation pipeline with clips, tracks, keyframes, curves, and a runtime player.
- Renderer-neutral UI toolkit with retained elements, free/stack/grid/scroll/tabs/splitter/dock layout, topmost hit-testing, modal layers, focus traversal, tooltip state, and cursor item interaction snapshots.

## Client Features

- MonoGame window/game loop with fixed-step update and variable rendering.
- Main menu with Singleplayer, planned splitscreen, planned multiplayer, settings, and exit.
- Escape no longer exits from the main menu by accident.
- Loading state for world/session preparation.
- Playing state with camera follow, tile/liquid rendering, player rendering, entities, lighting overlay, HUD, inventory overlay, pause menu, and debug console.
- Inventory overlay with hotbar/main slot widgets, core stack click rules, cursor-held stack drawing, shift-click quick move, and item tooltips for stats, effects, tags, and stack limits.
- Crafting overlay with known recipe list, selected recipe details, nearby station detection, ingredient availability, craft button, and shift-repeat crafting.
- Pause/settings overlay with tabs for gameplay, world, graphics, rendering, UI, debug, audio, keybinds, and system actions.
- Shared minimalist UI theme helper with dark surfaces, accent colors, hover/selected states, progress bars, and opacity controls.
- UI animation applied to menu and loading/pause surfaces.
- Runtime settings for theme, panel opacity, HUD opacity, animation speed, reduced motion, render cache budget, liquid opacity, streaming behavior, and debug metric visibility.
- Chunk render command cache with LRU-style budget trimming.
- Tile rendering can use real sprite assets when PNGs exist, or deterministic placeholders while assets are missing.

## Test Coverage Highlights

- Coordinate conversion, including negative coordinates.
- World tile get/set, chunk dirtiness, and save/load.
- Inventory and player inventory stack behavior.
- Crafting, item actions, mining/building, combat, projectiles, spawning, commands, settings, status effects, and world generation.
- Entity save/load and tile entity save/load.
- Coordinated session save/load round trips.
- UI animation track/player behavior.
- UI layout, hit-testing, modal layers, focus traversal, tooltips, and cursor interaction snapshots.

Current test count: 294 passing tests after the crafting UI and infinite station locator expansion.

## Current Engine Direction

The engine is now past the first playable prototype shell and is becoming a reusable Terraria-like 2D gamebuilding core. The most important next engine-grade steps are:

- Move more gameplay orchestration out of `PlayingState` into core simulation services.
- Add a real UI toolkit with focus, layout primitives, modal layering, tooltips, inventory widgets, and crafting screens.
- Upgrade rendering to atlas-backed batching, render targets, shader passes, particles, and animation clips for entities.
- Add RGB region-based lighting and dynamic lights attached to items, projectiles, entities, and furniture.
- Deepen worldgen with biome transitions, underground walls, larger cavern layers, lakes, structure spacing, chest rooms, and sampled infinite-world quality gates.
- Add save migrations, integrity reports, autosave rotation, and recovery diagnostics.
- Add content browser/editor tooling powered by the same data loaders as the runtime.
