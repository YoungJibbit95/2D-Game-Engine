# YjsE Work Ledger

Last updated: 2026-07-15 (bounded gameplay feedback, deterministic replay capture, phased combat runtime and retry-budgeted streaming)

## Active Work

- Active epic: Epic 2 - Living Sandbox And Developer Experience
- Active milestone: Milestone 5 - consume the new phased combat runtime from data-driven item actions and reduce authoritative snapshot/query costs
- Current work package: integrate authored attack sequences into every melee/ranged/magic item-use path, preserve one-simulation ownership, and convert the new streaming/AI measurements into representative long-session distributions.
- Branch and audit-start HEAD: `master` at `f9a06072a1172d2bd8d064cabad92235443dd8c8`, upstream `origin/master`.
- Audit-start working tree: already dirty with 56 tracked modified files and 70 actual untracked files represented by 47 collapsed `??` entries.
- Current working tree remains intentionally dirty with extensive pre-existing engine/game/art/website work. Nothing was staged, committed or pushed in this continuation.

Epic 0 is locally validated but remains `partial` until the tracked workflow has passed on hosted Windows and Ubuntu. Epic 1 started because the requested first runtime-correctness dependency could be completed locally without weakening that hosted gate.

## Completed Acceptance Criteria

- Captured the truth-audit baseline before production changes and preserved the pre-existing inventory, crafting, item, feedback and asset work.
- Fixed the two audit-start failures: threshold-crossing mining feedback and nine manifest/PNG dimension contracts.
- Pinned .NET SDK 8.0.420, C# 12 and the .NET 8 analyzer baseline; enabled warnings-as-errors, deterministic builds and per-project NuGet lockfiles.
- Added Windows/Ubuntu Debug/Release CI with focused contracts, full tests, strict asset audit, deterministic preview, benchmark smoke and isolated published-client smoke.
- Generated four project lockfiles and passed local locked restore.
- Added a versioned benchmark harness with constant inputs, warmup/sample metadata, revision/configuration/environment metadata and quick-versus-calibration profiles.
- Split texture resources from frame descriptors. Canonical source paths share exactly one resource; all frames and the system fallback are materialized outside `Draw`.
- Preserved each sprite's base/mod `SourceRoot`; mod overrides and mod-only sprites resolve against their own pack. Absolute paths and source-root escapes are rejected.
- Added exact PNG dimension validation, placeholder/resource ownership, exact-once disposal and texture load/allocation/decoded-byte telemetry.
- Removed `EnsureVisibleChunks` and texture creation from `Draw`. Camera position and streaming planning now use the same Update state.
- Removed known per-frame collection churn from the chunk render-cache trim/LRU path and added cache behavior tests.
- Hardened the client smoke: project manifest and mods use `GameProjectContentLoader`; scene pixels exclude the synthetic panel; source alpha and target pixels are checked; wall-clock timeout and complete resource disposal are enforced.
- Published `yjse.game.json` and runtime `Game.Data`, while excluding `asset_briefs` and `art_direction`; validated a bundle physically outside the repository.
- Created the seven art-contract documents and integrated only the three-ID UI sample (`mana_star`, `inventory_tab`, `crafting_hammer`) into active HUD/inventory/crafting paths.
- Consolidated six byte-identical source pairs into explicit aliases and removed six redundant PNGs.
- Hardened the Python asset audit and deterministic preview check. The strict production scope has zero hard issues.
- Completed local Debug and Release builds and full suites with 485/485 tests in each configuration.
- Replaced synchronous infinite-world streaming work with pure request snapshots, cancellable load/generate/decode and dirty-save jobs, world-session generation tokens, stale-result rejection and a bounded main-thread apply queue.
- Added streaming telemetry for pending/deferred work, decoded bytes, operations, cancellation, stale/failure counts and load/generate/apply/save timings; the active debug overlay now exposes it.
- Added deterministic streaming tests for rapid camera reversal, cooperative and uncooperative cancellation, world replacement, negative-X load/decode, dirty save/unload, mutation during save, queue/apply budgets and job failures.
- Added typed batch crafting, maximum/partial planning, recipe search/filter/sort/pinning, richer Terraria-style crafting UI and typed completion/failure events wired into the runtime journal and particles.
- Added inventory categories, rarity/value/description metadata, atomic transfer/trash/cursor-return transactions, favorites, sorting/compaction/query/statistics and revision-cached inventory/crafting UI work.
- Migrated player saves additively to format v3 for favorite slot-state persistence while retaining and testing v1/v2 stack compatibility.
- Replaced allocation-heavy event publish snapshots with subscription-time arrays; the event benchmark records 0 B steady-state typed publish.
- Finalized the first nine-asset production wave with exact manifest/brief/provenance, binary alpha, deterministic previews and 0 hard audit issues.
- Completed current local Debug and Release builds and full suites with 499/499 tests in each configuration.
- Made `LoadedGameSession` own exactly one `GameSimulation` and validate reference identity for content, world, player, inventory, entities, events, time, farm plots and equipment.
- Migrated `PlayingState.FixedUpdate` to one simulation tick. Variable Update now latches player commands and item-use requests; pause/overlays suppress latched actions; client-only animation, particles, streaming, saves and UI remain adapters.
- Added immutable renderer-neutral player/entity/farm/world-time/HUD snapshots. Player, entity, farm, hotbar, resource and debug rendering now consume the fixed-tick snapshot while tilemap, lighting and streaming retain the authoritative live world.
- Moved equipment/status stat application, item use, combat, contact damage, pickup magnetism, pickup, spawning, respawn, farming day boundaries and world simulation into the authoritative phase order.
- Added automated Core/MonoGame boundary and client orchestration regression tests, immutable snapshot tests, active-entity filtering, runtime pickup-option tests, deterministic state hashing and session lifecycle tests.
- Added additive `simulation.json` format v1 persistence for day, time-of-day and day length. Legacy saves without the sidecar retain the previous default clock and are covered by compatibility tests.
- Reworked inventory overlay hit zones, query/statistics refresh and tooltip/icon text caches. Focused tests prove zero steady allocation for stable hit-zone and query-cache loops.
- Expanded the benchmark to a representative session-owned simulation with inventory, equipment, farm plots, enemies and drops plus state-hash and dual-session deterministic replay measurements.
- Completed current local Debug and Release builds and full suites with 613/613 tests in each configuration; locked restore, style, strict asset audit, deterministic preview, both benchmark profiles and isolated published-client smoke pass.

