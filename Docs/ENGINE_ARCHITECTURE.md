# Engine Architecture

This document explains how the engine is intended to fit together. Checklist files keep only open work; this file is the living overview for contributors.

## Project Boundaries

`Game.Core` contains deterministic engine and gameplay logic. It must stay independent from MonoGame rendering, input devices, and content-pipeline types.

`Game.Client` adapts MonoGame to the core. It owns the window, SpriteBatch rendering, device input/audio, game states, concrete widgets, debug overlays and deterministic missing-resource fallback drawing.

`Game.Data` contains replaceable reference-game definitions. The modding contract includes tiles, items, crops, recipes, loot, biomes, entities, projectiles, status effects, spawns, finite/regional worldgen, structure templates, world events, soundscapes, sprite manifests and generation/provenance material.

`Game.Tests` is the safety net for core behavior. Any engine rule that can run without graphics should be tested here.

`Website` is a separately deployable static presentation surface. It may copy validated status/docs/art for browsing, but it is not an engine assembly, runtime content root or source of truth. Download links remain disabled until a real versioned artifact exists.

`yjse.game.json` is the boundary between the engine repo and a concrete game repo. The local manifest points at the sample `Game.Data`, while external games can provide their own manifest and content root. `Game.Core.Projects` resolves that manifest and gives client/tools a stable project path contract.

## Content Load Flow

The client calls `GameContentLoader.LoadWithMods(baseDataRoot, modsRoot)`.

The loader reads each definition folder, merges base data and mods by id, validates cross-registry references, and returns a `GameContentDatabase` plus a `ContentLoadReport`.

For standalone game repositories, `GameProjectContentLoader` first resolves `yjse.game.json`, then delegates to `GameContentLoader` with the manifest's content and mods roots. This keeps engine code and game content physically separable while preserving the same validation pipeline.

`GameSessionBootstrapper` is the core entrypoint for starting or resuming a playable session from a project root, save directory, seed, world name, settings, manifest, startup definition, and content database. It loads existing saves first when possible; otherwise it resolves the world profile, builds the starter inventory from startup JSON, generates finite or horizontally infinite terrain, preloads the infinite spawn area, creates the player at world spawn, and returns one `LoadedGameSession`.

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
- Regional generation profiles and structure plans.
- World-event definitions with base/mod override semantics and cross-reference validation.
- Crops and farming definitions.
- Topdown maps for RPG, town, farm, and life-sim layouts.
- Dialogue graphs for signs, NPCs, tutorials, quests, and scripted conversations.
- Shop definitions for stock, sell prices, currency items, and economy surfaces.
- Game startup definitions for starter inventory, selected hotbar slot, default startup map, and default world profile.

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

`InfiniteWorldChunkGenerator` is the streaming-oriented generator. It creates a finite-height, horizontally infinite world and deterministically materializes any chunk at negative or positive X from the same profile and seed. It resolves regional/vertical biome plans, terrain, ores, caves, pass-through mineable trees, water pockets and row/legend structure templates. Forest, Meadow, Amber Grove, Twilight Marsh, Mushroom Cave, Crystal Depths and Deep Cave are replaceable sample profiles; Amber Workshop and Marsh Lantern Grove prove biome-filtered materialization. Templates can use explicit transparent/air symbols and cross chunk boundaries without changing their coordinate result. Full-world analysis and quality gates still belong to finite generation until sampled infinite-world analysis exists.

`ChunkStreamingPlanner` calculates required, load, and unload sets around a visible tile area. `ChunkStreamingService` owns the lifecycle around that plan: it loads saved chunks first, generates missing chunks deterministically, saves dirty chunks before unloading when a save directory exists, skips dirty unloads when data would be lost, and returns metrics plus changed chunk position lists for debug UI. The service classifies retryable/cancelled/stale/permanent failures, applies bounded exponential update-backoff and terminal suppression, and independently limits main-thread apply by operations, elapsed time and decoded bytes. One oversize result may make progress from an otherwise empty apply window. Persistent World settings control concurrency, queue, apply and retry budgets. The service uses `WorldSaveService` in region-file mode by default while still benefiting from save-load fallback for older loose chunk files.

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

