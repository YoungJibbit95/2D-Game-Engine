# Engine Architecture

This document explains how the engine is intended to fit together. Checklist files keep only open work; this file is the living overview for contributors.

## Project Boundaries

`Game.Core` contains deterministic engine and gameplay logic. It must stay independent from MonoGame rendering, input devices, and content-pipeline types.

`Game.Client` adapts MonoGame to the core. It owns the window, SpriteBatch rendering, keyboard/mouse input, game states, debug overlays, and temporary placeholder drawing.

`Game.Data` contains base-game definitions. These files are the first version of the modding contract: tiles, items, recipes, loot, biomes, entities, projectiles, status effects, spawns, worldgen profiles, sprite asset manifests, and sprite generation briefs.

`Game.Tests` is the safety net for core behavior. Any engine rule that can run without graphics should be tested here.

## Content Load Flow

The client calls `GameContentLoader.LoadWithMods(baseDataRoot, modsRoot)`.

The loader reads each definition folder, merges base data and mods by id, validates cross-registry references, and returns a `GameContentDatabase` plus a `ContentLoadReport`.

The database currently exposes registries for:

- Tiles and tile numeric ids.
- Items and item stats.
- Recipes and crafting metadata.
- Loot tables.
- Biomes.
- Projectiles.
- Entity definitions.
- Spawn rules.
- Status effects.
- Sprite assets.
- World generation profiles.

Definitions reference sprites by stable sprite asset id, not by renderer texture object. For example, a tile can use `"texture": "tiles/dirt"`, while the asset manifest maps `tiles/dirt` to an actual source path.

## World Generation

`WorldGenerationProfile` is the data contract for world size and generation tuning. Profiles can be loaded from `Game.Data/worldgen` and merged through the same content/mod pipeline as tiles and items. Base data currently provides `small`, `medium`, and `large` profiles.

Profiles currently control:

- World dimensions.
- Surface base height and amplitude.
- Dirt layer depth.
- Cave walker count, length, depth, radius, and radius-change chance.
- Data-driven ore definitions, including tile id, replacement tile id, vein count, depth offsets, length, and radius.
- Water-pocket attempts, depth, and shape range.
- Tree attempt count, chance, and height range.
- Vertical dimension bands with per-depth ambient light, surface/subsurface/fill tiles, cave multipliers, ore multipliers, and tree allowance.

`WorldGenerationService` is the high-level API for menu/tools/server code. It resolves a profile from `GameContentDatabase`, runs `AdvancedWorldGenerator`, analyzes the result with `WorldAnalyzer`, evaluates a `WorldGenerationQualityGate`, and returns one build result containing all of that context.

`InfiniteWorldChunkGenerator` is the streaming-oriented generator. It creates a finite-height, horizontally infinite world and can deterministically materialize any chunk at negative or positive X from the same profile and seed. The first version generates terrain, depth dimensions, ores, caves, pass-through mineable trees, and underground water pockets per chunk. Full-world analysis and quality gates still belong to finite generation until a sampled infinite-world analysis pass exists.

`ChunkStreamingPlanner` calculates required, load, and unload sets around a visible tile area. `ChunkStreamingService` owns the lifecycle around that plan: it loads saved chunks first, generates missing chunks deterministically, saves dirty chunks before unloading when a save directory exists, skips dirty unloads when data would be lost, and returns metrics plus changed chunk position lists for debug UI. The service uses `WorldSaveService` in region-file mode by default while still benefiting from save-load fallback for older loose chunk files.

Streaming publishes typed events through `GameEventBus` when a caller provides one: `ChunkLoadedEvent`, `ChunkGeneratedEvent`, `ChunkSavedEvent`, `ChunkUnloadedEvent`, and `ChunkUnloadSkippedEvent`. Client UI, tools, audio, profiling, editor overlays, and later server replication can subscribe without duplicating lifecycle logic.

## World Model