- Added chunk-budgeted dynamic light recomputation to the authoritative simulation. Mining and placement no longer leave stale shadow values; streamed chunks become light-dirty and data-driven tile definitions can emit light.
- Replaced the per-tile darkness draw loop with a reusable visible light-map texture, linear light interpolation and depth-aware cave tint while preserving point-sampled world sprites.
- Added pixel-atmosphere color grading, cave fog, stepped vignette and animated exposed liquid highlights behind explicit rendering settings and performance measurements.
- Hardened `RectI`, coordinate conversion, infinite generation and streaming distance ordering against overflow and extreme X values, including infinities and NaN at the client boundary.
- Increased default mining speed to 1.6x while retaining proportional tool-power progression.
- Added configurable render frame-rate limits (`unlimited`, 30 through 360 FPS) independently of the 60 Hz authoritative fixed simulation; VSync remains a separate setting.
- Expanded the settings, pause, inventory and crafting UI control system with sliders, toggles, segmented/dropdown controls, search, direct values and compact-resolution behavior.
- Added typed developer-command specifications, validation, help, autocomplete, history, broad gameplay/debug intents and an active client console adapter with completion and readable result styling.
- Added friendly/hostile faction, sensing, state-AI, spawn habitat/limits and deterministic loot contracts plus playable squirrel, firefly, boar and cave-spider sample content.
- Added deterministic regional generation, biome-weather and world-event foundations for future active-world integration.
- Added crash reports plus CI-capable `WorldSelect` and `Playing` client smoke start states. Both state paths pass locally; the Singleplayer transition no longer reproduces the reported immediate crash.
- Resolved the captured far-travel crash in `WorldAnalyzer`/`EngineDebugSnapshotBuilder`: debug analysis now runs in Update, scans loaded negative-X chunks without finite-width indexing and performs no world scan or allocation-heavy analysis in Draw.
- Added `Website/`: a short game/download page, searchable data-driven engine wiki, honest disabled download states and a milestone update/validation script.
- Completed Debug and Release builds with 0 warnings/errors and 613/613 tests in each configuration. Strict sprite audit reports 0 hard issues; both state smokes preload 132 resources and 702 frames.
- Added `LivingWorldRuntime` to the authoritative phase order and immutable frame snapshot. Resolved horizontal regions plus vertical biome/cave layers now drive weather, ambient soundscape metadata, lighting multipliers, spawn biome IDs, resources, parallax and pixel atmosphere.
- Separated normal `*.json` world profiles from `*.region.json`; added regional-profile and structure-plan registries with mod override semantics. The active infinite generator and background streaming share the same data-driven planner.
- Regional terrain now affects elevation, soil depth, surface/subsurface material, cave density, planned caverns and ore density across negative and positive X. A 4,097-position bidirectional planner trace remains bounded and ordered.
- Added session-owned Xoshiro named RNG streams and a `System.Random` adapter. Combat, melee loot, status effects, spawn candidate/rule choice, farming and death keys use isolated streams.
- Added atomic `random-state.json` persistence with backup recovery and legacy fallback; save-mid-trace continuation and the state hash include stream state.
- Wired player-aware faction AI, attack intents and exactly-once entity death/loot into `GameSimulation`. Perception memory, flee, attack cooldowns, collision-aware movement, habitat/region caps, protected despawn and the crystal-spider elite are active contracts.
- Added explicit texture residency groups (`Ui`, `World`, `Entities`, `Backgrounds`, `Effects`) with decoded-byte budgets, deterministic preload, LRU eviction, pinning, alias-shared leases and telemetry. Draw lookup remains resident-only.
- Completed Wave 03: four priority regenerations, five human player layers, tile-matched Meadow/Forest/Cave parallax, biome ambient/particle/elite/UI sprites and deterministic provenance/preview tooling.
- Expanded the developer console with completion selection, independent history/output navigation, scrolling, signatures/help and compact layout. Added an opened-console Playing smoke.
- Updated and browser-validated the responsive download/info and 14-article engine-wiki website at desktop and mobile widths; download links remain disabled until a real artifact exists. Fixed the PowerShell update tool to emit BOM-free UTF-8 JSON.
- At that Wave 03 checkpoint the local Release suite passed 673/673; strict asset audit had 0 hard issues; MainMenu, WorldSelect, Playing and opened-console Playing smokes loaded 144 resources and 741 frames.
- Added release-inside pointer semantics, pointer capture, drag sliders, dropdowns, segmented controls, toggles, scroll, delayed tooltips and keyboard/gamepad fallback across menus and settings.
- Added settings-backed rounded geometry, stepped gradients, glow, shadows and real scene-capture backdrop blur with accessibility controls and bounded UI-effect quality.
- Added tile-aware 2D sun/point-light ray casting, colored torches, ambient occlusion, penumbra, bloom, cave residual light and quality/pass budgets. This is explicitly not hardware raytracing.
- Added bounded screen-space water/wet-surface reflections and multi-layer biome/cave parallax driven by living-world presentation IDs.
- Added fixed-tick layered animation clips, events, blends, action locks, renderer-neutral character rigs and prepared client draw commands. The active Wave 04 player path composes body, clothes, hair, armor and equipment without a second animation clock.
- Added bounded entity visual preparation with state animation, shadows, elite outlines, hurt tint, bob, squash/stretch, wing motion and projectile velocity rotation. Camera conversion and resident sprite resolution happen only in Draw.
- Added authoritative guard input, stamina, directional block, parry, guard break and combined contact/projectile player damage resolution. Attack runtime/combo and advanced projectile contracts cover gravity, drag, homing, pierce and bounce.
- Replaced spawn-point-only population with deterministic activity-source rings, viewport exclusion, habitat/ground/liquid/collision checks, local/region/global caps and protected despawn. Friendly/hostile AI now exposes flock, perch, flee, chase, investigate, return-home, day profiles and telemetry.
- Added structure-template rows/legend materialization to infinite generation, including explicit air, cave/surface origin and cross-chunk/negative-X coverage. The first forest camp is data-driven.
- Activated living-world background, ambient particle and soundscape metadata in the client; added bounded weather/biome particles and Core/client audio mixer, crossfade and voice telemetry foundations. No production sound files are shipped yet.
- Added deterministic advanced world-event definitions, executor, phases, modifiers, cooldowns and a bounded journal. `LivingWorldRuntime` now owns this executor, throttles advancement to once per 60 ticks and resolves event-modified spawn, light, weather, presentation and soundscape values into the authoritative frame snapshot.
- Completed Production Wave 04: 31 additive runtime assets with five 16-frame player layers, five seamless 512x128 backgrounds, 13 ambient/weather/combat effects, two nine-slices and six action icons; manifest, brief, prompt, provenance and preview contracts pass.
- Expanded the static product website at the Wave 04 checkpoint to a concise engine/game/download surface and a searchable 23-article development wiki with 29 local PNGs and byte-exact Game.Data provenance. Downloads remain honestly disabled.
- Added fixed-array 16-phase simulation telemetry, client phase overlay, BenchmarkDotNet categories, cold/warm streaming dry smokes and 200-entity spawn/AI scale gates.
- Activated typed interaction candidates and atomic building transactions around reach, support, collision and inventory/world commit rules. Global mining remains at the faster 1.6x tuning.
- Removed presentation resource creation from Draw, activated the prepared entity visual pipeline and disabled the debug HUD by default for normal first-run gameplay.
- Completed the pre-parser local Debug and Release checkpoint with 858/858 tests and five isolated MainMenu/Meadow/Forest/Mushroom/Crystal scene smokes.
- Added `GameContentDatabase.WorldEvents` with base/mod merge semantics and cross-reference validation; three new event definitions (`amberfall`, `lantern_tide`, `wildlife_migration`) are active content.
- Made transactional building the active `BuildingSystem` path, including optimistic and authoritative inventory commits, negative-X coverage and typed placement results. Mining a light occluder now has Core-light and client-ray-mask stale-shadow regressions.
- Completed Wave 05 content integration: 24 assets with 122 explicit source-rectangle frames (134 runtime descriptors), Amber Grove and Twilight Marsh regional biomes, tiles 12-15 including passable/mineable `mangrove_root`, 10 items, 7 recipes, four entity/AI actors with loot/spawn contracts, two structure templates and three event definitions.
- Added a generic, safely normalized `--scene-biome` content-ID contract with six parser cases so future data-driven biomes do not require client hard-coding. Smoke mode suppresses debug/profiler overlays regardless of local settings; its state-aware asset panel avoids both the menu logo and gameplay health/mana.
- Made new sessions start authoritatively at day midpoint while preserving stored time for existing saves. `InfiniteWorldChunkGenerator.GetSurfaceHeightAt` gives forced scenes the same deterministic local surface as generation, followed by a bounded 5x5 collision-chunk preload before player placement.
- Corrected authored cave ambience: cave light is `BaseLight * weather` independently of surface daylight, while surface ambience remains daylight-modulated. Crystal Depths uses 0.42 base light and Deep Cave remains dark at 0.16.
- Current post-integration Debug and Release builds/full suites pass with 0 warnings/errors and 870/870 tests; final style verification and Release publish succeed. MainMenu plus forced Forest, Amber Grove, Twilight Marsh and Crystal Depths pass at frame 60; local visual QA confirms loaded terrain, character, HUD and readable cave ambience.
- Repaired the height-dependent lighting failure end to end. `LivingWorldRuntime` now classifies depth against deterministic local surface height for finite and infinite worlds; global astronomical sunlight no longer inherits the player's cave-biome multiplier; presentation cave/atmosphere blending no longer depends on spawn Y or globally crushes visible open sky.
- Rebuilt Core skylight as vertical sky seeds plus bounded two-pass 2D indirect propagation. Shafts and cave mouths now fall off laterally through air, solid tiles are opaque to indirect sunlight, first solids attenuate immediately and initial session chunks retain their light-dirty work after persistence flags are cleared.
- Replaced per-source `Queue`/`Dictionary` lighting propagation with bounded reusable region workspaces for luminance, solidity, visits and queues. Bitwise tile/interaction flag queries remove hidden `Enum.HasFlag` boxing in hot paths.
- Added deterministic local-surface, mixed surface/cave viewport, shaft/side-chamber, first-solid and flag-allocation regressions. Focused lighting/living-world/client masks and the full suites pass.
- Reduced the current representative quick fixed tick from 11,099 B to 7,241 B average allocation (34.8%) through exclusive internal immutable-snapshot handoff, value-type entity/farm snapshots, struct enumeration and flag hotpath cleanup. Per-phase export identifies `FrameSnapshot` at 4,368 B and `Entities` at 1,830 B per tick.
- Added validated game-owned runtime animation content: eight Wave 04 clips, state machine, five-layer player rig, action mappings, 14 entity profiles and four typed fallbacks with base/mod override diagnostics. `GameContentDatabase` loads the registry and the active player renderer consumes `player.wave04`, retaining its old definitions only as compatibility fallback.
- Current Debug and Release full suites pass 888/888 with style verification green. Final Lighting BDN dry reduced allocation from 210.53/618.23 KB to 40 B at both 4/12 chunks; 12-chunk time improved from 30.50 to 28.50 ms while 4-chunk time moved from 13.41 to 15.62 ms for the richer 2D result.
- Added additive `world-events.json` format v1 for active event state, cooldowns, bounded journal and the last processed action sequence. Atomic replacement, `.bak` recovery, legacy defaults, corrupt/future rejection, removed-mod normalization and deterministic save-load-advance continuation are covered.
- Added deterministic exact-once Mine/Build/Melee/Shoot/Cast/Consume/Farm event triggers. Rare and quantity loot modifiers now flow through one immutable context across item-use, melee, projectile and entity-death loot; duplicate action sequences cannot retrigger or duplicate the journal.
- Bound active entity visual preparation to `GameContentDatabase.RuntimeAnimations`; JSON ranges, motion styles and typed fallbacks are converted once, source rectangles share one PNG resource, and the prepared 200-entity loop is 0 B steady state.
- Made Core sky dependencies explicit as Open/Unknown/Occluded. Unknown streamed space blocks light leakage, materialization/unload invalidates dependent regions, and deterministic visible-first dirty scheduling preserves bounded offscreen progress.
- Prior world-event/lighting checkpoint: Debug and Release passed 913/913; Release quick tick was 0.088 ms average/0.117 ms p95/0.233 ms p99 with 7,250 B average allocation and exact 240-tick replay.
- Replaced unbounded gameplay presentation handoff with fixed-capacity visual/audio command rings. Mining, placement, melee/projectile hits, deaths, normal/rare pickup, normal/rare loot drops, crafting, resource/status changes and scheduled/player-triggered world events now route through typed cues with drop/drain telemetry.
- Added exact-once scheduled world-event activation publication and explicit `LootDroppedEvent` emission from both authoritative death lifecycle and legacy immediate melee loot paths.
- Added a versioned replay input format containing fixed-step delta, player command, optional item-use request and periodic state hash; recording is a bounded ring, JSON is size-limited and first-divergence reports identify order/input/checkpoint/hash/version failures plus the last matching checkpoint. `GameSimulation` can start, snapshot and stop capture without enabling per-tick hashing by default.
- Added reusable tick-native attack sequencing with startup/active/recovery/cooldown phases, input buffering, lockouts, cancel/combo windows, resource-cost metadata, multiple timed swept melee shapes, bounded command/event/hit storage and typed runtime events. Gameplay item actions remain the next explicit integration step.
- Hardened background streaming with classified retryable/permanent/cancelled/stale failures, exponential update-backoff, bounded attempts, terminal suppression/reset, elapsed-time and decoded-byte apply budgets, oversize progress guarantees and cumulative retry/apply telemetry.
- Exposed streaming operations, load/save concurrency, apply queue/time/byte budgets and retry controls through validated persistent World settings and mouse-draggable Pause-menu sliders.
- Current Debug and Release full suites pass 995/995. Warn-as-error Release build, format policy, strict main/Wave05 sprite audits, deterministic previews, isolated publish and seven frame-60 client smokes pass locally.
## Remaining Acceptance Criteria