`GameSaveCoordinator` is the high-level save entrypoint for runtime sessions. It writes world chunks through `WorldSaveService`, player state through `PlayerSaveService`, runtime entities through `EntitySaveService`, optional tile/farm state, and versioned simulation clock state through `SimulationSaveService`. It returns `GameSaveResult`, publishes `GameSavedEvent` when a bus is provided, and owns an autosave accumulator through `TickAutosave`. The client uses this coordinator so gameplay autosave writes the same layout that tools and future server code can use.

`GameLoadCoordinator` is the matching high-level load entrypoint. It validates the session layout, loads world chunks, restores player/inventory/entities/tile entities/farm plots and the simulation clock, returns `GameLoadResult`, and publishes `GameLoadedEvent`. `simulation.json` format v1 stores day, exact time-of-day and day length. Its absence is a supported legacy case that restores the historical default clock. `GameSessionBootstrapper` injects the loaded clock into the session simulation before its first tick.

Player saves use format version 3. In addition to inventory and player resources, they persist favorite slot state, equipment assignments, active status-effect ids with exact remaining duration, and `CharacterAppearance`; v1/v2 stacks remain compatible. Loading validates referenced item/effect ids, skips removed or incompatible content safely, and returns typed `PlayerLoadWarning` records. `LoadedGameSession` carries the restored loadout, appearance and simulation clock into the client, and autosave writes the same live state back.

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

Building resolves the item definition, then the placed tile definition, and writes runtime solidity from the tile definition. `BuildingSystem` delegates active placement to `BuildingPlacementTransactionService`, which validates reach, support, entity collision and inventory preconditions before committing the world/inventory pair. It supports optimistic inventory and authoritative `PlayerInventory` paths plus negative X. This lets furniture/stations and `mangrove_root` be placeable or mineable without becoming full collision blocks. Durable request IDs, rollback/reconciliation and furniture/wall/liquid anchors remain open.

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

The runtime selected-item path resolves the primary action and routes it through `PlayerItemUseSystem`. The implemented gameplay actions are mining, building, melee, shooting, casting, consume, till, water, plant, and harvest. Shooting can spawn a projectile and consume an ammo item from the full player inventory. Casting spawns magic projectiles, consumes player mana, and uses the player's current stat block for magic damage and mana cost.

Every selected-item attempt now returns a structured `PlayerItemUseResult`: attempted/successful action kind, succeeded/in-progress/blocked status, typed failure/success reason, mining/action progress, cooldown duration/remaining, restore amounts, applied effects, and spawned runtime objects. Mining and item use also publish typed lifecycle/feedback events. This contract is the bridge for UI, audio, particles, replay capture, multiplayer validation, and debugging without duplicating gameplay rules in the client.

`useTime` is treated as a discrete action cooldown for placement, melee, shooting, consume, and cast actions. Mining remains continuous so progress can build while the input is held.

Legacy item types still infer default actions when JSON does not provide an explicit `actions` array. This keeps older data working while allowing new content to be fully data-driven.

`PlayerStatBlock` is the runtime bridge between equipment, effects, and action systems. It currently carries max health, defense, movement, melee/ranged/magic damage, mining speed, max mana, mana cost, and mana regeneration multipliers. `EquipmentStatCalculator` builds a stat block from `EquipmentLoadout`, and `PlayerEntity.ApplyStats` pushes max-health/max-mana plus movement and mana regeneration into the player runtime. `PlayerItemUseSystem` consumes the same stat block for mining speed, ranged projectile damage, magic projectile damage, and mana cost.

## Combat And Effects

Melee weapons can define an `attackShape` in item data. The current resolver supports rectangle, circle, and cone shapes and maps them to shared area queries.

Items and projectiles can define `onHitEffects`; enemies can define `onContactEffects` alongside contact damage and knockback. Runtime combat paths apply those effects through `StatusEffectApplier` when a status-effect registry is available.

