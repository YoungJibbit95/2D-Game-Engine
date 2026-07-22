

# YjsE Work Ledger

Last updated: 2026-07-22 (Session 0 integration baseline, subsystem audits and isolated performance acceptance)

## Active Work

- Active epic: Epic 2 - Living Sandbox And Developer Experience
- Active milestone: Milestone 5 - reduce authoritative snapshot/query costs and calibrate high-entity/streaming workloads
- Current work package: establish one reproducible Session 0 baseline for the integrated V8 runtime/art tree, preserve unchanged performance budgets, and hand Session 1 one exact UI/capture action.
- Current branch/HEAD: `codex/runtime-resilience-engine-update` at `9ce29ff`; audit-start baseline remains `master` at `f9a06072a1172d2bd8d064cabad92235443dd8c8`.
- Audit-start working tree: already dirty with 56 tracked modified files and 70 actual untracked files represented by 47 collapsed `??` entries.
- Session 0 began at `9ce29ff` with 186 tracked status entries and 138 untracked status entries across the intended engine/game/art integration; no reject, original, temporary or backup patch artifacts were present. The user authorized one validated baseline commit and prohibited pushing.

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
- Added `Website/`, then migrated it to a statically deployable Svelte 5/Vite multi-page app: a modern game/engine/download product surface, searchable 26-article wiki, honest disabled download states, local Lucide icons and milestone build/validation tooling. Static JSON and byte-exact Game.Data copies remain the presentation inputs.
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
- Added validated game-owned runtime animation content: eight Wave 04 clips, state machine, five-layer player rig, action mappings, 14 entity profiles andâ€¦9476 tokens truncatedâ€¦ tick 0.116 ms average/0.261 ms p99 and 3,125 B average/3,696 B p99 allocation; deterministic r…634 tokens truncated…gate pass with 26 docs, 8 built references, 41 PNGs and 30 byte-exact copies; download remains disabled until a release artifact exists |
| BenchmarkDotNet dry smokes | phase telemetry on/off, cold/warm streaming and two-source 200-entity spawning executed successfully |
| Lighting BenchmarkDotNet dry | 4 chunks: 15.62 ms/40 B; 12 chunks: 28.50 ms/40 B; allocation dropped by more than 99.98% from the recorded baseline |
| Physical particle micro-gate | 10,000 active particles: 0.181/0.300/0.475 ms p50/p95/p99 and 0 B; focused Core/client integration tests pass |
| `git diff --check` | passed; only Git's existing LF-to-CRLF notices were printed |

- Closed finite-background projection defects: authored parallax planes repeat only on X, preserve source aspect, use transparent vertical coverage and never stretch or repeat a terminal scanline below their authored depth.
- Added explicit frozen-weather eligibility and the `frostwood` regional biome. Snow/blizzard states now require a compatible biome, remain particle-based and premultiplied, and cannot create the former opaque gray/white viewport band.
- Unified day, dusk, night and cave presentation around `SolarRadianceModel`: local-column surface height, diffuse sky portals, lunar residual light and continuous horizon colors keep daytime terrain bright, night blue/readable and shafts naturally graded.
- Added a bounded 2,048-entry surface-height resolver cache that reuses one regional plan instead of rebuilding world-generation features for every lighting-mask column. The resolver is parity-tested across negative X and allocation-free after warmup.
- Hardened mana as reserve/commit/refund transactions and made magic items/projectiles preserve typed magic damage. Equipment-driven double jump, bounded flight and glide share the reusable movement-stat contract and HUD presentation.
- Corrected projectile collision ordering and semantics: background walls do not destroy projectiles, solid foreground tiles do, entity-versus-tile TOI is deterministic, incoming velocity survives impact for knockback, and wand shots reliably damage entities.
- Promoted the developer console to a mouse-oriented command palette with categories, search, signatures, examples, command/content autocomplete, independent history/output navigation and typed intents for world, spawn, inventory, rules, time/weather/biome, rendering, lighting, profiler, health, mana and projectile control.
- Rebased restored world-event clocks when loading or forcing an earlier world time. Active event duration, cooldowns and journal timestamps retain their relative progress instead of throwing on a backwards tick.
- Added cross-chunk tree material/socket dependencies and render-cache stamps. Unloading or mutating a neighboring chunk now invalidates only affected tree/auto-tile caches and no longer leaves mismatched canopy seams.
- Final local Debug and Release solution builds pass with 0 warnings/errors. Functional acceptance is 1,710/1,710 in both configurations, plus 21 class-isolated Release performance processes containing 39 tests at unchanged thresholds. Focused combat, lighting, parallax, weather, developer-console and tree/cache gates are green.
- Visual smokes pass for daytime Forest, readable Forest night, snowy Frostwood and 3440x1440 ultrawide Forest. Captures show no lower-edge background smear, no global snow rectangle and no permanent daytime blackout.
- Session 0 Core audit confirmed that regular and encounter spawns now acquire ingress leases, and recorded remaining authority gaps without activating speculative fixes: equipment/status mitigation bypass in the combined damage path, fixed-ID bow ammo, no distinct chop/tool-target contract and missing finite/bounds attack-shape validation.
- Session 0 renderer audit found no reject/conflict artifacts and confirmed that Main Menu depends on its untracked presentation helpers. It also recorded the 320x180 overflow, unapplied UI scale/inverse pointer transform, missing multi-tile anchor/footprint contract and unconsumed effect-sheet registry.
- Session 0 content/progression audit counted 49 items, 32 recipes, 13 loot tables, 6 projectiles, 3 effects and one two-offer shop. Only 21 items are directly reachable in audited sources (43 if ore mining is assumed); the three mobility accessories have no declared acquisition path and poison arrows cannot satisfy fixed bow ammo.
- Added deterministic acceptance partitions: 1,710 functional tests run in Debug and Release, while all 21 discovered performance classes run in fresh Release processes. Seven-window stabilization for the high-variance gates retains the original p99 thresholds and requires every measured window to remain 0 B.
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