- Obtain the first hosted Windows and Ubuntu CI results; local validation does not establish hosted graphics, casing or runner behavior.
- Reduce immutable frame snapshot, active-AI sensing and spatial-query churn; the current quick session allocates 7,250 B per fixed tick.
- Add atlas runtime, real batch/texture-switch counters and a calibrated decoded/GPU memory budget.
- Route guard/parry/break and projectile lifecycle details through the bounded feedback queues, then add real production audio files.
- Make melee/ranged/magic selected-item actions consume authored attack sequences, combo windows, resource costs and swept hit shapes instead of retaining separate cooldown authority.
- Convert the cold/warm streaming and 200-entity dry smokes into calibrated long-session distributions.
- Resolve remaining soft asset debt: 100 palette mismatches, 32 weak silhouettes and 2 low-occupancy sprites outside the clean Wave 03/04 scopes.

## Validation Results

| Command or gate | Result |
| --- | --- |
| `dotnet --version` | `8.0.420` |
| `dotnet restore YjsE.sln --locked-mode` | passed; all four projects current |
| Scoped `dotnet format YjsE.sln --verify-no-changes --no-restore --include ...` | passed for all files in the current world-event/loot/lighting/entity-visual implementation scope |
| Debug solution build | post-integration passed, 0 warnings, 0 errors |
| Release solution build | post-integration passed, 0 warnings, 0 errors |
| Debug full suite | post-feedback/replay/combat/streaming integration passed, 995/995 |
| Release full suite | post-feedback/replay/combat/streaming integration passed, 995/995 |
| Release build/publish | warn-as-error CI build passed; isolated publish contract passed under the local temporary directory |
| Focused streaming contracts | 38/38 passed in Debug and Release; retry, backoff, terminal classification, stale/cancel behavior and operation/time/byte apply budgets covered |
| Focused attack runtime contracts | 88/88 passed in Debug and Release; phase order, buffering, combos, cancels, swept shapes, bounded hits and 0 B steady sequencing covered |
| Focused replay/feedback contracts | 37 focused tests pass; version/validation/serialization/divergence plus bounded visual/audio routing and 0 B drain path covered |
| Focused animation/entity visual contracts | pass inside full suite; deterministic fixed-tick clocks, rigs, bounded commands and 200-entity preparation covered |
| Focused combat/projectile contracts | pass inside full suite; guard, parry, break, exact-once, friendly fire, lifetime, homing, pierce and bounce covered |
| Focused spawn/AI scale contracts | pass inside full suite; negative/positive X, activity sources, caps, habitat, memory and two 200-entity soaks covered |
| Focused renderer/UI contracts | pass inside full suite; ray masks, reflections, quality budgets, 0 B mask steady state and pointer/gamepad interaction covered |
| Strict Python sprite audit v2 | main CI scope passed; 180 IDs, 174 PNG sources, 6 valid alias groups, 0 hard issues |
| Deterministic Wave 04/05 preview/provenance | Wave 04 31/31 passes; Wave 05 supplemental report covers 24 assets/122 explicit frames and 0 hard issues, with 24 paths outside the main audit's `sprites/**` disk inventory |
| Release quick benchmark profile | passed and wrote JSON |
| Release calibration benchmark profile | passed and wrote JSON |
| Representative deterministic replay | integrated calibration passed; 1,200 ticks, matching `0x26927FF799797AC9` state hashes |
| RNG save/resume | passed; named-stream continuation matches after mid-trace `random-state.json` load; backup and legacy paths covered |
| World-event save/resume | 7/7 focused sidecar tests pass; atomic backup, legacy, corrupt/future rejection, registry normalization and deterministic continuation covered |
| Long camera planner trace | passed; 4,097 positions from negative to positive X remain bounded and center-ordered |
| Published client smokes | current MainMenu, WorldSelect, Forest, Amber Grove, Twilight Marsh, Crystal Depths and opened-console Forest pass at frame 60 with 199 resources, 1,062 runtime frames and nonblank scenes |
| Post-event lighting/animation smokes | fresh published forced Forest, Amber Grove and Crystal Depths runs pass at frame 60 with 199 resources/1,062 frames; surface daylight and Crystal residual cave light remain readable |
| Website validation | static gate passes after status synchronization; download remains honestly disabled until a release artifact exists |
| BenchmarkDotNet dry smokes | phase telemetry on/off, cold/warm streaming and two-source 200-entity spawning executed successfully |
| Lighting BenchmarkDotNet dry | 4 chunks: 15.62 ms/40 B; 12 chunks: 28.50 ms/40 B; allocation dropped by more than 99.98% from the recorded baseline |
| `git diff --check` | passed; only Git's existing LF-to-CRLF notices were printed |