`GuardRuntimeState` adds directional guard, stamina, regeneration, parry windows and guard break. `CombatSystem.ResolvePlayerDamage` resolves hostile projectiles and contact damage through one query and one `CombatDamageResolver`, producing actual health loss, mitigation, prevented damage, stamina spent and block/parry/break outcomes. `AttackSequencer` advances fixed-tick startup, active, recovery and cooldown phases with bounded input/event/hit buffers, lockouts, buffering, combo/cancel windows, resource metadata and multiple timed swept melee shapes. Projectile runtime state supports gravity, drag, homing, lifetime, friendly fire, exact-once hit tracking, pierce and bounce. Selected-item actions do not yet consume the sequencer and remain the next ownership migration.

Typed combat events are the intended presentation bridge. The active player item-use path uses the authoritative combat system, but not every weapon yet consumes all advanced attack/charge/combo definitions.

`GameplayFeedbackRouter` is the bounded renderer-neutral presentation adapter. It translates mining, placement, hits, deaths, normal/rare drops and pickups, crafting, resource/status changes and world-event activation into fixed-capacity visual/audio command rings. The client drains caller-owned arrays into particles and audio IDs; no MonoGame type or sound device enters Core.

## Replay Diagnostics

`Game.Core.Diagnostics.Replay` records a versioned authoritative input stream containing tick, sequence, fixed-step delta, player command, optional item-use request and optional state hash. `ReplayRecorder` uses a bounded ring and records overwrite count. Snapshot/restore and JSON round trips validate order, numerics, format and a 64 MiB payload ceiling. `ReplayComparer` reports the first missing, extra, reordered, input, checkpoint, hash or version divergence and retains the last matching checkpoint. `GameSimulation` exposes explicit start/snapshot/stop capture; replay capture and hash work are disabled by default.

## Crafting

`RecipeDefinition` describes output, ingredients, optional station id, category, sort order, and whether the recipe is known by default.

`CraftingStationLocator` scans nearby tiles and returns available station ids from tile definitions. `CraftingContext` combines player inventory, nearby stations, and known recipe ids.

`CraftingSystem.QueryRecipes` returns recipe state for UI and gameplay:

- Known or hidden.
- Station available or missing.
- Ingredients available or missing.
- Fully craftable or blocked.

The current station foundation is intentionally engine-level. UI screens can consume query results later without duplicating crafting rules.

The client `CraftingOverlay` now consumes `CraftingQueryResult` directly. It shows known recipes, selected recipe details, ingredient counts, station availability, nearby station text, one-click craft, and shift-repeat crafting. `CraftingStationLocator` supports horizontally infinite worlds by preserving negative X while still clamping vertical bounds.

## Genre Modules

YjsE is still being grown from a Terraria-like sandbox prototype, but core systems are intentionally genre-neutral where possible. New gameplay should prefer reusable modules over client-only rules.

The first non-sideview module is `Game.Core.Farming`, aimed at Stardew-like and Harvest-Moon-like games:

- `CropDefinition` is a data contract for seed item, harvest item, sprite id, growth stage days, seasons, water requirement, yield, regrow days, and tags.
- `CropRegistry` maps crop ids and seed item ids so planting can stay data-driven.
- `FarmPlotManager` stores tilled/watered plot state and crop instances independently from tile rendering.
- `FarmingSystem` handles tilling, watering, planting, daily growth, seasonal withering, harvesting, inventory consumption, and regrowing crops.
- Farm actions return `FarmActionResult` with explicit failure reasons for future UI, audio, particles, NPC jobs, and automation.
- `FarmPlotSaveService` persists tilled/watered plots and crop instances into `farm_plots.json`, and the coordinated save/load pipeline can include those plots with the rest of a session.

This design keeps farming independent from MonoGame and from a specific map renderer. A Terraria-style world can use the same plots on tile coordinates, while a topdown RPG map can use non-solid ground tiles tagged as soil or farmable.

`TopDownMovementController` is the matching movement foundation for Stardew-like, Zelda-like, RPG, and town/life-sim games. It uses the existing `PhysicsBody` and tile collision resolver without gravity, normalizes diagonal movement, supports optional single-axis movement, and remains compatible with tile solidity.

`Game.Core.Maps` is the topdown map foundation:

