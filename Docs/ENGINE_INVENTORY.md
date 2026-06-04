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
| IDE Setup | Rider-ready `YjsE.sln`, launch profile, and setup doc |

## Size Snapshot

The count excludes `bin`, `obj`, `.git`, and `.vs`.

| Area | Files | Lines |
| --- | ---: | ---: |
| `Game.Core` | 365 | 17,033 |
| `Game.Client` | 47 | 4,686 |
| `Game.Tests` | 86 | 8,558 |
| `Game.Data` | 66 | 1,768 |
| `Docs` | 9 | 754 |

| Extension | Files | Lines |
| --- | ---: | ---: |
| `.cs` | 494 | 30,214 |
| `.json` | 69 | 1,801 |
| `.md` | 11 | 939 |
| Project/solution/config files | 7 | 108 |

Total tracked workspace snapshot: 588 files, 33,141 lines.

## Core Engine Features

- Chunked tile world with finite vertical bounds and optional horizontally infinite X.
- Negative tile/chunk coordinate support through shared coordinate utilities.
- Runtime `TileInstance` data with tile id, wall id, liquid amount, light, and flags.
- Dirty chunk flags for save, mesh rebuild, and lighting work.
- Chunk metadata for active liquids, lit tiles, tile entity counts, and save ticks.
- Batch tile mutation through `World.ApplyTileEdits`.
- World region clamping that preserves infinite horizontal coordinates.
- Packed region-file chunk storage plus legacy loose chunk fallback.
- Coordinated save/load services for world, player, inventory, runtime entities, tile entities, and farm plots.
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
- Data-driven registries for tiles, items, crops, maps, dialogues, shops, startup profiles, recipes, loot, biomes, entities, projectiles, status effects, spawns, sprites, and worldgen profiles.
- Mod/content-pack loader with base/mod merge and validation reports.
- Game project manifest support through `yjse.game.json`, project/content/mod path resolution, project-scoped save/settings roots, and external game repo loading via `YJSE_GAME_ROOT` or `YJSE_GAME_DATA`.
- Core session bootstrapper that loads project content, resolves startup/world profile/settings, resumes saves, creates starter inventory, generates finite or infinite worlds, preloads spawn chunks, and returns a reusable `LoadedGameSession`.
- Inventory, hotbar, cursor-item foundation, stack merge/split/swap, pickup logic, equipment, and stat calculation.
- Crafting query model with known recipes, station checks, ingredient checks, categories, and sort order.
- Farming foundation for Stardew-like games: crop definitions, seed lookup, farm plots, tilling, watering, planting, seasonal growth, harvesting, regrow crops, selected-item action routing, and farm plot save/load.
- Topdown movement controller for RPG, life-sim, adventure, and farm games using shared tile collision.
- Topdown map foundation with JSON map definitions, tile layers, collision layers, objects, interactables, spawn points, warps, registry validation, mod merge support, runtime map queries, map sessions, map-specific pixel movement, facing, interaction targeting, runtime object state, door/gate passability, object action resolution, interaction events, and warp transition application.
- Dialogue foundation with JSON graph definitions, node/option validation, sessions, deterministic advancement, option selection, and explicit failure reasons.
- Shop/economy foundation with JSON shop definitions, stock, sell prices, per-entry currency overrides, inventory-backed buy/sell transactions, and explicit failure reasons.
- Startup profile foundation with JSON definitions, starter inventory targeting, selected hotbar slot, default world profile/startup map references, validation, and inventory creation service.
- Combat health, damage info, invulnerability, contact damage, projectile damage, melee attacks, loot drops, and status effects.
- Entity manager with runtime id assignment and spatial query index.
- Shared world queries for raycasts, line of sight, shape queries, and tile flood fill.
- Core settings model with video, rendering, UI, audio, gameplay, world, input, and debug sections.
- Engine-neutral UI animation pipeline with clips, tracks, keyframes, curves, and a runtime player.
- Renderer-neutral UI toolkit with retained elements, free/stack/grid/scroll/tabs/splitter/dock layout, topmost hit-testing, modal layers, focus traversal, tooltip state, and cursor item interaction snapshots.

## Client Features

- MonoGame window/game loop with fixed-step update and variable rendering.
- YjsE-branded main menu with Singleplayer, planned splitscreen, planned multiplayer, settings, and exit.
- World-select and create-world screens with saved-world metadata listing, typed world names, seed entry, random seeds, and resume through the loading flow.
- Escape no longer exits from the main menu by accident.
- Loading state for world/session preparation.
- Playing state with camera follow, tile/liquid rendering, player rendering, entities, lighting overlay, HUD, inventory overlay, pause menu, and debug console.
- Playing state can now route hoe, watering can, and seed use into farm plots, draw placeholder farm plots/crops, advance farm growth on new days, and autosave farm plot state.
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
- Crafting, farming, maps, topdown map sessions/movement/interactions/actions/transitions, dialogues, shops, item actions, mining/building, combat, projectiles, spawning, commands, settings, status effects, topdown movement, and world generation.
- Game project manifest loading, path resolution, fallback loose-content roots, and project content loading.
- Startup profile loading, starter inventory exact-slot behavior, fallback auto-placement, and reference validation.
- Core session bootstrapping for new sessions and existing save resume without client orchestration.
- Entity save/load and tile entity save/load.
- Coordinated session save/load round trips, including farm plot persistence.
- UI animation track/player behavior.
- UI layout, hit-testing, modal layers, focus traversal, tooltips, and cursor interaction snapshots.

Current test count: 347 passing tests after the core session bootstrap foundation.

## Current Engine Direction

The engine is now past the first playable prototype shell and is becoming a reusable multi-genre 2D gamebuilding core for Terraria-like, Stardew-like, RPG, action-adventure, and tool-driven sandbox games. The most important next engine-grade steps are:

- Move more frame orchestration out of `PlayingState` into core simulation/session services.
- Wire the renderer-neutral UI toolkit into more client screens and add reusable renderer widgets for panels, buttons, labels, slots, lists, tabs, splitters, and windows.
- Upgrade rendering to atlas-backed batching, render targets, shader passes, particles, and animation clips for entities.
- Add RGB region-based lighting and dynamic lights attached to items, projectiles, entities, and furniture.
- Deepen worldgen with biome transitions, underground walls, larger cavern layers, lakes, structure spacing, chest rooms, and sampled infinite-world quality gates.
- Continue moving toward stable engine package names, a multi-session host API, and a physical standalone game repository that references the engine.
- Add richer crop rendering, client topdown map rendering/input integration, weather/rain watering, shop/dialogue UI, shipping bins, NPC schedules, and relationship data.
- Add save migrations, integrity reports, autosave rotation, and recovery diagnostics.
- Add content browser/editor tooling powered by the same data loaders as the runtime.
