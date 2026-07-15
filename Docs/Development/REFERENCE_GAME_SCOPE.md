# YjsE Reference Game Scope

Last updated: 2026-07-11

## Product Decision

Primary reference game: Terraria-like sideview sandbox.

Decision status: `verified`.

The reference game is replaceable validation content for the engine. It is not permission to hard-code one game's balance, progression, assets or startup assumptions into reusable engine modules.

## Purpose

The reference game must prove that one coherent, renderer-neutral simulation and persistence path can support a production-shaped 2D sandbox. It is the integration surface for runtime, streaming, renderer, saves, content, UI, audio, particles and performance, not a collection of disconnected feature demonstrations.

## Capability Scope

| Capability | Status | Required proof for the reference slice |
| --- | --- | --- |
| Infinite horizontal world and negative X | `verified` | Coordinate, generation, streaming and save tests; active runtime traversal |
| Chunk streaming | `partial` | Bounded request/job/apply/save queues with no synchronous Draw I/O |
| Tile rendering | `partial` | Correct source frames, one texture resource per path, atlas/batch metrics and nonblank smoke |
| Mining and building | `partial` | Core rules, active client commands, typed feedback and persistence round trip |
| Physics | `partial` | Shared deterministic collision with platforms/material extensions and runtime evidence |
| Liquids | `partial` | Negative-X-safe active regions, bounded step cost and production visual path |
| Lighting | `partial` | Region-based RGB lighting, dynamic sources and measured queues |
| Items and inventory | `partial` | Data-driven definitions, robust transactions, active UI and save compatibility |
| Crafting | `partial` | Batch/search/tracking contracts integrated through active client and typed events |
| Equipment | `partial` | Runtime stats, UI, persistence and visible character/equipment integration |
| Combat and enemies | `partial` | Deterministic attacks, feedback, loot, AI and complete active-client phases |
| Loot and progression | `partial` | Data-driven tables and a coherent replaceable progression loop |
| Saves | `partial` | Atomic/recovery-safe behavior, migrations, compatibility fixtures and interruption tests |
| Mod content | `partial` | Provenance, deterministic order, dependency validation, packaging and sandbox policy |
| UI | `partial` | Core-driven models, keyboard/mouse/gamepad navigation and client renderer widgets |
| Audio | `planned` | Adapter contract, event-driven use, fallbacks, budgets and runtime smoke |
| Particles and effects | `partial` | Event-driven client renderer, bounded pools/budgets and representative effects |
| Client nonblank smoke | `partial` | Local OS-temp publish is verified; the semantic smoke must also pass hosted Windows and Ubuntu |
| Performance baseline | `partial` | First fixed-tick/content/world/texture calibration exists; frame, streaming, save and startup evidence remains |

## Minimum Playable Loop

1. Create or resume a world.
2. Spawn safely in a finite-height, horizontally infinite surface region.
3. Move, mine terrain and collect drops.
4. Place blocks and essential world objects.
5. Organize inventory and craft an upgrade through nearby stations.
6. Equip gear and use melee, ranged and magic actions.
7. Fight enemies, receive feedback, collect loot and preserve progression.
8. Save, exit, reload and continue without state loss.
9. Traverse enough chunks to exercise load/generate/apply/unload/save budgets.
10. Demonstrate the loop through automated contracts plus a bounded runtime smoke.

Current loop status: `partial`.

## Engine Versus Game Ownership

| Concern | Owner | Status |
| --- | --- | --- |
| Deterministic simulation, world and reusable gameplay rules | `Game.Core` | `partial` |
| MonoGame graphics/input/audio adaptation | `Game.Client` | `partial` |
| Sample definitions, balance and art | `Game.Data` | `partial` |
| Correctness and contract validation | `Game.Tests` | `partial` |
| Concrete external game identity and content root | `yjse.game.json` plus external game repository | `partial` |

`Game.Core` must not reference MonoGame. Rendering must not own authoritative gameplay. Client overlays must not duplicate core rules. A concrete game must not be forced to initialize unused Farming, Topdown, Dialogue or Shop modules.

## Retained Non-Primary Modules

| Module | Status | Allowed work while primary exit gates are open |
| --- | --- | --- |
| Farming | `partial` | Build fixes, regression tests, save compatibility and shared-runtime integration only |
| Topdown maps/movement | `partial` | Build fixes, regression tests and dependency-boundary maintenance only |
| Dialogue | `partial` | Validation, compatibility and maintenance only |
| Shops/economy | `partial` | Validation, compatibility and maintenance only |
| New genre foundations | `planned` | No implementation without an explicit scope decision |

## Asset Scope

Production asset work follows representative waves. Every produced asset needs a stable ID, manifest metadata, brief/specification, exact path and dimensions, frame/origin metadata where applicable, atlas group, runtime user, content validation, audit, preview and provenance.

Large waves remain `planned` until the renderer and atlas pipeline can use and measure them. Assets without active or immediately planned runtime users remain `planned`.

## Current Exclusions

- Networking before an explicit product decision and deterministic runtime.
- Large content or asset waves that do not close an active runtime acceptance criterion.
- Expanded Farming/Topdown social simulation while core runtime gates remain open.
- Game-specific balance or progression switches in `Game.Core` or `Game.Client`.
- A new save format without migration, compatibility, recovery and rollback evidence.

## Reference Slice Exit Gate

The reference slice remains `partial` until:

1. The active client uses one authoritative core simulation path.
2. Streaming and persistence are bounded and recovery-safe.
3. Renderer resource ownership, atlas/batch behavior and nonblank output are verified.
4. The minimum playable loop is complete and persists across save/load.
5. Debug/Release correctness gates pass on Windows and Ubuntu.
6. Performance and allocation baselines meet documented budgets or have explicit accepted exceptions.
7. Every claimed capability has an active runtime user, tests and current evidence.