- `MapDefinition` describes fixed-size tile maps with tile size, tags, layers, objects, and spawn points.
- `MapTileLayerDefinition` supports visual layers and collision layers. A layer blocks movement when `blocksMovement` is true and a tile value is non-zero.
- `MapObjectDefinition` supports generic objects, farm areas, signs, containers, shops, NPC spawns, and warps with optional target map/spawn ids.
- `MapRegistry` validates duplicate ids, layer dimensions, object bounds, spawn bounds, and tile-data length.
- `TopDownMapQueryService` resolves blocked tiles, interactable objects, object regions, spawn positions, and warp targets.
- `TopDownMapBody`, `TopDownMapMovementController`, and `TopDownMapMovementResult` provide pixel movement over authored maps with normalized diagonal movement, facing updates, separate-axis collision, blocking object checks, and warp detection.
- `TopDownMapSession` owns the active map id, active spawn id, actor body, and spawn positioning so a client or server can start a Stardew-like/RPG session without knowing map JSON details.
- `TopDownMapInteractionService` resolves the best interactable object in facing reach and returns explicit hit/miss data for signs, containers, shops, NPCs, doors, scripted objects, and future UI prompts.
- `TopDownMapTransitionSystem` applies warp objects by resolving target map/spawn definitions and moving the session body to the destination spawn with destination facing.
- `TopDownMapRuntimeStateStore` keeps per-map object state such as enabled/disabled, open/closed, interaction count, and last interaction tick. Query, movement, and targeting services consume that state so opened doors stop blocking and disabled objects cannot be targeted.
- `TopDownMapObjectInteractionSystem` turns resolved objects into engine actions: messages, containers, shipping bins, shops, door/gate toggles, scripted triggers, dialogue starts, farm-area use, and interactive warps. It publishes typed events for object interaction and map transitions.

Map data is loaded from `Game.Data/maps` and participates in base/mod merging and cross-reference validation. This gives Stardew-like and RPG-style games a separate map route instead of forcing every game type through procedural sideview terrain.

Dialogue and shop definitions now follow the same data-driven path. `DialogueRegistry` validates graph nodes, start nodes, option targets, and sequential links. `DialogueSystem` owns session movement through dialogue nodes and keeps option selection deterministic. `ShopRegistry` validates stock and sell prices, while `ShopTransactionService` buys and sells through `PlayerInventory` with explicit failure reasons for UI, audio, multiplayer validation, and tools. Map objects can reference `dialogueId` and `shopId`; the content validator reports broken references before gameplay begins.

Startup definitions finish the first pass at game-owned session setup. `GameStartupRegistry` loads JSON startup profiles, validates targeted hotbar/main-inventory slots, and references world profiles, maps, and starter item ids through the same content report. `GameStartupInventoryService` builds the initial `PlayerInventory` from that data. `Game.Core.Sessions` consumes those startup definitions, so the client no longer hard-codes copper tools, seeds, blocks, world profile choice, save resume, or initial player construction.

Future genre modules should follow the same shape: data definitions, registry, deterministic runtime state, clear result objects, and core tests before client UI.

## UI Toolkit

`Game.Core.UI` is the renderer-neutral UI foundation. It is intentionally independent from MonoGame so runtime UI, tools, editors, tests, and future console-specific clients can share the same layout and interaction rules.

The toolkit currently provides:

- Primitive geometry: `UiPoint`, `UiSize`, `UiRect`, and `UiThickness`.
- A retained `UiElement` tree with visibility, enabled state, hit-test state, focusability, z-index, margins, padding, tooltip text, and layout metadata.
- `UiLayoutEngine` with free, stack, grid, scroll, tabs, splitter, and dock layout modes.
- `UiHitTestService` with topmost child selection, z-order handling, and selected-tab awareness.
- `UiLayerStack` for modal and non-modal layer ordering.
- `UiFocusManager` for focus traversal across visible/enabled/focusable elements.
- `UiTooltipController` for delayed hover tooltips and pinned debug/tooling tooltips.
- `UiInteractionSnapshot` so UI routing can carry pointer, hit, focus, and cursor-item drag state together.

The MonoGame adapter adds `UiInteraction`: release-inside activation, pointer capture, hover/pressed/disabled/focus state, slider/dropdown/segmented/toggle/scroll behavior, delayed tooltips and keyboard/gamepad repeat. `UiTheme` maps settings into bounded rounded geometry, stepped gradients, glow, shadows, contrast and cursor treatments. Open modal overlays can reuse the prepared scene capture for a bounded multi-tap backdrop blur. Client overlays still own concrete drawing; migration onto more reusable core widget models remains open.

