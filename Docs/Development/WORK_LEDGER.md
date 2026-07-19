# YjsE Work Ledger

Last updated: 2026-07-19 (V6 native-aspect backgrounds, Wave 06, Tree V2, Terrain Polish, movement/physics and liquid presentation)

## Active Work

- Active epic: Epic 2 - Living Sandbox And Developer Experience
- Active milestone: Milestone 5 - reduce authoritative snapshot/query costs and calibrate high-entity/streaming workloads
- Current work package: preserve the measured 120-165 FPS presentation envelope while expanding the low-level simulation core: separate movement intent from physics, bound active liquids and homing/combat queries, eliminate finite-generation materialization churn and retain 500-entity/18,000-tick truth gates.
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
- Pre-lighting checkpoint: Debug and Release full suites passed 995/995; warn-as-error Release build, format policy, strict main/Wave05 sprite audits, deterministic previews, isolated publish and seven frame-60 client smokes passed locally.
- Removed presentation preparation from the fixed-step loop and added one post-simulation `LateUpdate` dispatch per client frame. Fixed-step catch-up can no longer repeat lighting, reflection and atmosphere work several times before one Draw.
- Replaced per-neighbor/per-ray world dictionary access in the client light mask with one packed tile sample per mask pixel, array-local AO and point rays, plus a linear bounded directional shadow sweep. Empty colored-light and bloom maps are no longer uploaded or drawn.
- Added civil twilight/night floors, perceptual darkness, gentler cave AO, configurable 0.22 cave residual default and wider O(n) separable penumbra. Amber surface and Crystal Depths frame-60 captures are readable without flattening cave depth.
- Added a 1920x1080 Medium lighting regression fixture. The current Release measurement is 1.964 ms average and 0 B across 40 samples after 8 warmups; the checkpoint Debug and Release full suites passed 999/999.
- Added allocation-free presentation cadence scheduling for lighting, reflections, atmosphere and scene capture. Reused scene captures avoid redundant reflection/blur work; Low/Balanced/Fast presets expose 30-120 Hz presentation rates independently of the 60 Hz simulation.
- Added a high-resolution variable render limiter, rolling average/p95/p99 frame telemetry and 120/144/165 Hz budget counters. A long Release Forest run at 1920x1080 and a 165 FPS cap averages 6.063 ms with 6.062 ms p95 and 6.063 ms p99 after a 300-frame warmup.
- Removed the per-tile global debug-grid command explosion, made chunk-streaming view-driven and bounded to 30 Hz while work is pending, and replaced keybind parsing allocations with a span-based parser. The same 1080p scene improved from 16.727 ms average Draw time to a 1.203 ms measured CPU Draw average in the final paced run.
- Added modern pixel/web UI primitives with layered surfaces, deterministic hover/focus/press animation, sliders, steppers, segmented controls, icon commands, toggles and tooltips. Pause/settings now expose direct cadence controls and presets instead of click-heavy value cycling.
- Integrated authored attack-sequence JSON into selected melee, ranged and magic items. Accepted starts atomically spend mana/ammo/stamina, attacks materialize only during active windows, and immutable snapshots/HUD expose phase, combo and action feedback.
- Moved visible chunk command rebuilds into bounded `LateUpdate` preparation. `Draw` now consumes prepared cache entries only, while authoritative lighting processes at most one dirty chunk per tick by default.
- Repaired empty travelling worlds: most spawn attempts now use a narrow offscreen ingress band around activity areas, placement searches loaded body/ground cells against local surface/open-sky data, and obsolete finite-world height restrictions no longer reject infinite-world actors. Bird, rabbit, bat and cave worm now have active friendly/hostile movement, AI and despawn profiles.
- Added a 90-second living-world integration contract proving friendly and hostile actors spawn outside the viewport in loaded chunks, move into visible range and retain valid AI rules.
- Separated atmospheric surface residual light from authored cave ambience, clamped buried solids, preserved bounded directional occlusion at maximum ray distance and added mined/open-sky/daylight regressions. The presentation light maps now rotate through three GPU resources and skip color/bloom encode and upload when no point light is visible.
- Added low-latency frame pacing with Windows MMCSS game-thread scheduling, a high-resolution waitable timer and explicit VSync policy. Debug client/Core builds are optimized while retaining portable symbols; frame finalization and lighting CPU/encode/upload phases are measured separately.
- Added deterministic biome/depth/night parallax composition with bounded variants, landmarks and local-surface horizon placement. Four native, seam-safe `1536x384` Forest/Amber/Marsh/Crystal V5 panoramas are active with complete brief, provenance, contact-sheet and audit contracts.
- The prior July 15 Debug and Release checkpoint passed 1076/1076 after the spawn, renderer, lighting and panorama integration.
- Repaired encounter starvation: matching encounters that are capped, cooling down or cannot produce a plan no longer consume the whole scheduler interval; successful encounter intents share the configured attempt budget with normal population maintenance and retain explicit runtime encounter attribution.
- Made unloaded/invalid tiles blocking for entity physics, added a strict null-AI constructor guard and verified moving spatial-query membership. Spawn placement and the 36,000-tick repeated soak reject actors in solids or unloaded chunks.
- Replaced per-add/per-tick full spatial rebuilds and production AI full-list sensing with incremental pooled grid updates, indexed ID lookup and bounded neighborhood queries. Indexed despawn and spawn-overlap checks remove the obvious mass-removal and placement scans.
- Added deterministic warm-start and loaded offscreen ingress, then repeated the five-minute/18,000-tick streaming soak across negative/positive X, day/night and surface/caves. The retained earlier checkpoint matched at `0x41CBB2B829F7EB8F`; the current trace is recorded below.
- Added 500-entity CPU/allocation gates. Final isolated Release AI/movement measures 4.488 ms p99 and 0.5 B/tick after a 31,186.8 B/tick allocation baseline; spawn-cap maintenance measures 0.184 ms p99 and 0 B/tick.
- Added tiered 2D point-light ray sampling: Medium uses one bounded ray while High uses three endpoint-spread samples for fractional penumbra visibility. The contract explicitly remains a CPU tile-mask/screen-space technique, not hardware ray tracing.
- Added a bounded reflection-radiance map that projects colored point-light and daylight energy onto the existing water/wet-surface plan. Content-hash-checked triple-buffer uploads cap the lighting texture candidates at four and remain 0 B in focused hot-path gates.
- Added a per-frame presentation admission budget. Lighting, reflection, atmosphere and scene-capture preparation no longer need to cluster on the same frame; initial, explicitly requested and starvation-protected work can still exceed the budget so visual state never stays stale indefinitely.
- Added deterministic procedural background detail tiers with 24/64/128 fixed commands for haze, wind-driven cloud wisps, stars, cave strata and ambient motes. Forest and Crystal Depths Release captures verify the integrated surface/cave paths.
- Expanded particles to 192/640/1,536 active instances by quality with hard per-call, ambient and draw budgets, offscreen culling, drag, pulse, sway, spark, ring and streak animation. Current Forest Draw averages 0.055 ms for the particle pass.
- Added responsive compact/regular/expanded HUD and profiler layout planners plus reusable pixel glass, glow, status-chip and segmented-meter primitives. Cached HUD text and the redesigned opened-pause surface pass 62/62 UI tests in Debug and Release; both layout planners are 0 B steady state.
- Current renderer/UI scopes pass 241/241 in Debug and Release. Game.Client Debug and Release builds pass with 0 warnings/errors; focused background/VFX passes 32/32 and focused lighting passes 22/22 in both configurations.
- Captured Release Forest, Crystal Depths and opened-pause smokes at 1280x720 with 209 resources, 1,072 frames and 0 invalid resources. The tuned Forest/120-cap sample records 8.333 ms average frame interval, 10.557 ms p95, 11.776 ms p99 and 1.922 ms CPU Draw average.
- Replaced per-camera `HashSet` materialization for required/retained chunks with immutable rectangular chunk windows and list-backed plan views. Membership remains O(1) for load/retain windows, unchanged-camera planning is bounded at 664 B and empty streaming updates no longer allocate five result lists eagerly.
- Reduced authoritative snapshot churn with version-cached hotbar/inventory summaries, value-compared farm snapshots, reusable pickup query buffers and a columnar copy-on-change entity snapshot builder. Published snapshots retain immutable historical storage and preserve faction, AI state, target and detailed AI telemetry.
- Current Release quick fixed-tick allocation fell from 7,250 B to 3,101 B (57.2%); calibration fell from 6,237 B to 2,690 B (56.9%). Calibration `FrameSnapshot` fell from 3,132 B to 1,621 B while pickup magnetism is 0 B in the representative profile.
- Added 0/500-entity snapshot distributions and immutability/determinism gates. The 500-moving-entity snapshot records 5,000.2 B/tick with 0.549 ms p99 snapshot time; the empty runtime snapshot records 624 B/tick. Non-default faction/AI/telemetry survives end-to-end capture.
- Calibrated the current cold/warm bidirectional streaming distribution across 65 negative-to-positive-X positions in two fresh Release runs. Cold p95 is 16.3-72.0 ms and p99 99.3-203.3 ms under OS/background-job variance; warm p99 is 0.014-0.015 ms. No sample exceeds its explicit budget.
- Renderer telemetry now truthfully reports MonoGame submission deltas: the focused Forest sample averages 48.12 Draw submissions, 805.32 SpriteBatch commands and 48.12 texture-binding changes with 6.136 ms frame p99. MonoGame exposes no backend GPU timestamps, so GPU time remains explicitly unsupported; static lighting content-hash elision measures 0 B and skips the upload.
- Current Debug and Release solution builds pass with 0 warnings/errors and both full suites pass 1381/1381. Exact allocation/timing micro-gates run serially so their evidence is not contaminated by unrelated test work. A long Forest smoke reaches seven active enemies with one visible actor, 0 invalid texture resources and bounded CPU Draw; its longer frame-pacing sample remains OS/upload-sensitive and is not used to replace the shorter calibrated renderer traces.
- Added four additive native `1536x384` V5 panoramas for Forest, Amber Grove, Twilight Marsh and Crystal Depths with exact manifest dimensions, biome runtime references, generation briefs, seam previews and provenance. The strict main audit now covers 190 IDs/184 PNGs/6 aliases with 0 hard issues.
- Made authored `_vN` panorama composition seam-safe and opaque at full surface presence, suppressed geometric haze/cloud backdrops that produced visible rectangles and retained thin stepped detail only for legacy scenes. Focused parallax gates pass 29/29 in Debug and Release at 0 B steady state.
- Shared an allocation-free deterministic tree silhouette planner between finite and infinite generation. Layered crowns, edge notches, branches and root flare replace rectangular canopies; 16/16 focused Debug/Release generation contracts pass.
- Polished World Library/Create World with responsive metadata cards, cached labels, caret/focus behavior, typed seed validation and pointer/gamepad navigation from 320x240 through 4K. The focused scope passes 22/22 in Debug and Release, its planner is 0 B and a 640x360 Release smoke passes.
- Routed guard block/parry/break and projectile launch through bounded exact-once feedback queues; typed bounce/pierce/expire/destroy adapters are covered without client-side authority and remain 0 B steady state. Runtime attachment of every terminal projectile path remains open.
- Captured final V5 Release smokes for all four biomes at 1920x1080 plus Forest at 2560x1440. Every capture passes with 209 resources, 1,072 frames and 0 invalid resources. Role-aware selection prevents surface/cave replacement, composite caves suppress legacy duplicates and authored panoramas now cover the full viewport instead of ending after the source-height band. Forest pacing averages 6.061/6.078 ms at 1080p/1440p and background Draw averages 0.044/0.047 ms at 0 B.
- Replaced the visually lossy full-height panorama scaling with a cached terrain envelope: the background anchors two tiles below the deepest generated surface across the visible range plus a 32-tile margin, while explicit `ExtendBottomEdge` coverage handles lakes, extreme offsets and later excavation. The focused parallax scope passes 43/43 in Debug and Release at 0 B; the final 2560x1440 Release smoke has 0 invalid resources, preserves the detailed Forest composition and measures background Draw at 0.127 ms average/0.225 ms peak. Its short-run frame tail is retained as visual proof, not as a replacement for the longer calibrated pacing trace.
- Replaced the lossy viewport-height panorama fit with an authored-distance contract. Surface images retain exact 4:1 aspect through one uniform X/Y viewport-tiered scale, including ultrawide; camera zoom, jumping, terrain depth and large negative X positions cannot change it. Seamless repeats use no mirror, alternation, jitter, overlap or fractional filtering, and top/bottom coverage remains separate from image scaling. The focused Parallax scope passes 80/80 in Debug and Release at 0 B.
- Moved sideview/topdown controllers into `Game.Core.Movement`; the sideview controller now applies intent only while `PhysicsWorld` owns player gravity, force integration and tile collision. Generic dynamic/kinematic/static bodies, materials, layer/mask filtering, detailed contacts, swept/substepped tile collision and deterministic heap-sort sweep-and-prune use caller-owned storage. Capacity overflow fails before mutation instead of slowing simulation time. Final isolated Release fixtures measure 1,000 settled/contact bodies at 0.988 ms/step and broadphase queries at 0.331 ms/query, both at 0 B.
- Replaced liquid whole-region steady scans with a bounded active/deferred cell workspace. Initial-world and compatibility scans now enter the same hard seed budget, scheduler work continues without new dirty regions and dropped active-cell work schedules bounded region recovery. The final 128-cell Release distribution records 0.021/0.047/0.069 ms p50/p95/p99 and 0 B per step.
- Added shared caller-owned entity/combat query workspaces. Projectile entity hits use swept broadphase plus deterministic time-of-impact ordering, and homing is capped at 256 fair rotating queries per tick, 128 nearest candidates and 512 entry tests per projectile. The final 500-projectile fixture records 0.290 ms average, 0.532 ms p99 and 0 B across 180 resolutions.
- Migrated finite terrain, cave, ore, wall, liquid, tree and structure passes to a chunk-local `WorldGenerationWorkspace`. The latest 256x128 quick fixture measures 0.694 ms average, 0.862 ms p95 and 1,252,512 B versus the previous 28.044 ms/17,052,112 B checkpoint; simple and advanced deterministic hashes remain unchanged.
- Hardened negative-X collision at large world coordinates by replacing a sub-ULP decimal edge epsilon with `MathF.BitDecrement`. The focused wall-entry regression and repeated 18,000-tick streaming soak now cover this far-world boundary.
- Added Mushroom Cave bat/spider spawn rules plus a bounded two-role skirmish encounter. The streaming soak now retains every chunk overlapped by an actor body, so it tests the same collision/loaded-world contract used by the runtime instead of a center-chunk approximation.
- Compiled the Playing presentation order through a fixed-capacity render graph with explicit pass/resource dependencies, output culling, phase validation and transient lifetime/alias telemetry. The generic struct executor rebuilds only when graph configuration changes; the representative 15-pass Release fixture remains 0 B and below 0.18 ms p99.
- Added content-load-time CPU-baked padded atlas pages for tile animation frames. Tilemap Draw now consumes atlas-page source rectangles without file IO, texture readback or atlas construction in Draw, while preserving registry ownership for non-tile resources and exposing page/bucket/byte telemetry.
- Added stable shadow-before-actor entity submission buckets, real fullscreen Effect binding and stable shader handles. The 200-alternating-actor fixture reduces estimated texture switches from 400 to 201 at 0 B and retains a lossless overflow fallback.
- Replaced the repeated eight-tap full-resolution overlay blur with a quality-scaled downsample plus bounded Kawase ping-pong preparation reused from scene capture. High 1080p radius-8 planning reduces estimated sample work by 62.5%; the visual open-pause Release smoke passes.
- Migrated ground/flying enemies and dropped items into one EntityManager-owned complete PhysicsWorld batch while leaving character intent in `Game.Core.Movement`. Deterministic body-pair narrowphase applies mass/material impulses, friction and tile-safe positional correction through caller-owned storage; initial tile overlaps now recover or fail closed with explicit flags. The retained 500-actor p99 baseline falls from 4.449 ms to a 1.688 ms median across three Release runs; the packed 500-body pair gate is 0 B.
- Added opacity-only authored-horizon feathering and corrected premultiplied parallax/reflection tints. The panorama keeps a uniform aspect-preserving repeat scale while the previous hard top-fill seam is removed; final 1080p Background Draw averages 0.043 ms at 0 B.
- Replaced the active opaque/feathered panorama path with twelve independent binary-alpha V6 Far/Mid/Near planes for Forest, Amber Grove, Twilight Marsh and Crystal Depths. Far silhouettes key authored sky organically, Mid/Near dissolve into the procedural atmosphere, stable viewport-tiered distance scales prevent camera-driven jump zoom and distinct 0.08/0.18/0.32 parallax ratios create Terraria-like depth without PNG-edge cutoff. The normal Far repeat is back to one Draw, and one redundant background SpriteBatch End/Begin boundary is removed.
- Completed Tree V2 with twelve deterministic root/branch/crown silhouettes and three connected 16-mask Forest oak, Amber autumn and Marsh foliage atlases; negative openings and overlapping clusters replace rectangular crowns and hollow leaf loops. Biome trunk/canopy IDs are data-driven and wind/density-aware falling leaves reuse the fixed particle pool.
- Activated the native 24x40 Wave 06 player across five synchronized 16-pose layers with manifest, runtime animation profile, brief, Imagegen source, deterministic generator, provenance, audit and preview. No legacy frame upscaling is used.
- Added a responsive allocation-free glass status dock with debuffs-first priority, pixel runes and duration strips. The integrated visual-polish scope passes 158/158 in Debug and Release; strict V6/Wave06/foliage audits pass 12/12, 5/5 and 3/3 with zero hard issues.
- Added six Terrain Polish V1 256x16 sheets for Forest grass/loam, layered stone, Amberstone, Marsh moss and Amberwood planks. Every sheet preserves the existing 16-mask 16x16 tile contract and passes its 6/6 supplemental strict audit.
- Added sideview coyote time (0.10 s), jump buffering (0.12 s), early-release variable jump height and a fixed-step button latch so short high-refresh presses are not lost; overlay/respawn transitions clear the latch and held jump does not create automatic bunny hopping.
- Added `LiquidPresentationPlanner` with a fixed 8,192-command buffer, bounded 65,536-tile scan and coalesced body/depth/surface/shore runs prepared in LateUpdate. Forest/Amber/Marsh/Crystal palettes share the reflection contract; 1080p preparation averages 0.253-0.384 ms at 0 B.
- Hardened snapshot ownership after integration: public immutable lists detach mutable inputs while internal hot paths use an explicit owned-array factory, and the deterministic worldgen hash now covers the twelve branched tree silhouettes.
## Remaining Acceptance Criteria

