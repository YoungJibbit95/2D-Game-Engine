# YjsE

<p align="center">
  <img src="https://komarev.com/ghpvc/?username=YoungJibbit95-2D-Game-Engine&label=repository%20views&color=7aa2f7&style=flat" alt="Repository views" />
  <img src="https://img.shields.io/github/last-commit/YoungJibbit95/2D-Game-Engine?style=flat&logo=git&color=7dcfff&labelColor=1a1b27" alt="Last commit" />
  <img src="https://img.shields.io/github/stars/YoungJibbit95/2D-Game-Engine?style=flat&logo=github&color=e0af68&labelColor=1a1b27" alt="GitHub stars" />
  <img src="https://img.shields.io/github/issues/YoungJibbit95/2D-Game-Engine?style=flat&logo=github&color=f7768e&labelColor=1a1b27" alt="Open issues" />
  <img src="https://img.shields.io/github/repo-size/YoungJibbit95/2D-Game-Engine?style=flat&logo=github&color=bb9af7&labelColor=1a1b27" alt="Repository size" />
</p>

<p align="center">
    YoungJibbit's Engine is a small MonoGame-based 2D sandbox action-adventure engine.
</p>
## Projects

- `Game.Core`: reusable engine and gameplay systems without MonoGame references.
- `Game.Client`: MonoGame DesktopGL host/client, game loop, states, rendering shell.
- `Game.Tests`: unit tests for core systems.
- `Game.Data`: sample/dev game content pack used to validate the engine locally.

The engine and game content are now separated by a `yjse.game.json` manifest. A future game can live in its own repository and point this client at that repo with `YJSE_GAME_ROOT`. See `Docs/ENGINE_GAME_SEPARATION.md`.

## Current Status

The engine is now a playable YjsE prototype shell with a growing reusable 2D sandbox core:

