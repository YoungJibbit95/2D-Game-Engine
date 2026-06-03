# Engine Checklist

Only open engine work is kept here. Completed items are removed after implementation.

## Core Architecture

- [ ] Move more gameplay orchestration from `Game.Client` into core runtime services.
- [ ] Rename/package public engine assemblies from temporary `Game.*` names toward stable `YjsE.*` package names once the API boundary settles.
- [ ] Add a lightweight service registry for runtime systems that need shared access without a god class.
- [ ] Add deterministic RNG streams for worldgen, loot, AI, particles, and future replays.
- [ ] Add a deterministic replay harness for core simulation ticks.
- [ ] Add allocation and frame-budget diagnostics for update, render, lighting, liquids, UI, and content loading.

## World, Chunks, And Streaming

- [ ] Add async/background chunk streaming jobs with a main-thread apply queue.
- [ ] Add streaming event history/debug panels and per-frame streaming budget controls.
- [ ] Add region-file compaction, tombstones, and offset tables so unloaded chunks can be deleted or updated without rewriting a whole region.
- [ ] Add migration tooling that can convert existing loose chunk folders into packed region files.
- [ ] Route terrain generation, liquids, and editor tools through the bulk tile edit API where batching improves dirty-region behavior.
- [ ] Add typed tile mutation events for batch edits so simulation, audio, particles, and undo tooling can consume one result object.
- [ ] Add background wall data generation and wall mining/placement rules.
- [ ] Add platform, ladder, damage tile, and one-way collision material rules.
- [ ] Add save/load migration and integrity reports for old world versions.

## World Generation

- [ ] Add biome transition bands and biome-specific generation steps.
- [ ] Add underground wall distribution, cave wall cleanup, and background cave materials.
- [ ] Add surface lakes, cave pools, and better water-body shaping.
- [ ] Add cavern layers with larger rooms and connector tunnels.
- [ ] Add structure spacing rules, chest rooms, and loot placement.
- [ ] Add seed retry integration that regenerates when quality gates fail.
- [ ] Add seed preview/export tooling using `WorldGenerationService`, `WorldAnalyzer`, and quality reports.
- [ ] Add sampled quality analysis for horizontally infinite generation profiles.

## World Simulation

- [ ] Expand liquid simulation with pressure, settling, source/sink rules, and better visual surface metadata.
- [ ] Integrate dirty light regions into `GameSimulation` the same way liquid/render dirty regions are scheduled.
- [ ] Add dynamic world-event zones for weather, encounters, rooms, scripted triggers, and future mod hooks.
- [ ] Add tile update scheduling for machines, traps, wiring, animated world objects, and client-visible farm plot updates.

## Rendering And Shaders

- [ ] Add texture atlas lookup and source-rect resolution.
- [ ] Move `ChunkRenderCache` from command caching to atlas/render-target backed batching.
- [ ] Generate/import real 16-frame autotile PNG sheets for base terrain tiles.
- [ ] Add render targets for world, liquids, lighting, particles, UI, and post-processing.
- [ ] Add HLSL shader loading through `ShaderEffectRegistry`.
- [ ] Add deeper draw metrics: draw calls, texture switches, render-target timings, and shader timings.
- [ ] Add screenshot or smoke verification for nonblank client rendering.

## Lighting

- [ ] Upgrade greyscale lighting to RGB light values.
- [ ] Make lighting region-based so infinite worlds do not require full-width light maps.
- [ ] Attach dynamic lights to entities, projectiles, dropped items, and equipped items.
- [ ] Add torch/furniture light definitions in data.
- [ ] Add light propagation debug overlays and queue metrics.
- [ ] Add ambient rules for depth, biome, weather, and time of day.

## Content And Modding

- [ ] Add JSON schema validation or source-aware definition validation messages.
- [ ] Track content provenance per definition: base game vs mod id.
- [ ] Add explicit mod load order configuration.
- [ ] Add content hot reload command and safe reload report.
- [ ] Add debug content browser for tiles, items, crops, maps, dialogues, shops, recipes, entities, effects, loot, spawns, sprites, and worldgen profiles.
- [ ] Add missing sound/effect fallbacks per asset category.
- [ ] Add MoonSharp script discovery and sandbox boundaries after the data contract is stable.

## Interaction, Combat, And Gameplay Systems

- [ ] Add world-aware line-of-sight to every runtime combat path, not only optional melee calls.
- [ ] Add combat animation phases, attack arcs, multi-hit timing, and per-frame hit windows.
- [ ] Add projectile knockback, spread, charge, reload, and ammo preference rules.
- [ ] Wire sideview tile/entity interactions into concrete actions for chests, doors, signs, NPCs, crafting stations, machines, and tile entities.
- [ ] Add concrete client/gameplay screens for topdown shop, dialogue, container, shipping-bin, and scripted trigger action results.
- [ ] Add shared tile/entity interaction result objects for sideview world interactions with failure reasons for UI feedback.
- [ ] Add particle/audio/event hooks for mining, placement, hit, kill, pickup, craft, and blocked-use feedback.

## Persistence And Tools

- [ ] Save player equipment, active status effects, and expanded tile entities.
- [ ] Add autosave rotation and backup-before-migration behavior.
- [ ] Add crash logs and save recovery diagnostics.
- [ ] Add world viewer tooling for chunks, biomes, light, liquid, entities, and ore distribution.
- [ ] Add item/tile/entity editors backed by the same JSON loaders as the game.
- [ ] Add CI-friendly content validation over base data and example mods.