Current low-level core evidence (`artifacts/performance-quick-2026-07-19.json`, `artifacts/performance-calibration-2026-07-19.json`):

- Quick fixed tick: 0.100 ms average, 0.133 ms p95, 0.200 ms p99, 3,101 B average and 3,672 B p99 allocation. This is 57.2% less average allocation than the previous 7,250 B quick checkpoint.
- Calibration fixed tick: 0.102 ms average, 0.149 ms p95, 0.257 ms p99 and 2,690 B average allocation. `FrameSnapshot` averages 1,621 B versus the previous 3,132 B; entity updates and pickup magnetism are 0 B in this fixture.
- Calibration initial streaming window averages 9.896 ms/11.607 ms p99 and 1,796,413 B average allocation, 30.0% below the earlier 2,564,943 B setup allocation. Timing is not presented as a speedup because content and OS scheduling changed.
- The compact unchanged-camera planner itself measures 664 B per request/plan. Current BDN dry traces allocate 13,576,584 B cold and 7,475,264 B warm versus 14,791,032 B and 10,133,640 B in the retained same-checkout smoke history (8.2%/26.2% lower).
- Two fresh 65-position long-session exports record cold p95 16.3-72.0 ms/p99 99.3-203.3 ms and warm p99 0.014-0.015 ms. The cold tail is intentionally reported as OS/background-job-sensitive.

Current physics/liquid/combat/worldgen evidence (`artifacts/performance-quick-physics-2026-07-19.json` plus isolated Release distribution tests):

- Quick fixed tick: 0.116 ms average, 0.159 ms p95, 0.261 ms p99, 3,125 B average and 3,696 B p99 allocation; 240-tick replay matches at `0x35B7C90B360EB235`.
- Simple 256x128 generation: 0.694 ms average, 0.862 ms p95 and 1,252,512 B.
- 1,000-body physics: 0.587 ms/step; reverse-ordered 1,000-body broadphase: 0.244 ms/query; both allocate 0 B.

Session 0 Release quick evidence (`artifacts/performance-session0.json`, scenario `yjse-epic1-harness-v3/quick-smoke`):

- Fixed tick: 0.309 ms average, 0.570 ms p95, 2.756 ms p99, 3,417 B average and 4,080 B p99 allocation; configured 4 ms/16,384 B guardrails pass.
- Content load averages 110.346 ms/4,171,347 B; simple 256x128 generation averages 0.879 ms/1,252,512 B; initial streaming averages 53.610 ms/1,940,200 B. Scenario-version and host differences prevent a speedup claim.
- Typed publication remains 0 B, and the 240-tick deterministic traces match exactly at `0x2B10E91090EC0C22`.
- 128 active liquid cells: 0.021/0.047/0.069 ms p50/p95/p99 and 0 B/step.
- 500 swept projectile queries: 0.290 ms average, 0.532 ms p99 and 0 B across 180 resolutions.
- 500 fast continuous bodies: 0.665 ms/step and 0 B; dense 128-body/8,128-pair continuous fixture: 6.311 ms/step and 0 B.
- Repeated 18,000-tick living-world traces match at `0xB732F6A63493D950`; the final trace spawns 134, despawns 99 and holds the population cap at 32.