- .NET 8 SDK-style solution.
- MonoGame DesktopGL client.
- `MainGame`, `IGameState`, `GameStateManager`, and `PlayingState`.
- Fixed-step update runner in `Game.Core`.
- FPS/debug text rendered without a content-pipeline font.
- Coordinate primitives: `TilePos`, `ChunkPos`, `RectI`, and `CoordinateUtils`.
- Chunked world model: `TileInstance`, `Chunk`, `World`, tile dirty flags, and solid checks.
- Chunk metadata tracks active liquid tiles, lit tiles, tile entities, and save ticks for streaming/save/debug decisions.
- Deterministic `SimpleWorldGenerator` with FastNoiseLite-backed surface noise.
- JSON-driven tile definitions and registry.
- JSON-driven item definitions and registry.
- JSON-driven crop definitions and registry for Stardew-like farming loops.
- Inventory stack rules, atomic transfers, protected favorites, split/merge/swap, sorting, compaction, querying, rarity/value metadata, and player-save v3 migration.
- Inventory cursor interaction foundation with left-click pick/place/merge/swap and right-click split/place-one rules.
- In-game inventory overlay with hotbar/main slots, cursor-held stack rendering, shift-click quick move, and item stat/effect/tag tooltips.
- In-game crafting overlay with search/category/visibility filters, recipe pinning, result details, station/ingredient/output planning, quantity controls, atomic batch craft, and craft-maximum.
- Tilemap rendering with visible chunk iteration.
- Camera follow.
- Player entity with keyboard movement, jumping, gravity, and tile collision.
- Topdown movement controller for Stardew-like, Zelda-like, RPG, and life-sim games using the shared tile collision model.
- Topdown map registry with JSON maps, layers, objects, collision, spawn points, warps, and query services for Stardew-like/RPG games.
- First HUD pass with hotbar slots and health bar.
- World save/load with readable metadata, MessagePack chunk payloads, and LZ4 compression.
- Packed chunk region-file storage is available for streamed worlds while legacy loose chunk files remain loadable.
- Coordinated save/autosave service writes world, player, runtime entities, and tile entities through one engine API.
- Coordinated session loading restores world, player, inventory, runtime entities, and tile entities through the matching engine API.
- Greyscale lighting with sunlight, point lights, render overlay, underground falloff tuning, and configurable ambient floor.
- Spatial grid for reusable area/entity queries.
- Game content database loader for base data roots.
- Recipe registry and crafting core with known recipe, station, ingredient, category, and sort query results.
- Loot table registry and deterministic loot rolling support.
- Biome registry and biome map baseline.
- Data-driven living-world regions with Forest, Meadow, Mushroom Cave, Crystal Depths and Deep Cave layers, deterministic weather/events and biome-aware infinite generation/rendering/spawning.
- Session-owned named RNG streams with atomic save/resume, backup recovery and deterministic replay hashing.
- Friendly/hostile AI with perception memory, flee/attack intents, habitat caps, protected despawn, exactly-once loot deaths and an elite ecosystem path.
- Texture residency groups with decoded-byte budgets, LRU eviction, pinning, alias-shared leases and telemetry.
- Mod/content-pack loader with `mod.json`, override reporting, and tile numeric-id conflict detection.
- Combat health component with damage types, damage info, and invulnerability timing.
- Projectile and entity definition registries in the content pipeline.
- JSON player save/load with inventory slot persistence.
- Autotile 4-bit neighbor mask logic.
- Runtime projectile entities with lifetime, gravity, tile collision, and factory creation.
- Runtime enemy entities with health, tile physics, AI behavior, and factory creation from data.
- Entity manager runtime id assignment and spatial query index.
- JSON entity save/load for enemies and projectiles.
- Dropped item entity and pickup system, including live player-inventory auto-pickup in gameplay.
- Mining and building core systems with reach checks, tool power, drops, placement validation, and item consumption.
- Combat resolver for projectile-vs-enemy hits with loot drops.
- WorldTime day/night model.
- Cross-registry content reference validation with loader report errors.
- Data-driven spawn rules and SpawnSystem using biome, day/night, vertical range, ground checks, chance, and active caps.
- Modular advanced world generation pipeline with terrain, caves, ore veins, and trees.
- Core gameplay event bus with events for mining, pickups, projectile hits, deaths, and command execution.
- Event bus supports global observation plus a bounded event journal for debug tooling and future replay/profiling.
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
- Chunk streaming service owns cancellable background load/generate/decode/save jobs, session-token stale rejection, bounded main-thread apply, recovery-safe save-before-unload, and detailed queue/byte/timing telemetry.
- Chunk streaming publishes typed lifecycle events for loaded, generated, saved, unloaded, and skipped chunks.
- Structure templates and placer support reusable worldgen structures, with an initial surface shelter generation step.
- Advanced world generation now includes underground liquid pockets using tile liquid data.
- `PlayerItemUseSystem` routes selected hotbar items into mining, building, and melee actions.
- `WorldSkySystem` evaluates day/night sky color and sunlight intensity for lighting/rendering integration.
- `LiquidSimulationSystem` provides an active-region baseline for water falling and sideways flow.
- `WorldSimulationScheduler` coordinates dirty liquid/render/light regions and runs liquid steps inside `GameSimulation`.
- Equipment core includes armor/accessory slots, item stat modifiers, and player stat calculation.
- Status effects load from JSON, participate in mod content merging, and support timed buffs/debuffs, damage ticks, healing ticks, and stat modifiers.
- World query services provide tile raycasts, line-of-sight checks, and filtered tile region scans for interaction, combat, and AI.
- Dirty region tracking is available as a foundation for future liquid, lighting, particle, and chunk-render schedulers.
- Tile mining and placement events feed world-simulation dirty regions through `WorldSimulationEventBridge`.
- World analysis metrics count tile distribution, liquid coverage, solid/air ratios, and surface ranges for generation quality checks.
- Interaction targeting can choose mining and placement targets from actor reach and world raycasts.
- Melee attacks can optionally respect world line-of-sight.
- Client tile rendering now visualizes tile liquid amounts, and rendering has initial layer, shader registry, and post-processing settings scaffolding.
- Startup now enters a YjsE-branded main menu with Singleplayer, planned local splitscreen, planned multiplayer, settings, and exit entries.
- Singleplayer now has a world-select screen and create-world flow with typed world name, seed entry, random seed, save listing, and resume from saved world metadata.
- A loading state prepares the singleplayer world session before entering gameplay.
- Existing singleplayer save folders are resumed automatically from the same save layout used by autosave.
- World generation profiles define starter small, medium, and large Terraria-like world sizes and tuning targets.
- Base worldgen data now includes `small`, `medium`, and `large` JSON profiles with vertical dimension bands for sky, surface, underground, and deep layers.
- Horizontally infinite worlds can stream deterministic chunks in both negative and positive X while keeping top/bottom finite.
- Infinite chunk generation includes depth-aware ambient light, terrain, caves, ores, water pockets, and pass-through mineable trees.
- Streamed infinite-world chunks can load from saved chunk files and dirty chunks are saved before unload.
- Engine diagnostics can build world/entity/time snapshots for future debug windows and profiler overlays.
- World generation profiles now drive terrain surface, dirt depth, cave walkers, ore veins, water pockets, and tree attempts.
- Generated wood and leaf tiles are pass-through but mineable, matching the Terraria-style tree interaction model.
- Client gameplay now resolves mouse world/tile targets and routes selected hotbar use into mining, building, and melee.
- The debug console includes `/debug world` for compact world, chunk, entity, liquid, and surface metrics.
- Tile and item definitions now support normalized tags for material-style gameplay rules and tooling filters.
- Sprite asset manifests now define stable logical sprite ids, categories, dimensions, tags, and future atlas hooks.
- AI sprite generation briefs in `Game.Data/asset_briefs` define exact prompts, output paths, sizes, palettes, and constraints for another model to generate the base sprites.
- Sprite asset auditing can compare manifests, generation briefs, and actual PNG header dimensions before packaging generated art.
- Generated PNG assets are present for core terrain, trees, workbench, starter item icons, materials, basic weapons, arrows, projectile, and slime.
- The client has a `ClientTextureRegistry` that can load generated PNGs from sprite manifests and create deterministic placeholders while art is missing.
- Tile rendering now uses a per-chunk render command cache and exposes debug metrics for visible chunks, cached chunks, rebuilt chunks, evictions, tile commands, and liquid commands.
- Chunk render caches now support an LRU-style cache budget controlled by rendering settings.
- Item definitions now support data-driven primary actions for mine, place, melee, shoot, consume, cast, and interact flows.
- Placeable items can declare placement support rules such as adjacent solid support, wall/solid support, or solid ground.
- Area queries support rectangles, circles, cones, and tile flood fill as shared foundations for attacks, AI, triggers, and interaction tooling.
- `Docs/ENGINE_STATUS.md` tracks what exists, what is partial, and what still needs deeper engine work.
- `Docs/ENGINE_ARCHITECTURE.md` and `Docs/ASSET_PIPELINE_PLAN.md` document how the engine and asset pipeline are meant to work.
- Tile entity foundations are available with a chest tile entity, tile-position manager, and JSON save/load service.
- Worldgen profiles can be loaded from JSON and evaluated with a quality gate using `WorldAnalyzer` metrics.
- Combat attack-shape definitions can resolve rectangle, circle, and cone query shapes for future data-driven weapons.
- Mining targeting can select pass-through non-air tiles, so Terraria-style trees/furniture can be walked through but still mined.
- The first crafting station foundation is in place through workbench tile/item data and nearby station discovery.
- Ranged selected-item use can spawn projectiles, consume ammo, and respect `useTime` cooldowns; base data includes a wooden bow and wooden arrows.
- World generation profiles are part of the content/mod pipeline and can drive caves, water pockets, tree parameters, and data-driven ore definitions.
- `WorldGenerationService` produces generated worlds together with analysis metrics and quality-gate reports for menus, tools, and seed validation.
- Melee weapons can use data-driven attack shapes from item JSON; projectiles, weapons, and enemy contact damage can apply status effects from content data.
- Shared settings now cover video, rendering, audio, gameplay, input/keybinds, and debug options.
- Shared settings now also include UI theme, opacity, animation, streaming, render-cache, and debug metric visibility options.
- Gameplay has an in-game pause menu with editable settings tabs; the same settings surface is reachable from the main menu.
- Settings rows and tabs support mouse selection, keybind capture warns about conflicts, keybind reset requires confirmation, and resolution/fullscreen/VSync changes apply live.
- The pause/settings surface now includes world streaming options and debug toggles for the debug HUD and tile grid.
- The client UI uses a shared minimalist theme helper and a core UI animation pipeline for menu/loading/pause transitions.
- `Game.Core.UI` now provides renderer-neutral UI layout primitives, focus traversal, hit-testing, modal layers, tooltip state, and cursor interaction snapshots.
- Escape no longer closes the game from the main menu.
- Base item data includes a first copper armor set with equipment stats and workbench recipes.
- Farming core includes tilling, watering, seed planting, crop growth, seasonal checks, harvesting, regrow crops, selected-item use routing, farm plot save/load, starter hoe/watering can/seed/item data, and crop sprite generation briefs.
- Game project manifests (`yjse.game.json`) let external game repositories provide their own content root, mods root, save root name, startup map, and default world profile while using this engine.
- Startup profiles in content define starter inventory, selected hotbar slot, startup map, and default world profile without hard-coded client rules.
- `GameSessionBootstrapper` in core creates or resumes playable sessions from project, startup, settings, save, seed, and content data so external clients/tools do not need MonoGame session wiring.

## Rider

Open `YjsE.sln` in JetBrains Rider and run the shared `Game.Client` configuration. See `Docs/RIDER_SETUP.md` for the exact setup and runtime controls.

## Inventory

See `Docs/ENGINE_INVENTORY.md` for the current tech stack, feature inventory, and LOC snapshot.

## External Game Projects

To run a separate game repository against this engine:

```powershell
$env:YJSE_GAME_ROOT = "F:\Games\MyYjsEGame"
dotnet run --project Game.Client
```

The external repo should contain a `yjse.game.json` plus its own content root. Without that variable, the local sample `Game.Data` is used.

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
