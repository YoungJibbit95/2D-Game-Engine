# Engine Status

This is a compact inventory of the current engine state. Open work remains in the checklist files; this file is allowed to mention completed systems for orientation.

## What Exists

The project has a solid MonoGame/.NET 8 shell with a clean `Game.Core` separation. Core already covers chunked world storage, finite and horizontally infinite world bounds, chunk metadata, tile coordinate helpers, deterministic world generation, streamed chunk save/load, content registries, sprite asset manifests, AI sprite generation briefs, mod merge reporting, save/load foundations, inventory, cursor item interactions, data-driven item actions, crafting query states, nearby crafting station discovery, loot, combat health, projectiles, entity definitions, enemy runtime entities, slime AI, spawning rules, command console support, settings, underground-aware lighting, time, spatial queries, and a core runtime simulation tick with world-simulation scheduling.

The client now has a main menu, a loading state, a keyboard/mouse-driven settings menu, an in-game pause menu, a playable state, camera follow, debug overlays, hotbar UI, visible tile/liquid rendering, and mouse-based selected item use for mining, building, and melee. Resolution, fullscreen, and VSync settings apply live through the client shell. Rendering is still mostly placeholder-driven, but there is a first pass at render layers, shader registry scaffolding, post-processing settings, a texture registry that can resolve sprite assets to PNGs or deterministic placeholders, and a chunk render command cache with visible/rebuilt/evicted draw metrics.

World generation is modular and deterministic. It currently generates surface terrain, dirt/stone layers, caves, profile-driven ore veins, underground water pockets, simple structures, pass-through mineable trees, a forest biome map, and a safe spawn point. `WorldGenerationProfile` can tune finite vertical dimensions, surface height, dirt depth, cave walkers, cave radii, data-driven ore definitions, water pockets, and tree shape/chance. `InfiniteWorldChunkGenerator` can now stream deterministic chunks in negative and positive X while keeping vertical bounds finite and dimension-banded. `WorldGenerationService` turns a content profile plus seed into a generated world, analysis metrics, and a quality report.

The engine also has increasingly useful debug foundations: `EngineDebugSnapshotBuilder`, `/debug world`, `WorldAnalyzer`, dirty-region tracking, world raycasts, line-of-sight checks, rectangle/circle/cone entity queries, and tile flood fill. Content definitions support normalized tags, placeable items can declare support rules, and tile entities now have a first manager/save model through chests.

## Partial Systems

Rendering needs the next production pipeline: texture atlases, render-target batching, autotile source rects, proper shader passes, entity animations, particle rendering, and deeper timing metrics. Placeholder tile and light rendering is now negative-X aware for horizontally infinite worlds, real tile PNGs can be resolved through `ClientTextureRegistry` once generated, and static tile draw commands are cached per chunk until mesh-dirty invalidation.

World simulation has liquid data, baseline liquid flow, dirty-region scheduling, event-driven tile mutation tracking, integration into `GameSimulation`, and chunk metadata refreshes. It still needs pressure/settling behavior, richer liquid rules, particle scheduling, renderer-cache integration, and better visual water surfaces.

Interactions can target and use selected hotbar items, including pass-through mineable tiles such as generated trees and non-solid placeables such as the workbench. They still need richer rules for placement anchors, tile entities, furniture footprints, liquids, mining feedback, use cooldown UI, and server-friendly action validation.

Combat can damage enemies with melee/projectiles and resolve loot, and selected item use can spawn projectile attacks with ammo checks. Item, projectile, and enemy definitions can now apply status effects on hit/contact, and melee weapons can use data-driven rectangle, circle, or cone attack shapes. It still needs line-of-sight in every runtime path, attack animation phases, ammo preference rules, better knockback, hit reactions, hurt flash, and combat events for UI/audio/particles.

World generation needs Terraria-like depth: biome transitions, underground walls, lakes, cave pools, larger cavern layers, structure spacing rules, chest rooms, biome foliage variants, seed retry integration for quality gates, seed preview tooling, and region-file packing for infinite streamed chunks.

UI has a functional debug/HUD baseline, main menu, loading flow, pause menu, and editable settings menu with tabs, mouse row selection, key capture, conflict warnings, and reset confirmation. It still needs a proper UI toolkit: focus, typed text fields, tooltips, visible drag/drop widgets powered by the core cursor item model, inventory windows, equipment widgets, crafting screens that consume `CraftingQueryResult`, world-select/create-world flows, and debug panels.

## Important Missing Engine Pieces

- More tile entity types for crafting stations, doors, signs, machines, wiring, and persistent interactables.
- Data-driven tool actions so new tools/weapons/items can be added without code switches.
- Save migrations and save integrity reports.
- Save lifecycle orchestration for full player/world/session autosaves on top of the chunk persistence primitives.
- RGB colored lighting and dynamic lights attached to entities/items/projectiles.
- AI sensors and behavior composition for line of sight, hearing, ledges, hazards, and group behavior.
- Physics materials for platforms, ladders, liquids, slopes later, damage blocks, and one-way collision.
- A trigger/zone system for scripted events, weather, encounters, rooms, and mod hooks.
- Content browser/editor tooling for tiles, items, recipes, entities, effects, loot, and worldgen presets.
- Deterministic replay harness and allocation/performance diagnostics for real engine hardening.