## Measured Performance

Local integrated dirty-tree calibration on Windows 10.0.26100, AMD Ryzen 5 5500 (6C/12T), .NET SDK 8.0.420 / runtime 8.0.28, Release, scenario `yjse-epic1-harness-v2`:

| Scenario | Samples | Average | p99 | Average allocation | Interpretation |
| --- | ---: | ---: | ---: | ---: | --- |
| Typed event publish | 10,000 after 1,000 warmups | below 0.001 ms | below 0.001 ms | 0 B | steady-state publish path is allocation-free |
| Content load, base without mods | 8 after 2 warmups | 53.760 ms | 61.957 ms | 2,359,115 B | includes mod-mergeable events, Wave 05 recipes/structures and presentation/audio content |
| Simple world generation, 256x128, seed 1337 | 8 after 2 warmups | 27.236 ms | 28.549 ms | 17,054,752 B | includes both new regional structure templates; allocation remains high |
| Background streaming initial window | 8 after 2 warmups | 7.474 ms | 8.540 ms | 2,300,820 B | setup fixture; separate cold/warm dry traces are earlier checkpoint evidence |
| Representative fixed tick, 128x64, seed 424242 | 10,000 after 1,000 warmups | 0.072 ms | 0.176 ms | 9,520 B | CPU headroom remains strong; event/snapshot/query allocation target is missed |
| Representative state hash | 100 after 4 warmups | 1.022 ms | 1.535 ms | 3,538 B | includes named RNG stream state; checkpoint-only |
| Dual-session deterministic replay, 1,200 ticks | 3 after 1 warmup | 262.805 ms | 291.304 ms | 35,268,091 B | both sessions match at every checkpoint |

