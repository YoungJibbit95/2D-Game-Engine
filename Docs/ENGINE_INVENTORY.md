# Engine Inventory

Last updated: 2026-07-15

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
| `Game.Core` | 599 | 45,760 text lines / 45,693 C# |
| `Game.Client` | 98 | 17,769 text lines / 17,608 C# |
| `Game.Tests` | 177 | 25,031 text lines / 24,780 C# |
| `Game.Benchmarks` | 8 | 1,627 text lines / 1,216 C# |
| `Game.Data` | 422 | 58,349 text lines plus 205 PNGs |
| `Website` | 52 | 2,051 text lines plus 41 PNG copies |
| `Docs` | 27 | 2,853 nonblank text lines |

| Extension | Files | Lines |
| --- | ---: | ---: |
| `.cs` | 868 | 89,297 |
| `.json` | 212 | 56,576 |
| `.md` | 34 | 3,171 nonblank lines |
| `.png` | 246 | binary; 205 are `Game.Data` assets and 41 are Website copies |
| Website `.html/.css/.js/.mjs` | 5 | 1,161 |

Snapshot method excludes `bin`, `obj`, `.git` and ignored benchmark/smoke artifacts. Counts can change as the dirty shared workspace evolves; current code/build/test evidence remains authoritative.

## Core Engine Features