## Assets

`SpriteAssetDefinition` is engine metadata for sprite identity, category, source path, dimensions, optional atlas id, frames, and tags. Individual frames can declare an `autoTileMask` from 0 to 15; `ClientTextureRegistry` resolves the frame that matches the current tile neighbor mask and falls back to frame 0 when a sheet has no exact variant yet.

`SpriteGenerationBrief` is a generation-time contract that maps a sprite id to an AI prompt, negative prompt, output path, palette hints, and hard requirements. It lives under `Game.Data/asset_briefs` instead of `Game.Data/assets` so runtime loading stays focused on gameplay metadata. Terrain tile briefs for dirt, grass, stone, copper ore, and iron ore request 256x16 horizontal strips with 16 frames ordered by the 4-bit autotile mask convention.

`SpriteAssetAuditService` is the engine-side bridge between manifests, generation briefs, and real files on disk. It resolves every sprite path against a content root, checks PNG header dimensions without bringing an image library into `Game.Core`, verifies generation-brief path/size matches, and reports missing files, unreadable files, dimension mismatches, and incomplete 0..15 autotile frame coverage. Tools and CI can use this before packaging a game or accepting generated assets.

Wave 05 adds 24 definitions with 122 explicit source-rectangle frames: two parallax backgrounds, four 16-mask autotiles, two world-object sheets, six item/tool sprites, four eight-frame actors and six UI assets. Entries without explicit frames receive one runtime default descriptor, producing 134 Wave 05 runtime descriptors. The supplemental audit validates existence, dimensions, briefs and provenance, but its 24 `assets/Wave05/**` paths sit outside the main strict audit's `sprites/**` disk inventory; normalizing that root is an open tooling task.

The client has a `ClientTextureRegistry` that resolves `SpriteAssetRegistry` ids into MonoGame textures. If the source PNG does not exist yet, it creates a deterministic category-colored placeholder. This lets gameplay and rendering code use stable sprite ids before the real art pass is finished.

## Simulation

`GameSimulation` is the authoritative core gameplay tick host. Its stable phase order covers command/guard submission, time/farming, living-world resolution, equipment/status stats, player item use, entities, AI attack intents, projectile/contact/guard combat, exactly-once deaths/loot, pickups, activity-source spawning, respawn, world simulation, dirty lighting and frame capture. `LoadedGameSession` owns exactly one instance and its session-owned named RNG registry; identity validation prevents a parallel client simulation.

`GameFrameSnapshot` is immutable and renderer-neutral. Player state includes guard/break/stamina; entity state includes faction, AI state/target and telemetry; living-world state includes region, biome/layer/cave, weather, ambient, light/resource multipliers, world-event state and presentation sprite/density/reverb/reflection metadata. Parallax, atmosphere, particles, soundscape, debug UI and spawning consume this same truth. `SimulationPhaseTelemetry` measures the 16 authoritative phases into fixed arrays when enabled.

`LivingWorldRuntime` owns `DeterministicWorldEventExecutor` and a bounded `WorldEventJournal`. It advances scheduled events at most once per 60 simulation ticks and evaluates successful Mine/Build/Melee/Shoot/Cast/Consume/Farm actions through an exact-once monotone sequence. Phased modifiers are resolved once into spawn, lighting, weather, presentation, quantity-loot and rare-loot values. `world-events.json` format v1 atomically persists the active snapshot, cooldowns, journal and last processed action sequence with backup recovery, legacy defaults, future-version rejection and removed-mod normalization. Authoritative item-use, melee, projectile and entity-death loot all receive the same immutable modifier context.

`SessionRandomRegistry` derives isolated Xoshiro streams by canonical name. Combat, spawning, effects, farming and death keys use those streams through native and `System.Random` adapters. `random-state.json` is atomically replaced with backup recovery, and `SimulationStateHasher` includes stream state. The calibration benchmark compares two independent sessions every 60 ticks and save tests prove mid-trace continuation.

## Rendering