Final Release quick-smoke guardrail evidence (`yjse-epic1-harness-v2`, 2026-07-14T19:34:33Z):

- Fixed tick: 0.130473 ms average, 0.1917 ms p95, 11,099 B average and 12,512 B p99 allocation.
- Configured gates pass: p95 below 4 ms and average allocation below 16,384 B.
- Typed event publication remains 0 B; two 240-tick traces match at `0x6D481199215B363A`.

Current post-optimization Release quick evidence (`artifacts/performance-final-lighting-animation.json`):

- Fixed tick: 0.074 ms average, 0.099 ms p95, 0.205 ms p99, 7,241 B average and 7,888 B p99 allocation.
- Per-phase allocation: frame snapshot 4,368 B, entities 1,830 B, pickup magnetism 249 B, world simulation 120 B; all other phases are at or below 48 B average.
- Typed publication remains 0 B and both 240-tick traces still match at `0x6D481199215B363A`.

Current post-world-event Release quick evidence (`artifacts/performance-quick-current.json`):

- Fixed tick: 0.088 ms average, 0.117 ms p95, 0.233 ms p99, 7,250 B average and 7,912 B p99 allocation.
- Per-phase allocation: frame snapshot 4,376 B, entities 1,830 B, pickup magnetism 249 B, world simulation 120 B and lighting 40 B.
- Content load averages 56.547 ms/2,778,368 B; simple world generation 28.044 ms/17,052,112 B; initial streaming window 7.697 ms/2,345,941 B.
- Typed publication remains 0 B and both 240-tick traces match at `0x6D481199215B363A`.