Published-client texture preload on NVIDIA GeForce RTX 3070 Ti:

- Current V5 traversal: 209 resources (208 PNG files plus one shared system fallback).
- 1,072/1,072 runtime frame descriptors resolve with 0 invalid resources.
- 19,609,600 B estimated decoded RGBA payload is split into explicit residency groups. This is not GPU-memory measurement; active/adjacent-biome residency remains open.

Current high-refresh client evidence (`artifacts/performance-final-v3-release.json` and `artifacts/performance-final-v3-release-run2.json`, Release, 1920x1080, VSync disabled, 165 FPS cap, real user settings, diagnostics and scripted traversal):

- 120 warmup frames followed by 600 measured frames; the rolling telemetry window retains the latest 512 samples.
- Across two fresh processes frame time averages 6.166-6.231 ms; p95 is 6.075-7.138 ms, p99 is 10.722-11.418 ms and maximum is 12.518-13.354 ms.
- 97.3-97.9% of retained frames meet 120 Hz and 94.1-97.5% meet 144 Hz. The cap-centered 165 Hz counter naturally straddles its exact 6.061 ms deadline and is not presented as an every-frame guarantee.
- Second-run CPU scopes: Draw 1.816 ms average/7.468 ms peak, Update 0.430/9.706 ms, Lighting mask 0.599/4.296 ms and GPU upload 0.489/4.088 ms. The first run recorded a rare 13.179 ms upload/OS scheduling tail, so actual GPU timestamps and progressive upload experiments remain required.
- Both runs preload 201 resources and 1,064 frames and render the V3 Forest panorama, local terrain horizon, smooth surface-to-cave lighting and moving wildlife.

BenchmarkDotNet dry-smoke evidence is single-operation execution validation, not a calibrated distribution:

- 200-entity spawn maintenance across two activity sources: 3.646 ms and 64 B.
- Cold streaming trace across negative/positive X: 135.732 ms and 14,791,032 B.
- Warm streaming trace across origin: 42.372 ms and 10,133,640 B.
- Final representative fixed-tick BDN dry: 788.5 us telemetry-off versus 618.9 us telemetry-on, both 12 KB. This is one operation and not a calibrated comparison; the reversed ordering is not treated as a speedup.

Current entity reliability evidence (xUnit Release, isolated non-parallel gates):

- Budgeted 500-entity AI/movement update: 0.371 ms average/0.886 ms p99 and 0 B/tick versus 0.995 ms average without decision budgeting.
- Budgeted 2,000-entity AI/movement update: 1.378 ms average/3.132 ms p99 and 0 B/tick versus 4.557 ms average without decision budgeting.
- 500-entity spawn-cap maintenance: 0.184 ms p99 and 0 B/tick.
- Repeated 18,000-tick streaming soak: deterministic `0xB732F6A63493D950`, 134 spawned, 99 despawned, maximum population 32, longest zero-population run 30 and maximum 85 loaded chunks.

Current post-upgrade renderer evidence (`artifacts/renderer-upgrade-forest-tuned.json`, Release, 1280x720, 120 FPS cap, scripted Forest traversal):

- 60 warmup frames followed by 120 measured frames; average interval 8.333 ms, p95 10.557 ms, p99 11.776 ms and maximum 14.969 ms.
- CPU Draw averages 1.922 ms. Background is 0.151 ms, particles 0.055 ms and UI 0.147 ms average; these scopes remain under their local budgets.
- Lighting mask averages 2.569 ms, reflection radiance 0.297 ms, GPU-upload CPU scope 0.519 ms and total lighting preparation 3.743 ms. Mask/upload tuning remains open and no value is presented as GPU time.
- MonoGame CPU-side deltas average 60.69 Draw submissions, 3,933.7 SpriteBatch commands and 60.69 texture binding changes per frame. All 209 resources and 1,072 frame descriptors resolve with 0 invalid resources.
- The post-upgrade 2560x1440 Forest sample passes at 8.452 ms average frame interval, 10.251 ms p95, 14.682 ms p99 and 2.050 ms CPU Draw; it averages 63.3 Draw submissions, 4,784.2 SpriteBatch commands and 63.3 texture binding changes, with three visible actors in the captured frame.
- Forest, Crystal Depths and opened-pause captures pass and were visually inspected. The smoke asset panel is intentionally present and is excluded from the nonblank-scene threshold.

