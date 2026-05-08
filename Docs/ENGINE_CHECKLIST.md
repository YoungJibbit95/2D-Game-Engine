# Engine Checklist

## Project Foundation

- [x] .NET 8 SDK-style solution.
- [x] `Game.Core` without MonoGame references.
- [x] `Game.Client` with MonoGame DesktopGL.
- [x] `Game.Tests` with xUnit.
- [x] Rider-friendly shared run configuration.
- [x] `launchSettings.json` for `Game.Client`.
- [x] `global.json` SDK roll-forward baseline.
- [x] Rider/.NET 10 build fallback to .NET 8 targeting packs.
- [x] Fixed update runner in core.
- [x] State interface and state manager.
- [x] Serilog baseline logging.
- [x] Core event bus for gameplay/tooling hooks.
- [x] Central settings/config service.
- [x] Asset/content registry abstraction.
- [ ] Game-state transition stack for pause/menu overlays.

## Coordinates And World Data

- [x] `TilePos`, `ChunkPos`, `RectI`.
- [x] World/tile/chunk coordinate conversion.
- [x] Tests for negative coordinate conversion.
- [x] Chunked `World` storage.
- [x] Dirty flags for tile changes.
- [x] Deterministic simple world generation.
- [x] Noise service abstraction.
- [x] Chunk unload/load boundary policy.
- [x] Chunk iteration helpers for view and saves.
- [x] World metadata enrichment.

## World Generation

- [x] Modular generation pipeline.
- [x] Generation context with seed, random, noise, and surface map.
- [x] Terrain generation step.
- [x] Cave random-walk generation step.
- [x] Ore vein generation step.
- [x] Underground water pocket generation step.
- [x] Tree generation step.
- [x] Deterministic advanced generation tests.
- [x] Biome-specific generation step routing.
- [x] Structure generator.
- [x] Spawn point finder service.

## Data Pipeline

- [x] JSON tile definition loader.
- [x] Tile registry with numeric id validation.
- [x] JSON item definition loader.
- [x] Item registry with stack rules.
- [x] Base `Game.Data` tile and item files.
- [x] Content database loader for base content root.
- [x] Recipe definition loader.
- [x] Crafting system core.
- [x] Entity/projectile definition loaders.
- [x] Loot table loader.
- [x] Biome loader.
- [x] Mod folder scan and override report.
- [ ] JSON schema or validation report.

## Rendering

- [x] Debug text renderer without content pipeline dependency.
- [x] `Camera2D`.
- [x] Visible chunk calculation.
- [x] Placeholder tile colors/textures.
- [x] Tilemap renderer.
- [x] Debug grid toggle.
- [ ] Chunk render cache.
- [x] Autotiling mask calculation.
- [x] Lighting overlay renderer.

## Physics And Simulation

- [x] `PhysicsBody`.
- [x] AABB tile collision resolver.
- [x] Separate-axis collision resolution.
- [x] Gravity and ground detection.
- [x] Entity manager.
- [x] Spatial hash for entity queries.
- [x] Central core simulation tick orchestration.
- [ ] Allocation checks for update loops.

## Save/Load

- [x] MessagePack dependency selected.
- [x] LZ4 dependency selected.
- [x] World metadata save/load.
- [x] Binary chunk serialization.
- [x] Dirty-chunk save policy.
- [x] Player save data.
- [x] Runtime entity save/load.
- [x] Dropped item runtime save/load.
- [x] Generate -> save -> load -> compare integration test.

## Lighting

- [x] Greyscale tile light values.
- [x] Sunlight pass.
- [x] Time-driven sky/sunlight model.
- [x] Point light propagation.
- [x] Solid tile attenuation.
- [ ] Dirty light regions.
- [ ] Colored RGB light.
- [ ] Dynamic light sources attached to entities/items.

## Tooling

- [x] `README` dependency/status notes.
- [x] Debug overlay with FPS, player tile, chunk, and entity count.
- [x] Command parser, registry, and dispatcher in core.
- [x] Command execution events.
- [x] `/give` command.
- [x] `/time` command.
- [x] `/spawn` command.
- [x] Runtime command console overlay in client.
- [ ] World viewer tool project.

## Modding

- [x] Base content and mod content share loader path.
- [x] `mod.json` manifest parsing.
- [x] Stable id override reporting.
- [x] Tile numeric-id conflict detection.
- [x] Entity/projectile content participates in mod merge pipeline.
- [ ] Mod load order config file.
- [x] Cross-registry reference validation.
- [ ] Script discovery and sandboxing.

## Combat And Runtime Definitions

- [x] Damage type model.
- [x] Damage info model.
- [x] Health component with invulnerability window.
- [x] Melee attack system with cooldown and hitbox queries.
- [x] Projectile definitions and registry.
- [x] Entity definitions and registry.
- [x] Runtime projectile entity.
- [x] Runtime enemy factory from definitions.
- [x] Runtime enemy entity with health and physics.
- [x] Basic AI behavior interface.
- [x] Slime AI behavior baseline.
- [x] Entity manager assigns runtime ids.
- [x] Entity manager spatial queries.
- [x] Runtime projectile hit detection system.
- [x] Runtime projectile/enemy combat resolution.
- [x] Loot drops produced from runtime combat.
- [x] Runtime enemy/player contact damage.
- [x] Player respawn system from world spawn metadata.

## World Interaction

- [x] Dropped item runtime entity.
- [x] Item pickup system.
- [x] Player inventory model with hotbar/main split.
- [x] Selected item use core for mining, building, and melee.
- [x] Mining progress system.
- [x] Mining range/tool-power checks.
- [x] Tile drop result from mining.
- [x] Building placement checks.
- [x] Placeable item to tile placement.
- [x] Actor-bounds placement rejection.
- [ ] Wiring world interaction into client input.

## Time

- [x] World time model.
- [x] Day/night state.
- [x] Time commands foundation (`SetDay`, `SetNight`).
- [ ] Time-driven sky/lighting update.
- [x] Time-driven spawn rules.

## Spawning

- [x] Spawn rule definitions.
- [x] Spawn rule registry and JSON loader.
- [x] Spawn rules participate in content/mod merge pipeline.
- [x] Spawn rule validation against entity and biome registries.
- [x] Spawn system checks biome, time, vertical range, ground, chance, and active caps.
- [x] Spawn scheduler around player/view.
- [x] Spawn despawn policy.