Current post-feedback/replay/combat/streaming Release evidence (`artifacts/performance-quick-current.json`, `artifacts/performance-calibration-current.json`):

- Quick fixed tick: 0.099 ms average, 0.131 ms p95, 0.346 ms p99, 7,250 B average and 7,912 B p99 allocation.
- Calibration fixed tick: 0.065 ms average, 0.098 ms p95, 0.129 ms p99 and 6,237 B average allocation; frame snapshots remain the largest phase at 3,132 B average in this profile.
- Calibration state hash: 0.974 ms average/1.536 ms p99; 1,200-tick dual-session replay remains exact at `0x26927FF799797AC9`.
- Initial streaming window: quick 8.841 ms average/9.167 ms p99 and calibration 7.762 ms average/8.235 ms p99. This fixture includes setup and does not replace a long camera-trace distribution.

Published-client texture preload on NVIDIA GeForce RTX 3070 Ti:

- Pre-final publish: 199 resources (198 PNG files plus one shared system fallback).
- 1,062/1,062 runtime frame descriptors resolve; Wave 05 contributes 122 explicit source rectangles plus 12 implicit default frames; 0 invalid resources.
- Pre-parser Forest checkpoint: 129.901 ms resource-load time and 6,638,968 B measured setup allocations.
- 5,453,824 B estimated decoded RGBA payload, split into explicit residency groups. This is not GPU-memory measurement; the final integrated publish and post-parser scene matrix succeed.