Current final V5 renderer evidence (`artifacts/performance-v5-covered-release-1080p.json`, `artifacts/performance-v5-covered-release-1440p.json`, Release, 165 FPS cap):

- 120 warmup frames followed by 600 measured frames; the rolling telemetry retains the latest 512 samples.
- 1080p: 6.061 ms average, 6.062 ms p95, 6.325 ms p99, 9.704 ms max, 99.80% within 120 Hz and 99.22% within 144 Hz.
- 1440p: 6.078 ms average, 6.064 ms p95, 8.625 ms p99, 14.956 ms max, 98.83% within 120 Hz and 97.27% within 144 Hz.
- CPU Draw averages 0.609/0.745 ms; background 0.044/0.047 ms and tilemap 0.338/0.430 ms. Tilemap and background remain 0 B in both runs.
- MonoGame submission deltas average 59.79/62.06 Draw calls and texture changes with 4,578.8/5,513.9 sprite commands. These are CPU-side framework counters, not GPU timestamps.

Raw local artifacts remain under ignored `artifacts/`, including `performance-lighting-physics-v7-final.*`, `final-integration-forest.*`, current strict audit JSON, prior renderer/V5 reports, benchmark profiles and `benchmarkdotnet/**`.

## Adventure Style V5 Checkpoint

- Promoted the user-selected lush fantasy worldscape and carved wood/brass interface into a shared art-direction contract. The project-owned 1672x941 main-menu panorama is runtime-active with aspect-fill crop planning, procedural fallback and bounded ambient motion.
- Reworked the shared UI material primitives so HUD, pause/settings, inventory, crafting, character editor and developer surfaces inherit dark carved wood, inset slate, brass rails, corner fittings and parchment-like hierarchy instead of isolated flat panels.
- Enlarged the expanded 1080p/1440p HUD to a reference-weighted adventure frame: 420x154 vital crest, 360x136 compass/world panel and 52-pixel hotbar slots, while compact and regular layouts remain contained.
- Added deterministic, allocation-free exposed terrain detail commands for grass, dirt, stone, amberstone, marsh moss, snow and ice. A second pixel-only tile pass keeps the visual detail but collapses texture churn: the measured Forest smoke moved from 1,976 to 69 average Draw submissions and reduced Render.Tilemap from 2.963 ms to 0.368 ms.
- Closed the remaining tree presentation split: one seed/anchor-stable palette now covers a complete regional trunk/canopy, authored regional sheets retain their lighting direction and only legacy generic leaves may mirror. Dense crown geometry and cardinal-hole regressions remain deterministic and allocation-free.
- Expanded living-world composition in Meadow and Twilight Marsh with a guaranteed modern hostile role alongside wildlife. Regular and encounter spawns now receive bounded offscreen ingress leases; ingress can step vertically around up to four tiles of terrain relief without spawning inside solids.
- Corrected the Main Menu generation-brief schema without losing its direct asset evidence fields. The repository-wide loader, runtime manifest, dimensions, provenance hashes and preview contract now agree.
- Current Release evidence: 0-warning build; 47/47 focused tree/terrain/Adventure-layout tests; 31/31 focused menu/asset/living-world tests; isolated 500/2,000-actor AI, 500-projectile contact and 500-entity gates all pass at 0 B steady state where specified. Four 1920x1080 runtime smokes (Main Menu, Forest gameplay, Pause/Settings and Crafting) pass with 263 resources, 1,518 frames and no invalid resource.
- The first complete 1,742-test run after integration passed 1,739 functional tests and reported three host-jitter-only p99 gates; all four affected cases then passed together in a fresh isolated Release process. A quiescent second complete Release run passed 1,743/1,743. Hosted CI remains required before treating cross-host tails as closed.

## Known Failures And Blockers
- Core combat/content remains `partial` for defense mitigation propagation, authored ammo policy, chop/tool-class routing and attack-shape validation. These were audited only; Session 0 does not hide them behind client-side duplicates.
- UI scaling remains `partial`: `Video.UiScale` is stored but not applied to a shared viewport/inverse-pointer transform, Pause exposes a wider 0.5-4.0 range, and 320x180 Main Menu layout overflows.
- Progression remains `partial`: `double_jump_boots`, `ether_glider` and `skyward_wings` are active preload/runtime content without acquisition, and poison-arrow/effect reachability is incomplete.