The world is tile-based. It can be finite in both axes or horizontally infinite with finite vertical bounds. Global tile coordinates map to chunk coordinates through `CoordinateUtils`, including negative tile and chunk X.

`World` stores chunks in a dictionary keyed by `ChunkPos`. Chunks contain fixed tile arrays and dirty flags:

- `IsDirty` means save or persistence work may be needed.
- `NeedsMeshRebuild` means renderer cache work may be needed.
- `NeedsLightUpdate` means lighting work may be needed.

Chunks also own `ChunkMetadata`, which tracks active liquid tiles, lit tiles, tile entity count, and the last save tick. `ChunkMetadataService` can refresh all chunks, refresh touched regions, or mark chunks as saved. This metadata is intentionally separate from save payloads so runtime systems can rebuild it from world state.

`WorldSaveService` persists the infinite-horizontal flag and can round-trip loaded negative and positive X chunks. It exposes single-chunk save/load methods for streaming worlds. The client uses those methods to load a saved streamed chunk before falling back to deterministic generation, and to save dirty chunks before unloading them.

Chunk storage supports two modes. `LooseFiles` writes one compressed MessagePack/LZ4 `.bin` file per chunk under `chunks/`, which keeps debugging simple and preserves existing saves. `RegionFiles` writes chunks into packed `.region` files under `regions/` through `ChunkRegionStore`, grouping chunks by `ChunkRegionPos` with floor-division support for negative chunk coordinates. Region saves still reuse `ChunkBinarySerializer` for each chunk payload, so the chunk binary contract stays centralized. `WorldSaveService.Load` reads the storage mode from metadata and falls back to the other format when needed, which keeps old saves loadable while allowing streaming worlds to move toward packed persistence.

The current region format rewrites a full region file when chunks inside it change. That is acceptable for the first engine-facing persistence layer, but future work should add offset tables, tombstones, compaction, save migration tools, and integrity reports.

`GameSaveCoordinator` is the high-level save entrypoint for runtime sessions. It writes world chunks through `WorldSaveService`, player state through `PlayerSaveService`, runtime entities through `EntitySaveService`, and optional tile entities through `TileEntitySaveService`. It returns `GameSaveResult`, publishes `GameSavedEvent` when a bus is provided, and owns an autosave accumulator through `TickAutosave`. The client uses this coordinator so the gameplay autosave setting saves the same layout that tools and future server code can use.

`GameLoadCoordinator` is the matching high-level load entrypoint. It validates the session layout, loads world chunks, restores the player body and health, reconstructs `PlayerInventory`, repopulates `EntityManager`, restores `TileEntityManager`, returns `GameLoadResult`, and publishes `GameLoadedEvent`. `WorldSessionFactory` now resumes an existing singleplayer save folder through this coordinator before falling back to fresh generation.

Tile changes should always go through `World.SetTile`, `World.RemoveTile`, `World.TrySetTile`, `World.TryRemoveTile`, or `World.ApplyTileEdits` so chunk dirtiness, render rebuilds, lighting updates, and neighbor invalidation stay consistent.

`World.ApplyTileEdits` is the batch mutation path for generation tools, structure placement, liquid solvers, future editors, and scripted events. It validates the whole batch before mutating, coalesces duplicate tile positions with last-write-wins behavior, applies only real changes, computes changed bounds, and returns changed positions plus every loaded dirty chunk touched by the padded invalidation region.

`World.ClampRegionToBounds` is the shared region clamp for finite and horizontally infinite worlds. Finite worlds clamp X/Y to the actual world rectangle, while infinite worlds only clamp vertical bounds and preserve negative or positive X. Simulation systems use this so streamed chunks on negative X keep participating in liquids, lighting, render dirtiness, and future world-event processing.

`StructurePlacer` uses the same batch mutation path. Structure placement results report the changed bounds and dirty chunks, which gives generation code and future editor tooling an immediate hook for previews, undo records, save scheduling, and render invalidation.