- Chunked tile world with finite vertical bounds and optional horizontally infinite X.
- Negative tile/chunk coordinate support through shared coordinate utilities.
- Runtime `TileInstance` data with tile id, wall id, liquid amount, light, and flags.
- Dirty chunk flags for save, mesh rebuild, and lighting work.
- Chunk metadata for active liquids, lit tiles, tile entity counts, and save ticks.
- Batch tile mutation through `World.ApplyTileEdits`.
- World region clamping that preserves infinite horizontal coordinates.
- Packed region-file chunk storage plus legacy loose chunk fallback.
- Coordinated save/load services for world, player, inventory, equipment, exact active-effect durations, character appearance, runtime entities, tile entities, and farm plots.
- Session-level autosave and session-level load orchestration.
- Deterministic finite and streamed world generation.
- Profile-driven generation for dimensions, terrain, cave walkers, large caverns/connectors, ores, shaped surface lakes, cave pools, underground walls/cleanup, trees, vertical dimension bands, regional biomes and row/legend structure templates.
- Pass-through mineable tree tiles for Terraria-style movement and mining.
- Bounded background chunk streaming with immutable request snapshots, cancellation/session tokens, classified failures, exponential retry/backoff, terminal suppression/reset, negative-X load/generate/decode, recovery-safe dirty saves, operation/time/decoded-byte apply budgets, oversize progress guarantees, lifecycle events and queue/retry/byte/timing telemetry.
- World simulation scheduler for dirty liquid, render, and light regions.
- Baseline liquid simulation with active-region stepping.
- Dirty-region greyscale simulation light with sunlight, tile emission, underground falloff and stale-shadow repair after mining/placement.
- Client presentation light with budgeted tile-aware sun/point ray casts, colored torches, AO, penumbra, bloom and cave residual light. It is 2D ray casting, not hardware raytracing.
- Allocation-free steady-state core event publication with typed subscriptions, global observation, bounded journal tooling and benchmark coverage. A separate fixed-capacity gameplay feedback router translates authoritative mining/build/combat/projectile/death/drop/pickup/craft/status/world-event outcomes into reusable visual and audio commands with drop/drain telemetry.
- Bounded event journal for debugging, profiling, and future replay/replication.
- Data-driven registries for tiles, items, crops, maps, dialogues, shops, startup profiles, recipes, loot, biomes, entities, projectiles, status effects, spawns, sprites, runtime animations, characters, finite/regional worldgen, structure plans, soundscapes and world events.
- Mod/content-pack loader with base/mod merge and validation reports.
- Game project manifest support through `yjse.game.json`, project/content/mod path resolution, project-scoped save/settings roots, and external game repo loading via `YJSE_GAME_ROOT` or `YJSE_GAME_DATA`.
- Core session bootstrapper that loads project content, resolves startup/world profile/settings, resumes saves, creates starter inventory, generates finite or infinite worlds, preloads spawn chunks, and returns a reusable `LoadedGameSession`.
- Inventory, hotbar and cursor-item transactions with favorite protection, revision tracking, sorting, compaction, querying, rarity/value metadata, live pickup logic, lossless equipment replacement, stat calculation, and player-save v3 migration.
- Crafting query model with known recipes, station checks, ingredient checks, categories, and sort order.
- Farming foundation for Stardew-like games: crop definitions, seed lookup, farm plots, tilling, watering, planting, seasonal growth, harvesting, regrow crops, selected-item action routing, and farm plot save/load.
- Topdown movement controller for RPG, life-sim, adventure, and farm games using shared tile collision.
- Topdown map foundation with JSON map definitions, tile layers, collision layers, objects, interactables, spawn points, warps, registry validation, mod merge support, runtime map queries, map sessions, map-specific pixel movement, facing, interaction targeting, runtime object state, door/gate passability, object action resolution, interaction events, and warp transition application.
- Dialogue foundation with JSON graph definitions, node/option validation, sessions, deterministic advancement, option selection, and explicit failure reasons.
- Shop/economy foundation with JSON shop definitions, stock, sell prices, per-entry currency overrides, inventory-backed buy/sell transactions, and explicit failure reasons.
- Startup profile foundation with JSON definitions, starter inventory targeting, selected hotbar slot, default world profile/startup map references, validation, and inventory creation service.
- Sprite asset audit foundation that checks manifest file existence, PNG header dimensions, generation brief path/size matches, missing briefs, and complete autotile mask coverage.
- Generated/base PNG assets now cover core terrain, tree tiles, workbench, starter blocks, ores, materials/tools/weapons, projectiles, player layers, critters/enemies, foliage/furniture and biome backgrounds. Wave 05 contributes 24 assets with 122 explicit source-rectangle frames for Amber Grove/Twilight Marsh, four autotiles, two object sheets, six items/tools, four actors and six UI assets.
- Combat health, damage info, invulnerability, directional guard, stamina, parry, guard break, mitigation and combined contact/projectile resolution. The tick-native attack sequencer adds startup/active/recovery/cooldown, buffering, lockouts, combo/cancel windows, resource metadata, multiple timed swept shapes and bounded command/event/hit buffers with 0 B steady-state tests.
- Projectile runtime contracts for gravity, drag, homing, collision decisions, lifetime, exact-once hits, friendly fire, pierce and bounce.
- Entity manager with runtime id assignment and spatial query index.
- Activity-source spawning with deterministic candidate/rule streams, viewport exclusion, habitat/ground/liquid/collision checks, local/region/global caps and protected despawn.
- Friendly/hostile AI with perception memory, flock, perch, flee, chase, investigate, return-home, day profiles and fixed telemetry snapshots.
- Shared world queries for raycasts, line of sight, shape queries, and tile flood fill.
- Active transactional building through `BuildingPlacementTransactionService`, with optimistic/authoritative inventory commits, negative-X coverage and typed validation/commit results.
- Core settings model with video, rendering, UI, audio, gameplay, world, input, and debug sections.
- Engine-neutral UI animation pipeline with clips, tracks, keyframes, curves, and a runtime player.
- Fixed-tick gameplay animation pipeline with source-rectangle tracks, loop/ping-pong/once playback, event cursors, blends, action locks, layered state machines, character rigs, sockets and legacy adapters.
- Deterministic advanced world-event definitions, phases, modifiers, cooldowns, executor and bounded journal. `LivingWorldRuntime` owns scheduled and exact-once action triggers; atomic snapshot/cooldown/journal persistence and rare/quantity loot routing are active.
- Versioned deterministic replay diagnostics with bounded input-frame capture, fixed-step delta, player/item requests, periodic state hashes, 64 MiB JSON safety limit and typed first-divergence reports. `GameSimulation` exposes explicit start/snapshot/stop capture APIs.
- Renderer-neutral audio mixer, category buses, crossfade envelopes, soundscape definitions and command planning; the client supplies MonoGame voice/device adapters.
- Runtime sprite animation clips, loop modes, JSON loader, sprite animator, character definitions, character animation state resolver, and first character editor session foundation.
- Renderer-neutral UI toolkit with retained elements, free/stack/grid/scroll/tabs/splitter/dock layout, topmost hit-testing, modal layers, focus traversal, tooltip state, and cursor item interaction snapshots.
- Engine-neutral performance profiler with timestamp/allocation scopes, rolling averages, peaks, budgets, and slowest-pass snapshots.

## Client Features