- Debug and Release each pass 1,710/1,710 functional tests with 0 build warnings/errors. Release passes all 21 discovered timing classes (39 tests) in fresh processes at unchanged budgets. Combined-host p99 outliers remain diagnostic, not functional failures; hosted CI remains unverified.
- Full-solution `dotnet format style --verify-no-changes` passes on the integrated working tree. `git diff --check` also passes; Git only reports existing LF-to-CRLF conversion notices.
- Svelte website source checks, optimized build and static/provenance validation pass locally. The previous static site had desktop/mobile browser QA; the current Svelte snapshot still needs hosted or permitted browser capture. Hosting and real artifact downloads remain unavailable.
- Hosted CI is `implemented-unverified`. Session 0 may create the authorized local baseline commit, but no push is permitted, so no hosted run exists.
- Background streaming correctness, retry/backoff, terminal classification, operation/time/byte budgets, the long planner trace and cold/warm settle distributions are locally verified; labelled region-read/generate/apply/save distributions and hosted behavior remain unverified.
- The authoritative simulation path is locally verified and its snapshot allocation is roughly halved, but it still allocates per tick and has no previous-frame interpolation contract.
- Session runtime randomness is named and persisted; finite world generation still uses its explicit seed-local `Random(seed)` by design.
- Texture groups, eviction budgets and tile atlas pages are active, but measured GPU memory and cross-category/active-biome packing remain absent. The current V7 smoke loads 259 resources/1,514 frames, estimates 43,344,896 decoded bytes and reports 0 invalid resources.
- Fixed-tick CPU headroom is strong, but the Session 0 v3 quick/retained calibration representative ticks still allocate 3,417/2,690 B. Snapshot columns and pickup magnetism are improved; combat/projectile query ownership is caller-owned and allocation-free in its representative fixture.
- The retained 600-frame V7 1080p trace passes both stretch gates: 98.44% within 120 Hz and 96.88% within 144 Hz. The post-V8 120-frame trace averages 6.061 ms with 8.202 ms p95 and 9.168 ms p99; 96.7% remains inside 120 Hz while targeting 165 FPS. A matching long 1440p/all-biome matrix, actual GPU timestamps and refresh-rate enumeration remain open.
- Post-V8 Forest light-mask construction averages 1.563 ms with a 2.622 ms peak and 0 B; total lighting preparation averages 1.971 ms with 0 B in the last sample. The richer local-surface/portal transport costs more than retained V7 but removes the periodic 425,984-byte scratch allocation and remains within the 120 Hz frame budget.
- Advanced world-event execution, phases, modifiers, exact-once scheduled/action activation, rare/quantity loot routing, replay input logs/divergence diagnostics and recovery-safe persistence are active. Trigger/room zones and production audio remain open.
- Audio mixer/soundscape routing is active, but no production sound files exist, so missing-asset telemetry rather than audible content is the expected fallback.

## Exact Next Action

Add deterministic Inventory opening to the existing smoke hook and capture compact/regular/expanded Inventory, Crafting, Settings and Developer Menu at 720p/1080p/1440p. Then author Frostwood-specific background planes, trees, vegetation and structures instead of recoloring generic Forest planes, and run a long 1080p/1440p all-biome lighting/weather/performance matrix. Continue the combined dense-scene fixture with scheduled AI, continuous bodies, projectiles and physical particles while preserving zero-allocation hot paths and the hosted/CI truth gates.

## Recommended Subagents

- Cross-Biome Visual QA: traversal/jump/mining captures at 720p/1440p and opacity/order comparison for all V6 planes.
- Tree Overlay Core: stable cross-chunk anchors, native branch-tip/canopy commands and before/after atlas/draw/visual gates for sub-tile foliage overhang.
- Legacy Asset Polish: audit and replace the highest-impact enemy/item/UI sprites with native dimensions and complete provenance.
- Water Visual QA: deterministic water-heavy fixtures and 1080p/1440p shoreline/depth acceptance for the now-prepared presentation path.
- Combat Visual QA: terminal projectile adapters and visible melee/ranged/magic/guard/encounter capture matrix.
- Performance/Integrator: active-biome residency, 1440p tier measurements, architecture boundaries and full truth gates.
- AI/Navigation Core: bounded perception snapshots, deterministic job publication, hierarchical path requests and 2,000-actor fairness/performance gates.
- Physics Core: production continuous-body routing, sloped shape contracts and dense adversarial solver distributions.
- Render Core: global transient descriptor ownership, GPU timestamp backend and 1440p 144-Hz tail attribution.

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
- `Website/static/data/**`
- `Game.Tests/DeterminismTests/**`
- `Docs/Development/CAPABILITY_MATRIX.md`
- `Docs/Development/PERFORMANCE_BUDGETS.md`