- Obtain the first hosted Windows and Ubuntu CI results; local validation does not establish hosted graphics, casing or runner behavior.
- Continue reducing the remaining 3,125 B quick / 2,690 B calibration fixed-tick allocation without leaking mutable snapshot storage; previous-frame interpolation and remaining snapshot columns remain open.
- Extend the tile atlas into compatible entity/UI categories, add actual GPU timestamps and calibrate GPU-memory/residency budgets; CPU-side submission and texture-switch counters are captured.
- Attach bounce/pierce/expire/destroy adapters to every authoritative projectile terminal path, capture the visual combat matrix and add real production audio files.
- Extend the new cold/warm settle distribution with labelled region-read/generate/apply/save service distributions and retain the 500-entity/18,000-tick gates.
- Resolve remaining soft asset debt across legacy enemies, items and secondary UI; the former panorama/foreground/tree/16x32-player scale mismatch is closed by V6 depth planes, Tree V2 and native 24x40 Wave 06.

## Validation Results

| Command or gate | Result |
| --- | --- |
| `dotnet --version` | `8.0.420` |
| `dotnet restore YjsE.sln --locked-mode` | passed; all four projects current |
| Scoped `dotnet format YjsE.sln --verify-no-changes --no-restore --include ...` | passed after line-ending normalization for the current render-graph/atlas/blur/entity-submission and PhysicsWorld/entity-integration scope |
| Debug solution build | post-integration passed, 0 warnings, 0 errors |
| Release solution build | post-integration passed, 0 warnings, 0 errors |
| Debug full suite | passed 1381/1381 after snapshot, streaming, renderer, parallax, movement/physics, active-liquid, combat-query, generation-workspace and visual-polish integration |
| Release full suite | passed 1381/1381 after snapshot, streaming, renderer, parallax, movement/physics, active-liquid, combat-query, generation-workspace and visual-polish integration |
| Release build/publish | warn-as-error CI build passed; isolated publish contract passed under the local temporary directory |
| Focused streaming contracts | 38/38 passed in Debug and Release; retry, backoff, terminal classification, stale/cancel behavior and operation/time/byte apply budgets covered |
| Focused attack runtime contracts | pass inside both full suites; authored melee/ranged/magic item references, atomic resource spend, phase order, buffering, combos, cancels, active-window shapes/projectiles, snapshot state and 0 B steady sequencing covered |
| Focused replay/feedback contracts | 37 focused tests pass; version/validation/serialization/divergence plus bounded visual/audio routing and 0 B drain path covered |
| Focused animation/entity visual contracts | pass inside full suite; deterministic fixed-tick clocks, rigs, bounded commands and 200-entity preparation covered |
| Focused combat/projectile contracts | pass inside full suite; guard, parry, break, exact-once, friendly fire, lifetime, homing, pierce and bounce covered |
| Focused physics/liquid/query/generation performance | 11/11 Release contracts pass; 1,000-body physics and overlap, 128 active liquid cells, 500-projectile combat and 256x128 generation remain inside CPU/allocation budgets |
| Focused central physics/spawn integration | 35/35 pass in Debug and Release; includes complete enemy/item batches, body-pair impulses/correction, capacity fail-fast, initial-overlap recovery, negative-X collision and the repeated 18,000-tick soak |
| Focused compiled render-core contracts | render graph, atlas planner, backdrop planner and entity submission pass inside both full suites; graph compile/execute, planner and representative 200-actor steady paths assert 0 B |
| Focused spawn/AI scale contracts | 65/65 pass in Debug and Release; negative/positive X, loaded offscreen ingress, local surface/open sky, encounter fallback, caps/despawn, null AI, unloaded-boundary physics, a 90-second integration, repeated 18,000-tick streaming soak and 500-entity gates covered |
| Focused renderer/UI contracts | 241/241 pass in Debug and Release; includes LateUpdate cardinality, cadence/admission scheduling, telemetry/limiting, 1/3-sample ray masks, fractional penumbra, reflection radiance, procedural background/particle budgets, responsive UI layouts and pointer/gamepad interaction |
| Integrated background/tree/character/UI contracts | 158/158 pass in Debug and Release; focused Parallax passes 80/80 and status planning remains 0 B across 10,000 iterations |
| Strict supplemental visual asset audits | background depth planes 12/12, Wave 06 character layers 5/5, Terrain Polish V1 6/6 and loose foliage atlases 3/3 pass with 0 hard issues |
| Strict Python sprite audit v2 | main CI scope passed; 190 IDs, 184 PNG sources, 6 valid alias groups, 0 hard issues; 156 pass and 34 review assets |
| V5 panorama runtime matrix | Forest, Amber Grove, Twilight Marsh and Crystal Depths pass at 1920x1080; Forest also passes at 2560x1440. All report 209 resources, 1,072 frames and 0 invalid resources; rectangular haze/cloud overlays are absent in final captures |
| World Library UI smoke | 640x360 Release capture passed with responsive card/footer containment and 0 invalid resources |
| Deterministic Wave 04/05 preview/provenance | Wave 04 31/31 passes; Wave 05 supplemental report covers 24 assets/122 explicit frames and 0 hard issues, with 24 paths outside the main audit's `sprites/**` disk inventory |
| Release quick benchmark profile | passed; latest physics/worldgen profile fixed tick 0.116 ms average/0.261 ms p99 and 3,125 B average/3,696 B p99 allocation; deterministic replay matched |
| Release calibration benchmark profile | passed; fixed tick 0.102 ms average/0.257 ms p99 and 2,690 B average allocation |
| Representative deterministic replay | integrated calibration passed; 1,200 ticks, matching `0x26927FF799797AC9` state hashes |
| RNG save/resume | passed; named-stream continuation matches after mid-trace `random-state.json` load; backup and legacy paths covered |
| World-event save/resume | 7/7 focused sidecar tests pass; atomic backup, legacy, corrupt/future rejection, registry normalization and deterministic continuation covered |
| Long camera planner trace | passed; 4,097 positions from negative to positive X remain bounded and center-ordered |
| Cold/warm streaming distribution | two 65-position Release runs pass; cold p99 99.3-203.3 ms, warm p99 0.014-0.015 ms, zero over-budget samples |
| Published/client smokes | V5 Forest, Amber Grove, Twilight Marsh and Crystal Depths pass with 209 resources, 1,072 runtime frames, 0 invalid resources, nonblank scenes and full viewport coverage |
| Final V5 renderer smoke | 1920x1080 Forest: 6.061 ms average, 6.062 ms p95, 6.325 ms p99 and 0.609 ms CPU Draw. 2560x1440 Forest: 6.078 ms average, 6.064 ms p95, 8.625 ms p99 and 0.745 ms CPU Draw; both use a 165 cap with 120 warmups/600 measured frames |
| Final V6 visual-polish smokes | Forest/Amber/Marsh pass at 1920x1080 after the final connected-foliage regeneration; Forest passes at 2560x1440 and Amber at 3440x1440. All load 235 resources/1,308 frames/0 invalid. Background Draw averages 0.089-0.113 ms at 1080p and 0.134 ms ultrawide, all 0 B |
| High-refresh frame budget | V5 1080p stays 99.80% within 120 Hz and 99.22% within 144 Hz; V5 1440p stays 98.83%/97.27%. Exact every-frame 165 Hz remains explicitly unclaimed |
| Presentation scheduler micro-gate | 200,000 frames/600,000 cadence decisions: 16.431 ms total, 27.4 ns per decision and 0 B; 10,000 telemetry captures remain 0 B |
| Presentation admission micro-gate | 1,000 budgeted schedules remain 0 B; optional work is deferred while initial/explicit/starved work is forced and telemetered |
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
- 128 active liquid cells: 0.021/0.047/0.069 ms p50/p95/p99 and 0 B/step.
- 500 swept projectile queries: 0.290 ms average, 0.532 ms p99 and 0 B across 180 resolutions.
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