## World Simulation Scheduling

`WorldSimulationScheduler` coordinates dirty regions for liquid simulation, render work, and lighting work. It is not a renderer or a liquid solver itself; it decides which tile regions need attention and when.

The current scheduler handles:

- Liquid dirty regions.
- Render dirty regions.
- Light dirty regions.
- Initial seeding from existing liquid tiles.
- Fixed-interval liquid stepping.
- Requeueing changed liquid regions for future simulation.
- Horizontally infinite dirty-region clamping.
- Initial liquid seeding from loaded chunks in horizontally infinite worlds.

`LiquidSimulationSystem` now returns changed tile regions, not just counts. `GameSimulation` consumes the scheduler result, refreshes chunk metadata for render-dirty regions, and exposes the world-simulation result through `GameSimulationTickResult`.

`WorldSimulationEventBridge` connects gameplay events to dirty regions. `TileMinedEvent` and `TilePlacedEvent` mark affected tile areas so liquids, lighting, render caches, and chunk metadata can react without every gameplay system manually knowing all downstream systems.

`GameEventBus` supports typed subscriptions and global event observers. `SubscribeAll` is intended for debugging, editor tooling, profiling, event history, and future replay/replication capture. `GameEventJournal` is a bounded in-memory recorder that subscribes globally, stores sequence numbers and timestamps, and can be drained by debug panels without changing gameplay systems.

## Tiles And Interaction

`TileInstance` is runtime data: numeric tile id, wall id, liquid, light, and flags.

`TileDefinition` is data-driven behavior: display name, texture id, solidity, light blocking, hardness, mining requirement, drop item, tags, and optional crafting station id.

This separation matters: generated trees use wood and leaf tile ids but can be placed as pass-through runtime tiles, so players can walk through them while still mining them.

Mining targets any non-air tile along the aim ray. Collision still only blocks solid runtime tiles.

Building resolves the item definition, then the placed tile definition, and writes runtime solidity from the tile definition. This lets furniture/stations be placeable without becoming full collision blocks.

## Items And Actions

`ItemDefinition` still has a coarse `ItemType` for inventory grouping and broad UI behavior, but actual use behavior is moving to `ItemActionDefinition`.

The current action kinds are:

- `Mine`
- `Place`
- `Melee`
- `Shoot`
- `Consume`
- `Cast`
- `Interact`

The runtime selected-item path resolves the primary action and routes it through `PlayerItemUseSystem`. The implemented gameplay actions are mining, building, melee, and shooting. Shooting can spawn a projectile and consume an ammo item from the full player inventory.

`useTime` is treated as a discrete action cooldown for placement, melee, shooting, consume, and cast actions. Mining remains continuous so progress can build while the input is held.

Legacy item types still infer default actions when JSON does not provide an explicit `actions` array. This keeps older data working while allowing new content to be fully data-driven.

## Combat And Effects

Melee weapons can define an `attackShape` in item data. The current resolver supports rectangle, circle, and cone shapes and maps them to shared area queries.

Items and projectiles can define `onHitEffects`; enemies can define `onContactEffects` alongside contact damage and knockback. Runtime combat paths apply those effects through `StatusEffectApplier` when a status-effect registry is available.

This keeps combat content data-driven while leaving animation timing, weapon arcs, particles, audio, and UI feedback to later client/gameplay layers.

## Crafting

`RecipeDefinition` describes output, ingredients, optional station id, category, sort order, and whether the recipe is known by default.

`CraftingStationLocator` scans nearby tiles and returns available station ids from tile definitions. `CraftingContext` combines player inventory, nearby stations, and known recipe ids.

`CraftingSystem.QueryRecipes` returns recipe state for UI and gameplay:

- Known or hidden.
- Station available or missing.
- Ingredients available or missing.
- Fully craftable or blocked.

The current station foundation is intentionally engine-level. UI screens can consume query results later without duplicating crafting rules.