The client renderer draws biome parallax, tiles/liquids, prepared player/entity visuals, particles, presentation lighting, atmosphere, screen-space reflections, HUD and debug surfaces. Infinite-world passes preserve negative X. `ParallaxLayerPlanner` consumes the living presentation background ID and composes bounded surface/cave/depth/weather layers. `Wave04PlayerCharacterRenderer` advances one fixed-tick layered rig and prepares body/clothes/hair/armor/equipment commands. `EntityVisualPipeline` converts the central `RuntimeAnimations.ResolveEntity` profiles once during content setup, then prepares bounded state animations, source rectangles, typed fallbacks, shadows, outlines, hit tints and motion styles without steady-state allocation; Draw only camera-transforms and resolves resident frames.

Core lighting classifies every sky dependency explicitly as Open, Unknown or Occluded. Unknown streamed space contributes neutral residual light but blocks direct/indirect sunlight and point-light propagation; chunk materialization and unload invalidate dependent light regions. Dirty work is selected deterministically with visible regions first and a bounded offscreen remainder. `LightingRenderer` builds its viewport mask outside Draw from tile-aware CPU ray casts for sunlight and bounded colored point lights, then applies AO, penumbra, bloom and cave residual light. `ScreenSpaceEffectsRenderer` captures scene color under a pixel budget and distorts only planned water/wet surfaces; the same capture can supply UI backdrop blur. Quality tiers cap mask pixels, rays, samples, light count, reflection surfaces and taps. This is 2D ray casting/raymarching and screen-space compositing, not hardware raytracing or path tracing.

`ChunkRenderCache` stores per-chunk tile draw commands and rebuilds only for mesh-dirty chunks. `ClientTextureRegistry` separates canonical resources from frames and manages explicit UI, World, Entities, Backgrounds and Effects residency groups with decoded-byte budgets, LRU eviction, pinning and alias-shared leases. Known Draw lookups are resident-only and expose global/per-group telemetry.

`PerformanceProfiler` is renderer-neutral and uses disposable timestamp/allocation scopes with rolling averages, peaks, per-pass budgets, and ordered snapshots. `MainGame` measures update, fixed simulation, and draw. `PlayingState` measures streaming, background, tilemap, entities, lighting, UI, and debug passes. Optional client overlays show the slowest passes, current allocations, the bounded event journal, and streaming backlogs.

The next renderer evolution should add:

- Render-target or atlas-backed chunk batching.
- Atlas lookup and source-rect resolution.
- Compiled shader-registry entries for GPU presentation alternatives.
- Actual draw-call, texture-switch, GPU-time and 1080p/1440p capture metrics.

## Settings And Pause Flow

`GameSettings` is the shared data contract for video, rendering/presentation quality, UI effects, audio, gameplay/spawning, world streaming, input, debug and accessibility. The core stores keybinds as strings so it stays independent from MonoGame input types. Render FPS can be unlimited or capped from 30 through 360 independently of the 60 Hz simulation.

`PauseMenuOverlay` is the current client settings surface. It is used both from gameplay pause and the main-menu settings state. Changes are saved through `GameSettingsService`; gameplay and rendering options that are already wired can affect the active `PlayingState` immediately, and video settings flow through `GameStateManager.ApplySettings` into `MainGame` for live resolution, fullscreen, and VSync changes.

The client UI resolves colors, radii, gradients, glow, shadow, blur budget and contrast through `UiTheme`, while `Game.Core.UI.Animation` provides engine-neutral clips/tracks/curves. Main menu, loading and pause/settings use animated transitions. Pointer capture, release-inside clicks, drag controls and gamepad focus are active. Concrete overlays still own their row/slot composition; reusable core widgets and automated end-to-end click paths remain future work.

## Website Boundary

`Website/index.html` is the concise game/download/info surface; `Website/docs.html` is the larger searchable development wiki. Both are dependency-free and consume copied JSON/PNG presentation data. Validation checks local links, status vocabulary, script locality, PNG headers and byte-exact provenance copies. The website never loads `Game.Core` or becomes a runtime content source. Its current copy reports the 995/995 Debug/Release checkpoint; public downloads remain disabled until a versioned artifact exists. Client scene-smoke truth stays separate from website static validation.

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