- 500-entity AI/movement update: 4.488 ms p99, 88 B over the measured window and 0.5 B/tick; the pre-fix baseline allocated 31,186.8 B/tick.
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

Raw local artifacts remain under ignored `artifacts/`, including `renderer-upgrade-forest-tuned.*`, `renderer-upgrade-forest-1440p.*`, `renderer-upgrade-crystal.*`, `renderer-upgrade-ui.*`, `performance-calibration-final-integrated.json`, `asset-audit-final.json`, `benchmarkdotnet/**` and the pre-final `smoke-*-final` PNG/JSON reports.

## Known Failures And Blockers

- No local build or test failure remains: Debug and Release both pass 1381/1381. Hosted Windows/Linux graphics, casing and runner behavior remain unverified until this revision completes its first hosted run.
- Full-solution `dotnet format --verify-no-changes` still reports pre-existing mixed newline/charset debt across the dirty tree; the current parallax implementation/test scope passes the same gate after repo-format normalization.
- Website static validation and desktop/mobile browser QA pass locally. Hosting and real artifact downloads remain unavailable.
- Hosted CI is `implemented-unverified`; no commit/push was authorized, so no hosted run exists.
- Background streaming correctness, retry/backoff, terminal classification, operation/time/byte budgets, the long planner trace and cold/warm settle distributions are locally verified; labelled region-read/generate/apply/save distributions and hosted behavior remain unverified.
- The authoritative simulation path is locally verified and its snapshot allocation is roughly halved, but it still allocates per tick and has no previous-frame interpolation contract.
- Session runtime randomness is named and persisted; finite world generation still uses its explicit seed-local `Random(seed)` by design.
- Texture groups, eviction budgets and tile atlas pages are active, but measured GPU memory and cross-category/active-biome packing remain absent; the current final smoke loads 235 resources/1,308 frames and estimates 34,744,320 decoded source RGBA bytes.
- Fixed-tick CPU headroom is strong, but the current quick/calibration representative ticks still allocate 3,125/2,690 B. Snapshot columns and pickup magnetism are improved; combat/projectile query ownership is caller-owned and allocation-free in its representative fixture.
- Current V5 1080p/1440p CPU presentation pacing is locally verified at the 120-165 Hz target range, and CPU-side draw/texture-switch counters are captured. Actual GPU timestamps, the complete 1440p quality/biome matrix and refresh-rate enumeration remain open.
- Final V5 Forest lighting-mask averages 0.535/0.601 ms at 1080p/1440p and upload CPU scope 0.370/0.210 ms, both inside their average targets. Rare process/upload peaks remain and are not presented as GPU time.
- Advanced world-event execution, phases, modifiers, exact-once scheduled/action activation, rare/quantity loot routing, replay input logs/divergence diagnostics and recovery-safe persistence are active. Trigger/room zones and production audio remain open.
- Audio mixer/soundscape routing is active, but no production sound files exist, so missing-asset telemetry rather than audible content is the expected fallback.

## Exact Next Action

Add one-way platform and sloped-tile shape contracts plus continuous fast body-body TOI while preserving the current caller-owned 0 B workspaces. In parallel, create water-heavy Amber/Marsh fixtures and accept the prepared shore/depth visuals at 1080p/1440p. Then complete the four-biome 720p/1440p jump/mining comparison, active-biome background residency and remaining inventory/crafting migration to the status-dock material language. Retain full Debug/Release, 0 B background/liquid/status planners and renderer p95/p99 gates.

## Recommended Subagents

- Cross-Biome Visual QA: traversal/jump/mining captures at 720p/1440p and opacity/order comparison for all V6 planes.
- Legacy Asset Polish: audit and replace the highest-impact enemy/item/UI sprites with native dimensions and complete provenance.
- Water Visual QA: deterministic water-heavy fixtures and 1080p/1440p shoreline/depth acceptance for the now-prepared presentation path.
- Combat Visual QA: terminal projectile adapters and visible melee/ranged/magic/guard/encounter capture matrix.
- Performance/Integrator: active-biome residency, 1440p tier measurements, architecture boundaries and full truth gates.

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