## Assets

`SpriteAssetDefinition` is engine metadata for sprite identity, category, source path, dimensions, optional atlas id, frames, and tags. Individual frames can declare an `autoTileMask` from 0 to 15; `ClientTextureRegistry` resolves the frame that matches the current tile neighbor mask and falls back to frame 0 when a sheet has no exact variant yet.

`SpriteGenerationBrief` is a generation-time contract that maps a sprite id to an AI prompt, negative prompt, output path, palette hints, and hard requirements. It lives under `Game.Data/asset_briefs` instead of `Game.Data/assets` so runtime loading stays focused on gameplay metadata. Terrain tile briefs for dirt, grass, stone, copper ore, and iron ore request 256x16 horizontal strips with 16 frames ordered by the 4-bit autotile mask convention.

The client has a `ClientTextureRegistry` that resolves `SpriteAssetRegistry` ids into MonoGame textures. If the source PNG does not exist yet, it creates a deterministic category-colored placeholder. This lets gameplay and rendering code use stable sprite ids before the real art pass is finished.

## Simulation

`GameSimulation` is the core gameplay tick host for tests and future server-friendly logic. It updates time, world simulation, player, entities, projectiles, contact damage, pickups, spawning, respawn, and chunk metadata refreshes.

The current client still has direct state orchestration in `PlayingState`. Long term, more gameplay should move into core simulation services and the client should mostly feed input and render snapshots.

## Rendering

The current client renderer draws tiles, liquids, entities, player, lighting overlay, HUD, and debug text. Tile and lighting passes are aware of horizontally infinite worlds and do not clamp visible chunks to `0..WidthTiles` when the world is infinite. Tile rendering now asks `TileDefinition.TexturePath` for a sprite id and can draw a real loaded texture when the PNG exists; otherwise it keeps the existing readable colored fallback.

`ChunkRenderCache` stores per-chunk tile draw commands. It rebuilds when a chunk has `NeedsMeshRebuild`, computes a 4-bit autotile neighbor mask for each non-air tile, then clears only that mesh flag so save dirtiness remains intact. `TilemapRenderer` passes those masks into `ClientTextureRegistry`, which selects the best source frame for real terrain sheets and keeps placeholder rendering working until final art exists. The renderer exposes `TilemapRenderMetrics` for visible chunks, cached chunks, rebuilt chunks, evicted chunks, tile commands, and liquid commands.

The next renderer evolution should use:

- Chunk render caches.
- Render-target or atlas-backed chunk batching.
- Real terrain sprite sheets with complete autotile frame metadata.
- Atlas lookup and source-rect resolution.
- Render targets for world, lighting, liquids, post effects, and UI.
- Shader registry entries mapped to actual MonoGame effects.

## Settings And Pause Flow

`GameSettings` is the shared data contract for video, rendering, audio, gameplay, input, and debug options. The core stores keybinds as string bindings so it stays independent from MonoGame input types.

`PauseMenuOverlay` is the current client settings surface. It is used both from gameplay pause and the main-menu settings state. Changes are saved through `GameSettingsService`; gameplay and rendering options that are already wired can affect the active `PlayingState` immediately, and video settings flow through `GameStateManager.ApplySettings` into `MainGame` for live resolution, fullscreen, and VSync changes.

The overlay has its own row/tab hit zones for now. This is intentionally practical rather than final; a richer UI toolkit should later absorb focus, hit-testing, keyboard/gamepad navigation, typed input, confirmation dialogs, and reusable list/tab widgets.

## Testing Rules

Prefer core tests for:

- Coordinate conversion.
- Tile get/set and chunk dirtiness.
- Generation profiles and quality gates.
- Mining/building rules.
- Inventory and crafting rules.
- Combat and status effects.
- Save/load round trips.
- Data loader validation.

Client rendering should eventually get smoke tests or screenshot tests, but game rules belong in `Game.Core` first.