BenchmarkDotNet dry-smoke evidence is single-operation execution validation, not a calibrated distribution:

- 200-entity spawn maintenance across two activity sources: 3.646 ms and 64 B.
- Cold streaming trace across negative/positive X: 135.732 ms and 14,791,032 B.
- Warm streaming trace across origin: 42.372 ms and 10,133,640 B.
- Final representative fixed-tick BDN dry: 788.5 us telemetry-off versus 618.9 us telemetry-on, both 12 KB. This is one operation and not a calibrated comparison; the reversed ordering is not treated as a speedup.

Raw local artifacts remain under ignored `artifacts/`, including `performance-calibration-final-integrated.json`, `asset-audit-final.json`, `benchmarkdotnet/**` and the pre-final `smoke-*-final` PNG/JSON reports.

## Known Failures And Blockers

- No known post-integration build or test failure remains. Current-scope format verification passes; whole-solution format verification still reports the shared dirty tree's broad CRLF/charset/final-newline drift and is not treated as a scoped code regression.
- Website static validation and desktop/mobile browser QA pass locally. Hosting and real artifact downloads remain unavailable.
- Hosted CI is `implemented-unverified`; no commit/push was authorized, so no hosted run exists.
- Background streaming correctness, retry/backoff, terminal classification, operation/time/byte budgets, long planner trace and cold/warm dry smokes are locally verified; hosted behavior and calibrated service time series remain unverified.
- The authoritative simulation path is locally verified, but its immutable snapshot currently allocates per tick and has no previous-frame interpolation contract.
- Session runtime randomness is named and persisted; finite world generation still uses its explicit seed-local `Random(seed)` by design.
- Texture groups and eviction budgets are active, but atlas pages and measured GPU memory are still absent.
- Fixed-tick CPU headroom is strong, but the current quick representative tick still allocates 7,250 B. Snapshot/query reduction and calibrated 200-entity attribution remain open.
- Advanced world-event execution, phases, modifiers, exact-once scheduled/action activation, rare/quantity loot routing, replay input logs/divergence diagnostics and recovery-safe persistence are active. Trigger/room zones and production audio remain open.
- Audio mixer/soundscape routing is active, but no production sound files exist, so missing-asset telemetry rather than audible content is the expected fallback.