- MonoGame window/game loop with fixed-step update and variable rendering.
- YjsE-branded main menu with Singleplayer, planned splitscreen, planned multiplayer, settings, and exit.
- World-select and create-world screens with saved-world metadata listing, typed world names, seed entry, random seeds, and resume through the loading flow.
- Escape no longer exits from the main menu by accident.
- Loading state for world/session preparation.
- Playing state with camera follow, tile/liquid rendering, Wave 04 layered player rigs, bounded animated entity commands, tile-aware 2D presentation light, reflections, Wave 04/05 parallax, particles, soundscape routing, HUD, overlays and developer console.
- Playing state can now route hoe, watering can, and seed use into farm plots, draw placeholder farm plots/crops, advance farm growth on new days, and autosave farm plot state.
- Inventory overlay with hotbar/main slot widgets, core stack click rules, cursor-held stack drawing, shift-click quick move, and item tooltips for stats, effects, tags, and stack limits.
- Crafting overlay with search/category/visibility filters, recipe pins, nearby station detection, ingredient/output planning, quantity controls, atomic batch/maximum crafting, typed events, and revision-cached refresh.
- Pause/settings overlay with tabs for gameplay, world, graphics, rendering, UI effects, accessibility, debug, audio, keybinds, and system actions.
- Mouse-first interaction with release-inside clicks, pointer capture, drag sliders, dropdowns, segmented controls, toggles, scrolling, delayed tooltips and keyboard/gamepad fallback.
- Modern pixel UI renderer with bounded rounded geometry, gradients, glow, shadows, progress/guard bars and real scene-capture backdrop blur.
- UI animation applied to menu, loading and pause/settings surfaces.
- Runtime settings for theme, panel opacity, HUD opacity, animation speed, reduced motion, render cache budget, liquid opacity, streaming operations/concurrency/apply/retry budgets and debug metric visibility.
- Chunk render command cache with LRU-style budget trimming plus presentation pass, ray/sample, reflection-surface, audio voice and entity-visual telemetry.
- Tile/entity/item/projectile rendering can use real sprite assets when PNGs exist, or deterministic placeholders/debug rectangles while assets are missing.
- Mining progress/crack feedback, action failure messages, item cooldown bars, buff/debuff timers, performance metrics, event journal, and streaming backlog overlays.
- Saved equipment, effects, and character appearance are restored through `LoadedGameSession` into active gameplay and written back by autosave.

## Test Coverage Highlights

- Coordinate conversion, including negative coordinates.
- World tile get/set, chunk dirtiness, and save/load.
- Inventory and player inventory stack behavior.
- Crafting, farming, maps, topdown map sessions/movement/interactions/actions/transitions, dialogues, shops, item actions, transactional mining/building, combat, projectiles, spawning, commands, settings, status effects, topdown movement, and world generation.
- Game project manifest loading, path resolution, fallback loose-content roots, and project content loading.
- Startup profile loading, starter inventory exact-slot behavior, fallback auto-placement, and reference validation.
- Core session bootstrapping for new sessions and existing save resume without client orchestration.
- Sprite asset audit behavior for present files, missing files, brief mismatches, dimension mismatches, and incomplete autotile definitions.
- Entity save/load and tile entity save/load.
- Coordinated session save/load round trips, including farm plot persistence.
- UI animation track/player behavior.
- UI layout, release-inside hit-testing, pointer capture, drag controls, modal layers, keyboard/gamepad focus, tooltips, accessibility and cursor interaction snapshots.
- Fixed-tick layered animation, character rigs, guard snapshot adaptation and bounded entity visual preparation.
- Activity-source spawning, friendly/hostile AI memory/state transitions and 200-entity spawn/AI soaks.
- Tile-aware ray masks including mined-occluder repair, quality/pass budgets, reflections, ambient particles, audio mixer/soundscape and active world-event executor/throttle contracts.

Current post-integration local test count: Debug 995/995 and Release 995/995. Format, warn-as-error build, performance, strict asset/preview and seven isolated client-smoke gates pass; hosted Windows/Ubuntu results remain pending.

## Current Engine Direction

The engine is now past the first playable prototype shell and is becoming a reusable multi-genre 2D gamebuilding core for Terraria-like, Stardew-like, RPG, action-adventure, and tool-driven sandbox games. The most important next engine-grade steps are:

- Make every item-use weapon consume reusable attack phases, combo windows and swept hit shapes; add authored attack/death event timelines.
- Continue migrating overlays onto reusable renderer-neutral UI models and add automated click-path smokes.
- Upgrade rendering to atlas-backed batching and compiled shader passes; add draw-call, texture-switch, GPU-time and 1080p/1440p measurements.
- Wire sprite asset audits into CI, debug commands, packaging, and editor tooling.
- Add persisted RGB region light values and dynamic lights attached to items, projectiles, entities and furniture beyond the current colored presentation layer.
- Deepen worldgen with biome transitions, biome-specific materials, structure spacing, chest rooms, seed retry/preview tooling, and sampled infinite-world quality gates.
- Continue moving toward stable engine package names, a multi-session host API and a physical standalone game repository that references the engine.
- Reduce the current 6,237 B calibrated fixed-tick allocation using representative/200-entity phase evidence and bounded frame snapshot/query reuse.
- Add richer crop rendering, client topdown map rendering/input integration, weather/rain watering, shop/dialogue UI, shipping bins, NPC schedules, and relationship data.
- Add save migrations, integrity reports, autosave rotation, and recovery diagnostics.
- Add content browser/editor tooling powered by the same data loaders as the runtime.