## Exact Next Action

Continue Epic 2 Milestone 5 with one authoritative weapon-consumption and runtime-cost slice. Add data-driven item attack-sequence references/loader validation; make melee, ranged and magic selected-item actions advance the shared `AttackSequencer`, spend resources on accepted starts, materialize timed swept shapes/projectiles only in active windows, and expose phase/combo state in immutable snapshots and bounded feedback. In parallel reduce frame-snapshot/entity-query allocation and add calibrated 200-entity plus long cold/warm streaming camera distributions. Exit with Debug/Release gates, authored multi-step weapon tests, Forest/Amber/Crystal encounter captures and comparisons against the current 0.065 ms/6,237 B calibrated fixed-tick checkpoint.

## Recommended Subagents

- Item Combat Integration: authored item attack-sequence JSON, active-window execution and immutable phase/combo snapshots.
- Combat Presentation: guard/parry/break, projectile lifecycle, damage-number and audio cue adapters on the bounded feedback queues.
- Performance: representative/200-entity phase distributions, snapshot/query reduction and calibrated comparisons.
- Streaming Measurement: calibrated cold/warm long-camera traces using the new retry/apply telemetry.
- Lead Integrator: one-simulation ownership, encounter smokes, persistence compatibility and truth gates.

## Files Likely Needed Next

- `Game.Core/Randomness/**`
- `Game.Core/Runtime/GameSimulation.cs`
- `Game.Core/Runtime/GameFrameSnapshot.cs`
- `Game.Core/World/Generation/**`
- `Game.Core/Biomes/**`
- `Game.Core/Weather/**`
- `Game.Core/WorldEvents/**`
- `Game.Core/Spawning/**`
- `Game.Core/Animation/**`
- `Game.Core/Combat/**`
- `Game.Core/Audio/**`
- `Game.Client/Rendering/**`
- `Game.Client/Audio/**`
- `Game.Client/Rendering/ClientTextureRegistry.cs`
- `Game.Data/biomes/**`
- `Game.Data/worldgen/**`
- `Game.Data/sprites/entities/player/**`
- `Game.Data/sprites/world/backgrounds/**`
- `Game.Data/world-events/**`
- `Game.Data/soundscapes/**`
- `Website/data/**`
- `Game.Tests/DeterminismTests/**`
- `Docs/Development/CAPABILITY_MATRIX.md`
- `Docs/Development/PERFORMANCE_BUDGETS.md`
