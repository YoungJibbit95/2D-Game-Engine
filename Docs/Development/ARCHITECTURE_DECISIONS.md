# YjsE Architecture Decisions

Last updated: 2026-07-19

Decision status values are limited to `verified`, `implemented-unverified`, `partial`, `planned`, `blocked`, and `deprecated`.

## ADR-0001: Pin The Stable .NET 8 SDK Exactly

Status: `verified`

### Context

The repository targeted .NET 8 but requested SDK 8.0.100 with `latestMajor` roll-forward. Tool selection could therefore vary by machine and select a later major SDK. The Windows host used for the audit selected 8.0.420.

### Decision

Pin SDK 8.0.420 in `global.json`, disable roll-forward, and disallow prerelease SDKs. CI installs the SDK described by `global.json`.

### Alternatives

- Keep `latestMajor`: rejected because compiler, analyzer and SDK behavior can drift across machines.
- Use `latestPatch`: rejected for the initial control-plane baseline because it still permits unreviewed SDK movement.
- Move to a newer target framework: rejected because it changes product/runtime compatibility beyond Epic 0.

### Consequences

- Local and hosted builds require SDK 8.0.420 exactly.
- SDK upgrades become explicit reviewed changes.
- Machine-specific targeting-pack path overrides are no longer part of shared build configuration.

### Migration

Install 8.0.420 locally or let the CI setup action install it. Remove IDE-specific assumptions that a newer SDK is acceptable merely because it can target net8.0.

### Validation

Record `dotnet --version` and `dotnet --info` locally and in Windows/Ubuntu CI. Run Debug/Release build and tests after the pin.

### Rollback

Revert the `global.json` change only if 8.0.420 cannot be obtained from supported distribution channels. Select another exact stable .NET 8 SDK; do not restore `latestMajor`.

## ADR-0002: Use A Stable C# 12 And .NET 8 Analyzer Policy

Status: `verified`

### Context

`LangVersion=latest`, warnings not treated as errors, and a whitespace-only editor configuration did not define a reproducible quality gate.

### Decision

Use C# 12, enable the stable SDK .NET 8 analyzer baseline, treat warnings as errors, and run a warning-severity `dotnet format style --verify-no-changes` gate in CI. The initial stable style rule requires file-scoped namespaces. Deterministic builds are explicit; CI builds set `ContinuousIntegrationBuild`.

### Alternatives

- Use `latest` language/analyzer behavior: rejected because it changes with SDK selection.
- Add a broad third-party analyzer package immediately: deferred until the built-in policy is green and its marginal value is measured.
- Keep warnings advisory: rejected because CI would permit known regressions.

### Consequences

- Existing warnings or formatting drift can make the initial workflow red.
- Analyzer changes require an explicit policy revision instead of arriving with a different major SDK.
- Local builds receive the same warning policy as CI.

### Migration

Run format verification and both build configurations. Fix actionable diagnostics in coherent ownership scopes; do not blanket-suppress warnings to make CI green.

### Validation

`dotnet format style YjsE.sln --verify-no-changes --no-restore --severity warn`, followed by Debug and Release builds with no warnings.

### Rollback

If one analyzer rule is demonstrably unsuitable, document a narrow rule-specific severity decision in `.editorconfig`. Do not disable analyzers or warnings-as-errors globally.

## ADR-0003: Validate Windows And Linux In A Debug/Release Matrix

Status: `implemented-unverified`

### Context

The repository had no tracked CI. Windows-only local evidence cannot reveal Linux path casing, separator, native dependency, or runtime differences.

### Decision

Use one GitHub Actions matrix across `windows-latest` and `ubuntu-latest`, with Debug and Release configurations. Each job installs the pinned SDK, restores, builds, runs explicit focused gates, runs the full suite, and uploads TRX results. Format verification runs once per OS in the Debug leg.

### Alternatives

- Windows-only CI: rejected because the DesktopGL/content path is intended to be cross-platform.
- One Release job only: rejected because Debug and Release are both Definition-of-Done gates.
- Separate divergent scripts per OS: rejected until an OS-specific command is necessary.

### Consequences

- Four validation legs increase CI time but make OS/configuration failures explicit.
- Focused gates repeat tests also present in the full suite; this is intentional for discoverability.
- Hosted CI remains `implemented-unverified` until the first recorded run.

### Migration

Land the workflow, observe the first matrix without weakening gates, and fix platform-specific issues in bounded slices.

### Validation

A complete run must produce four successful matrix legs and their test-result artifacts.

### Rollback

If a hosted image has a confirmed external outage, pin a supported image temporarily and document the blocker. Do not remove Linux or Release coverage as a convenience.

## ADR-0004: Keep Focused Contract Gates Beside The Full Suite

Status: `partial`

### Context

The initial suite was broad, but asset, repository content, and negative-X requirements were not visible as named CI gates. A full-suite failure could obscure which product contract regressed.

### Decision

Run explicit filters for sprite assets, content/cross-references, and negative-X coordinate/generation/streaming/liquid/structure/save behavior before the full solution suite.

### Alternatives

- Rely only on the full suite: rejected because contract ownership and CI diagnostics remain opaque.
- Create new test projects immediately: deferred because solution and test-project ownership is reserved for coordinated later work.
- Replace the full suite with filtered tests: rejected because filters are not comprehensive regression coverage.

### Consequences

- CI repeats a small subset of tests.
- Filter names become part of the control-plane contract and must change when namespaces move.
- A focused green result never replaces the full-suite requirement.

### Migration

Keep the current tests in place, make hard asset failures comprehensive in a later test-owned slice, and update filters together with any namespace migration.

### Validation

Each matrix leg must emit `assets.trx`, `content.trx`, `negative-x.trx`, and `full.trx`.

### Rollback

If a filter becomes invalid, repair it or replace it with a purpose-built executable gate. Do not silently delete the covered acceptance criterion.

## ADR-0005: Require A Deterministic Nonblank Client Smoke

Status: `partial`

### Context

`Game.Client` can build without proving that DesktopGL starts or that visible pixels reach the backbuffer. A bounded client-owned smoke contract now exists and passed once locally on Windows; hosted Windows and Ubuntu evidence remains pending.

### Decision

Use the client-owned smoke mode with deterministic settings and `GameProjectContentLoader`, publish the manifest/runtime content outside the repository, render three bounded frames, and capture the backbuffer. Exclude the synthetic sample panel from the scene threshold, validate real alpha in required source rectangles and changed pixels in their target rectangles, write PNG/JSON diagnostics, enforce an internal wall-clock deadline, and exit with a meaningful code. Execute it directly on Windows and under a virtual display on Ubuntu.

### Alternatives

- Treat successful compilation as rendering proof: rejected.
- Use an exact screenshot hash: rejected because driver-level raster differences make it brittle.
- Add a graphics dependency to `Game.Core`: rejected because it violates the renderer boundary.

### Consequences

- The smoke needs controlled filesystem paths, a timeout, and graphics-runner setup.
- CI invokes the real target only in Release legs and uploads its diagnostics.

### Migration

Validate the implemented target locally, then validate both hosted CI OS paths before changing the capability to `verified`.

### Validation

The artifact records project/adapter identity, dimensions, scene nonblank pixels, color count, required source/target sprites, texture telemetry and exit status. An all-black scene, transparent sample, placeholder, invalid texture, crash or timeout fails the gate. Startup duration remains a separate future measurement.

### Rollback

If cross-driver thresholds are unstable, adjust the semantic threshold using recorded artifacts. Do not replace the smoke with a process-start-only check.

## ADR-0006: Separate Benchmarks From Correctness Tests

Status: `verified`

### Context

`PerformanceProfilerTests` validate profiler calculations but do not provide reproducible engine baselines. A lightweight benchmark executable now exists and passed once locally on Windows, but no durable or hosted result exists yet.

### Decision

Use the Release `Game.Benchmarks` quick harness for fixed simulation, content loading and representative simple world generation. Release CI jobs write and upload its JSON report. Expand later into infinite generation, streaming and persistence scenarios; keep client-render measurements in the separate smoke/telemetry path.

### Alternatives

- Put timing assertions in xUnit tests: rejected because shared runners and JIT variance make them flaky.
- Treat runtime rolling averages as baselines: rejected because the scenario and environment are uncontrolled.
- Gate performance immediately on uncalibrated thresholds: rejected.

### Consequences

- Performance fields remain `unknown` until artifacts exist.
- Benchmark scenario revisions require explicit metadata and comparison boundaries.

### Migration

Run the implemented quick harness locally and on both hosted OS paths, record its first non-gating output, then add deterministic streaming and persistence fixtures.

### Validation

Results include commit/tree state, hardware, OS, SDK, configuration, scenario version, warmup, samples, timing distribution and allocations.

### Rollback

If the initial framework is unsuitable, retain the scenario contracts and raw evidence while replacing the runner. Do not fold wall-clock assertions into correctness tests.

## ADR-0007: Commit Per-Project NuGet Lockfiles

Status: `verified`

### Context

An exact SDK does not prevent transitive NuGet dependency resolution from changing when package feeds evolve. The solution graph is changing during Epic 0, so lockfiles must be generated from the final integrated project graph rather than from an intermediate subagent checkout.

### Decision

Set `RestorePackagesWithLockFile=true` centrally, commit each project's `packages.lock.json`, and require `dotnet restore --locked-mode` in CI. The lead generates the lockfiles after the final project graph is integrated.

### Alternatives

- Rely only on direct package versions: rejected because transitive resolution can still drift.
- Generate lockfiles before the project graph stabilizes: rejected because the result would immediately be stale.
- Use unlocked CI restore: rejected because CI could validate a dependency graph different from the reviewed checkout.

### Consequences

- Package updates require intentional lockfile diffs.
- Local locked restore is deterministic; hosted restore remains unobserved until the workflow runs.
- Local exploratory restore can regenerate locks, but acceptance uses locked mode.

### Migration

After project-graph changes, intentionally regenerate/review affected lockfiles, then run local and hosted locked restores.

### Validation

`dotnet restore YjsE.sln --locked-mode` succeeds without modifying any lockfile on the local host and both hosted OS families.

### Rollback

If a package source cannot support deterministic locking, document the exact source defect and replace or mirror that dependency. Do not silently remove locked restore from CI.

## ADR-0008: Keep The Sideview Sandbox As The Primary Reference Path

Status: `verified`

### Context

The repository contains Terraria-like, farming, topdown, dialogue and shop foundations. Starting new genre foundations before runtime, renderer, streaming and persistence gates close would fragment validation.

### Decision

Use the Terraria-like sideview sandbox as the primary reference game until its active exit gate is satisfied. Keep other modules compiling and tested without uncontrolled expansion.

### Alternatives

- Advance all genre paths equally: rejected because it spreads production work across incomplete foundations.
- Delete non-primary modules: rejected because they already provide reusable tested capability.
- Embed one concrete game's progression in the engine: rejected because `Game.Data` is replaceable validation content.

### Consequences

- Roadmap priority favors runtime convergence and a coherent sideview vertical slice.
- Non-primary modules can receive maintenance and compatibility fixes, but not unrelated feature waves.

### Migration

Record active runtime usage in the capability matrix and move concrete balance/progression into replaceable game content.

### Validation

The reference slice must use the same core simulation, persistence, content and performance contracts as tests and future games.

### Rollback

Change the primary reference path only through an explicit product decision with revised scope, dependencies and exit gates.

## ADR-0009: Preserve Sprite Pack Provenance And Separate Resources From Frames

Status: `verified`

### Context

The original client cache keyed texture objects by sprite ID and frame. Multiple frames of one PNG could create duplicate GPU resources, first use could perform file work in Draw, and every mod sprite was resolved against one base `Game.Data` root. A mod override could therefore display the base PNG or a placeholder while content validation remained green.

### Decision

`GameContentLoader` stamps each sprite definition with the absolute root of the base or mod pack that supplied it. Authored sprite paths remain relative and must not escape that root. `ClientTextureRegistry` keys resources by canonical source path and frame descriptors by value-type asset/frame keys. It preloads every current frame plus the shared fallback outside Draw, validates decoded dimensions, shares resources across explicit source aliases, records load/allocation/decoded-byte telemetry, and disposes each resource exactly once.

The client smoke must load through `GameProjectContentLoader`, use a physically isolated published project, and validate real source alpha plus rendered target pixels. A synthetic panel cannot satisfy the scene nonblank threshold by itself.

### Alternatives

- Keep one global content root: rejected because mod overrides lose source provenance.
- Resolve paths only during Draw: rejected because it reintroduces disk I/O and allocation into the render path.
- Create one texture per frame: rejected because sheets must share one resource and source rectangles.
- Silently clip wrong PNG dimensions: rejected because corrupt/custom content would appear real while violating its contract.
- Preload nothing and depend on perfect content references: rejected because the system fallback would still allocate on first unknown lookup.

### Consequences

- At the ADR adoption checkpoint startup loaded all 124 PNG sources plus one fallback and materialized 683 frame descriptors before rendering.
- Mod overrides and mod-only sprites load from their actual packs.
- Wrong dimensions, absolute paths and root escapes fail with actionable diagnostics.
- Correctness was locally verified, but all-content residency was not scalable to large asset waves and has since been replaced by explicit residency groups, decoded-byte budgets, pinning and LRU eviction.
- Atlas metadata still does not implement an atlas runtime, and decoded bytes are not GPU-memory accounting.

### Migration

Keep `SourceRoot` runtime-assigned rather than JSON-authored. Preserve stable sprite IDs and use `sourceAliasOf` only when roles intentionally share one canonical source. The `PreloadAll` migration is complete: explicit UI, World, Entities, Backgrounds and Effects groups now enforce decoded-byte budgets while keeping lookup free of Draw-time file work. Atlas pages and measured GPU memory remain later work.

### Validation

- Unit/integration tests cover multi-frame sharing, aliases, fallback sharing, exact-once disposal, wrong dimensions, path escapes, base/mod override roots and post-preload stable frame/resource counts.
- At adoption, local Debug and Release full suites passed 499/499 and the OS-temp published smoke loaded 125 resources/683 frames with 0 invalid resources.
- Current post-integration Debug and Release full suites pass 995/995; the published scene matrix loads 199 resources/1,062 frames with 0 invalid resources.

### Rollback

Grouped residency has replaced the eager policy. Retain canonical per-pack resource keys, pre-materialized handles for active groups, strict dimensions and exact ownership. Do not return to frame-owned textures, a single base root, unbounded eager residency or lazy Draw-time file access.

## ADR-0010: Stream Chunks Through Session-Tokened Background Jobs

Status: `verified`

### Context

Infinite-world visibility planning had left `Draw`, but `ChunkStreamingService.Update` still performed region reads, decode, deterministic generation and dirty saves synchronously. Camera reversal or session replacement also had no ownership token, so a later asynchronous implementation could apply obsolete chunks to the wrong world.

### Decision

Create an immutable request snapshot from visible tiles and loaded chunk positions. Every load/generate/save request and result carries a monotonically increasing world-session generation plus request sequence. Disk read, decode, generation and dirty save run behind `IChunkStreamingJobRunner`; only bounded apply work mutates the live `World` on the update thread. Cancellation is cooperative, stale results are rejected, unfinished cancelled jobs still count against concurrency, dirty chunks are snapshotted and compared before clean/unload, and existing region/legacy save formats remain unchanged.

### Alternatives

- Run all work synchronously in Update: rejected because storage and generation stalls scale with camera movement.
- Mutate live chunks from worker threads: rejected because simulation, rendering and save ownership are main-thread contracts.
- Accept completion by position only: rejected because world replacement and camera reversal can reuse coordinates.
- Clear dirty flags immediately after scheduling save: rejected because edits during the save would be lost.

### Consequences

- Load and save concurrency, apply queue length and per-update apply operations are bounded independently.
- Telemetry reports jobs, deferred work, bytes, operations, cancellation, stale/failure counts and cumulative timings.
- Background failures do not escape through `Task.Result` into the game loop; classified retry/backoff and terminal telemetry are defined by ADR-0019, while durable user-facing error reports remain future work.
- Apply work is operation-bounded; elapsed-time and byte budgets remain a later performance refinement.

### Migration

Callers continue using `ChunkStreamingService.Update`; existing save directories require no migration. Session hosts must cancel pending work on disposal and must never apply chunk results outside the service.

### Validation

Twenty focused tests cover pure snapshots, negative X, rapid camera reversal, cooperative and uncooperative cancellation, session replacement, stale results, load/decode, deterministic generation, dirty save/unload, edits during save, queue/apply budgets and failed jobs. Debug/Release full suites and the published client smoke pass locally.

### Rollback

Keep the request/result/session-token contracts even if the job scheduler changes. Do not reintroduce live-world mutation on background threads or synchronous storage/generation in Draw.

## ADR-0011: Make The Loaded Session Own One Authoritative Simulation

Status: `verified`

### Context

`GameSimulation` covered combat, spawning, respawn and world simulation in tests, while the active client separately updated time, farming, player, entities, item use and pickups. Activating both paths would double-advance shared state and leave server, replay and save boundaries ambiguous.

### Decision

`LoadedGameSession` owns exactly one `GameSimulation` and validates reference identity for every authoritative runtime object. `PlayingState.Update` latches commands and item-use requests; its fixed step either performs no tick while paused or submits one simulation tick. Core captures an immutable renderer-neutral frame snapshot after the ordered phase pipeline. The client consumes that snapshot for player, entity, farm, time and HUD drawing while retaining the one live world only for tile rendering, lighting, streaming, commands and persistence.

World clock persistence is additive: `simulation.json` format v1 stores day, time-of-day and day length. Missing files are the supported legacy migration path. Gameplay state hashes exclude derived chunk-cache/save telemetry and compare serialized gameplay state across save/resume.

### Alternatives

- Keep client and core orchestrators: rejected because two live simulations can advance the same object graph.
- Move rendering/input types into Core: rejected because it violates the renderer-neutral boundary.
- Render all systems directly from mutable runtime objects: rejected for player/entities/time because it prevents a stable replay, server and interpolation boundary.
- Put world time into player or chunk metadata: rejected because it is session simulation state.

### Consequences

- Combat, contact damage, spawning, respawn and world simulation are now active in the same path as player movement and item use.
- Pause skips the entire tick; overlays suppress pending gameplay actions while the world keeps its established update behavior when not paused.
- Snapshot construction currently allocates about 6.36 KB per representative tick and needs bounded reuse without exposing mutability.
- Default runtime RNG construction is now the main determinism blocker.

### Migration

Client features must submit commands/requests or consume snapshots/events instead of updating gameplay systems directly. New save readers treat absent `simulation.json` as default day one/time zero. Future simulation-state fields require additive format evolution and compatibility tests.

### Validation

Automated tests cover phase order, no-tick behavior, session identity/lifecycle, snapshot immutability, active render entities, runtime options, Core/client boundaries, legacy/new simulation saves and save/resume state hashes. Local Debug and Release pass 532/532; a 1,200-tick dual-session replay matches every 60 ticks; isolated client smoke renders successfully.

### Rollback

Change the internal phase or snapshot implementation if profiling demands it, but retain one session-owned simulation and the renderer-neutral client boundary. Never restore a second gameplay loop in `PlayingState`.

## ADR-0012: Use A Budgeted 2D Presentation Stack Instead Of Claiming Hardware Raytracing

Status: `verified`

### Context

The sideview reference game needs softer sunlight, colored torches, cave residual light, reflections and modern UI effects without turning `Game.Core` into a graphics engine or requiring hardware raytracing support.

### Decision

Keep authoritative light values and dirty-region scheduling in renderer-neutral Core. The MonoGame client builds bounded, viewport-sized masks with tile-aware CPU ray casts, point-light occlusion, ambient occlusion, separable penumbra/bloom approximations and cave residual light. Water and wet-surface reflections use a budgeted screen-color capture. UI backdrop blur reuses that prepared capture. Quality tiers clamp mask pixels, rays, samples, blur taps, point lights, reflection surfaces and scene-capture pixels.

This is explicitly a 2D ray-cast/raymarch and screen-space presentation pipeline. It is not DXR, Vulkan ray tracing, path tracing or a physically based global-illumination claim.

### Consequences

- `Draw` only composites prepared resources; resource creation and CPU mask construction stay in Load/Update/fixed preparation.
- Low-quality and disabled tiers remain usable on weaker hardware.
- Occluded light, penumbra, bloom and reflections are artistic approximations over the tile world.
- GPU timing, shader-based acceleration and calibrated 1080p/1440p budgets remain open.

### Validation

Renderer contracts cover quality clamping, pass planning, 0 B steady-state mask construction, visible-light collection, reflection-surface planning and deterministic particles. Five isolated Release scene smokes pass at 640x360. Hardware raytracing is neither required nor advertised.

## ADR-0013: Advance Animation On Fixed Ticks And Render Prepared Layer Commands

Status: `verified`

### Context

Player and creature animation had accumulated client-time helpers that could drift from gameplay and could not compose armor, tools, shields or action locks reliably.

### Decision

`Game.Core.Animation` owns fixed-tick clips, loop modes, events, layered state machines, blends, action locks and renderer-neutral character rigs. Frames of one PNG remain source rectangles over one resident texture. The client prepares player rig poses and bounded entity visual commands outside `Draw`; `Draw` transforms those commands through the camera and resolves only resident sprite/frame keys.

### Consequences

- Guard, tool, hurt and locomotion states share the authoritative snapshot clock.
- Character appearance, armor and equipment are independent layers instead of baked full-character textures.
- Entity shadows, elite outlines, hit flashes, bob, squash/stretch and velocity rotation are client presentation, not gameplay state.
- The legacy sprite-animation adapters remain migration tools, not a second live player clock.

### Validation

Animation and entity-visual suites cover deterministic fixed-tick playback, event cursors, action locks, rig attachments, legacy conversion, guard snapshot mapping, bounded command buffers and 200-entity preparation.

## ADR-0014: Spawn Around Activity Sources, Not A Global Spawn Point

Status: `verified`

### Context

Spawn attempts centered on world spawn made long travel empty and coupled the system to one player. Future split-screen, multiplayer and spectator cameras require more than one active region.

### Decision

`SpawnScheduler` consumes bounded `SpawnActivitySource` snapshots. Each source supplies focus tile, visible bounds and living-world environment. Candidate rings enforce minimum/maximum distance, optional viewport exclusion, loaded chunks, solid ground, air/liquid/collision suitability and deterministic rule streams. Local, regional and global caps plus protected/engaged despawn rules bound population.

### Consequences

- The current single-player simulation submits one player/camera-derived source; the API accepts multiple sources.
- Biome, vertical layer, time, weather, world event and habitat can affect weights without client authority.
- Friendly and hostile AI share perception memory, home return and telemetry contracts.

### Validation

Negative/positive-X distribution, caps, liquid rejection, protected despawn and 200-entity soak tests pass. The BDN dry smoke for two activity sources and 200 entities completed in 3.646 ms with 64 B for its single measured operation; this is a smoke value, not a calibrated production distribution.

## ADR-0015: Treat Pointer Semantics And UI Effects As Engine Contracts

Status: `verified`

### Context

Settings and menus required excessive clicking, ambiguous press behavior and ad hoc visual treatments. Keyboard-only focus also left mouse and future gamepad flows inconsistent.

### Decision

Client UI uses release-inside activation, pointer capture, hover/pressed/disabled/focus states, drag sliders and scroll controls. Keyboard and gamepad navigation remain fallbacks over the same control model. `GameSettings` owns accessibility and UI-effect values; `UiTheme` renders bounded rounded geometry, stepped gradients, glow and shadows, while backdrop blur consumes the prepared scene capture under presentation quality budgets.

### Consequences

- Menus, world selection, creation and pause/settings share interaction semantics.
- Every numeric option can use direct values and drag controls instead of repeated button clicks.
- Rounded corners and blur are real render paths, but font shaping remains the current bitmap-font pipeline.

### Validation

UI tests cover pointer capture, release-inside clicks, sliders, dropdowns, toggles, focus, gamepad repeat, tooltips, settings round trips and guard HUD behavior. Main-menu and playing smokes pass locally.

## ADR-0016: Resolve Phased World Events Once In The Living-World Runtime

Status: `verified`

### Context

The repository had both a lightweight scheduled event state and a richer executor with phases, cooldowns, modifiers and a bounded journal. Keeping both active would allow spawning, lighting, particles and audio to observe different event truth, while evaluating the richer scheduler every fixed tick would spend work on a world-scale decision that changes much less frequently.

### Decision

`GameContentDatabase.WorldEvents` owns base/mod-merged, cross-reference-validated definitions. `LivingWorldRuntime` owns one `DeterministicWorldEventExecutor`, one runtime snapshot and one bounded journal. It advances scheduled events at most once every 60 simulation ticks, or immediately when relevant weather/night/underground context changes. Successful Mine/Build/Melee/Shoot/Cast/Consume/Farm actions enter through a monotone sequence contract and are evaluated once with a coordinate-hashed deterministic chance. The runtime composes root and phase modifiers once and writes spawn density, sky/ambient light, weather intensity, color grade, particle, soundscape, quantity-loot and rare-loot values into the immutable living-world frame snapshot.

`WorldEventStateSaveService` persists the aggregate runtime snapshot, cooldowns, bounded journal and last processed action sequence in additive `world-events.json` format v1. Writes use temp-plus-atomic-replace with backup recovery; missing files are the legacy default path, malformed or future versions are rejected, and removed mod definitions are normalized out during restore. Rare/quantity loot modifiers are applied through one `LootKillContext` in authoritative item-use, melee, projectile and death-lifecycle paths, never re-evaluated in the client.

### Consequences

- Spawning, lighting, parallax/atmosphere, particles and soundscape selection observe the same event ID, phase, progress and intensity.
- The 60-tick cadence bounds world-event scheduler cost while preserving deterministic context-triggered transitions.
- Mods can override event definitions through the normal content pipeline; unresolved biome/event/soundscape references are validation errors.
- Duplicate or replayed action sequences cannot retrigger an event or add a second journal entry; save/resume preserves this boundary.
- Trigger/room zones and replay input-log diagnostics remain separate future contracts.

### Validation

Post-integration Debug and Release pass 995/995. Focused runtime/event/save tests cover base/mod loading, reference validation, activation, phase changes, cooldown, bounded journal restore, atomic backup recovery, legacy/future formats, removed-mod normalization, exact-once scheduled/action activation, rare/quantity loot and deterministic save-load-advance continuation. The current 240-tick quick replay matches every checkpoint and ends at `0x6D481199215B363A`.

## ADR-0017: Bound Gameplay Presentation At The Event Adapter

Status: `verified`

### Context

Authoritative gameplay already emitted typed events, but client particles and sound could accumulate through ad hoc collections or duplicated callbacks. Rare loot and scheduled world-event activation also lacked one explicit presentation boundary.

### Decision

`GameplayFeedbackRouter` is the renderer-neutral adapter from authoritative events to two fixed-capacity rings: visual cues and audio cues. Overflow deterministically drops the oldest command and increments telemetry. The client drains into caller-owned arrays, emits particles and submits audio IDs; missing sound assets remain a client-registry fallback. Entity death paths publish `LootDroppedEvent`, and `GameSimulation` publishes scheduled or player-triggered `WorldEventActivatedEvent` exactly once.

### Consequences

- Gameplay systems remain unaware of MonoGame particles, textures and sound devices.
- Mining, placement, hits, deaths, drops, pickups, crafting, status/resources and world events use one bounded presentation path.
- Concrete production audio files and some guard/projectile lifecycle cues remain content work rather than simulation authority.

### Validation

Focused feedback/death/simulation tests cover normal and rare drops, world positions, exact-once activation, overflow telemetry and a 0 B steady-state caller-buffer drain.

## ADR-0018: Record Replay Inputs As Versioned Bounded Diagnostics

Status: `verified`

### Context

Named RNG snapshots and state hashing proved two prepared simulations could match, but there was no portable record of the commands that produced a run or typed explanation of the first mismatch.

### Decision

Replay frames contain format version, tick, monotone sequence, fixed-step delta, player command, optional item-use request and optional checkpoint hash. `ReplayRecorder` is a fixed-capacity chronological ring with explicit drop count. JSON serialization has a 64 MiB limit and validates versions/order/numerics. `ReplayComparer` reports the first missing, extra, reordered, input, checkpoint, hash or version divergence plus the last matching checkpoint. `GameSimulation` exposes explicit start, snapshot and stop capture methods; capture and hashing are disabled by default.

### Consequences

- Variable render frame rate does not affect the recorded authoritative fixed-step input stream.
- Diagnostic capture has bounded memory and periodic hash cost is opt-in.
- Playback orchestration and persistence beside saves remain separate future UX contracts.

### Validation

Twenty focused replay tests pass in the integrated suite, including validation, rollover, snapshot/restore, deterministic JSON, first divergence and 0 B recorder writes.

## ADR-0019: Retry Streaming Work Without Surrendering Apply Budgets

Status: `verified`

### Context

Background chunk jobs could report failures but transient I/O faults were terminal, and operation count alone could not prevent one update from applying too much decoded data or spending too much wall time.

### Decision

Streaming classifies failures as retryable, cancelled, stale or permanent. Retryable load/generate and save jobs use bounded exponential update-backoff and terminal exhaustion; reset is explicit. Main-thread application is independently limited by operations, elapsed time and decoded bytes. One oversize item may advance when no prior item was applied so byte budgets cannot starve a chunk forever. Persistent World settings expose concurrency, queue, apply and retry limits.

### Consequences

- Camera/session cancellation and stale-result rejection remain stronger than retry intent.
- Transient failures do not spin each frame, permanent failures do not retry, and terminal suppression is observable.
- A scripted time source makes elapsed-budget behavior deterministic in tests; production uses monotonic time.

### Validation

Thirty-eight focused streaming tests pass in Debug and Release. They cover load/save retries, backoff, exhaustion/reset, classifications, operation/time/byte deferral, oversize progress, cancellation, stale results, negative X and dirty-save ownership.

## ADR-0020: Keep Character Intent Outside The Physics Core

Status: `verified`

### Context

Character and topdown controllers previously lived in `Game.Core.Physics`, while player, enemies and items each mixed intent, gravity and tile resolution. That made Physics a gameplay-policy folder and created a double-integration risk for a future central world step.

### Decision

`Game.Core.Movement` owns renderer-neutral control intent and locomotion tuning. `Game.Core.Physics` owns bodies, mass/forces/impulses, gravity, damping, layers/masks, materials, integration, tile collision, contacts and broadphase. `SideViewCharacterController.ApplyIntent` can change desired velocity or request a jump but cannot advance position, apply gravity or query the world. `PlayerEntity` submits its dynamic body to `PhysicsWorld`; architecture tests reject movement/character controllers in the Physics namespace or source folder.

`PhysicsWorld.Step` consumes caller-owned result/contact spans. Body capacity is a hard configuration contract: overflow or undersized result storage fails before mutation. The engine never hides overload by deferring bodies and slowing their authoritative simulation time. `StepWithBodyCollisions` runs the deterministic sweep-and-prune broadphase, resolves AABB narrowphase contacts, mass/material impulses, friction and tile-safe positional correction through caller-owned pair/contact storage. `EntityManager` owns one fixed-capacity batch for enemies and dropped items; ground enemies/items submit dynamic bodies, flying actors submit kinematic bodies, and gameplay classes consume the returned poses instead of applying a second gravity/collision path.

### Consequences

- Player control and physical integration have one explicit boundary.
- Static, kinematic and dynamic bodies share one renderer-neutral contract.
- Player, enemies and dropped items now use explicit PhysicsWorld integration without moving CharacterController policy into Physics. Dynamic/kinematic body pairs use mutual masks and deterministic material impulses. The topdown tile path, inverted/ceiling slopes and joints remain separate additions.
- Moving the public topdown types from `Game.Core.Physics` to `Game.Core.Movement` is a source-level breaking namespace change for external consumers.

### Validation

Focused Debug/Release physics and entity-integration suites cover the architecture boundary, intent-only behavior, forces/impulses, capacity rejection, detailed contacts, high-speed tile sweeps, negative coordinates and 0 B steady paths. The central 500-actor entity update reduced the retained p99 sample from 4.449 ms to a 1.688 ms median across three runs; a 1,000-body Release step measures 0.988 ms and the broadphase 0.331 ms, both at 0 B.

## ADR-0021: Keep Liquids As A Budgeted Active Frontier

Status: `verified`

### Context

Scanning every dirty region on each liquid tick made cost proportional to world area even when only a few cells could move. Initial seeding also bypassed the new simulation budget and could stall the first fixed tick.

### Decision

Each liquid runtime owns a bounded `LiquidSimulationWorkspace` with deduplicated FIFO active cells, incremental seed-region cursors and changed-region telemetry. Active-cell, transfer and seed checks have independent hard per-step budgets. Initial finite worlds enqueue one bounded region and infinite worlds enqueue loaded chunk bounds; tile scanning occurs only inside the liquid step budget. The scheduler continues pending liquid work without new dirty input, rebinds safely on world replacement and consumes changed-region views in the same tick. Capacity loss is telemetered and changed regions are re-enqueued for bounded recovery.

### Consequences

- Steady liquid work scales with active cells instead of total region area.
- Unloaded neighbors are never materialized by flow and negative/infinite coordinates remain supported.
- `ChangedRegions` is a workspace-owned tick-local view; long-lived consumers must copy it.
- Pressure, viscosity/material types, sources/sinks, buoyancy and renderer-facing surface data remain separate additions.

### Validation

Flow, conservation, determinism, fairness, budget, capacity, world-rebind, negative-X and unloaded-boundary contracts pass in both full suites. The final isolated 128-cell Release distribution is 0.021/0.047/0.069 ms p50/p95/p99 and 0 B per step.

## ADR-0022: Materialize Finite Worlds Through Chunk-Local Generation Storage

Status: `verified`

### Context

Finite initial generation used the normal runtime tile-edit path, repeatedly marking/merging dirty regions and materializing temporary edit collections even though no renderer, saver or simulation consumer could observe the half-built world.

### Decision

Finite generation passes write through a bounds-safe `WorldGenerationWorkspace` that caches chunk tile arrays lazily and publishes clean materialized chunks only after generation completes. Terrain, caves/caverns/pools, ore, walls, lakes, structures, trees and shared tile-mutation helpers use the workspace. The contract rejects horizontally infinite worlds; streaming generation and normal runtime edits retain their existing dirty/event ownership.

### Consequences

- Initial generation avoids runtime dirty-region churn without weakening runtime mutation semantics.
- Custom finite steps remain interoperable and deterministic simple/advanced hashes are unchanged.
- Infinite/regional generation workspaces and large-world memory curves remain future work.

### Validation

Focused Debug/Release generation scopes pass 49/49, including bounds, chunk cleanliness, custom-step interoperability, infinite rejection and unchanged deterministic hashes. The latest 256x128 quick fixture measures 0.694 ms average, 0.862 ms p95 and 1,252,512 B, down from 28.044 ms and 17,052,112 B.

## ADR-0023: Compile A Bounded Presentation Graph And Reduce Material Churn

Status: `verified`

### Context

The active Playing renderer had an implicit pass order, tile commands grouped by their original source textures, entity shadows interleaved with actors and an eight-tap full-resolution UI blur fallback. This was workable, but it made new render techniques harder to schedule and spent submission/sample budget on state changes that did not improve the image.

### Decision

`PlayingState` declares and compiles a fixed-capacity render graph with stable numeric pass/resource IDs, explicit phases, resource reads/writes, dependency validation, output culling and transient lifetime/alias telemetry. The compiled plan executes through a generic struct executor without delegates, boxing, LINQ or per-frame graph rebuilding. Lighting configuration changes invalidate and recompile the plan outside the steady Draw path.

Tile animation frames are packed once during content configuration into CPU-baked padded atlas pages; Draw consumes source rectangles from the atlas and never performs texture readback, file IO or atlas construction. Entity presentation keeps stable shadow-before-actor semantics while grouping actor material/texture runs through a fixed-capacity submission plan with a lossless overflow fallback. Open-overlay backdrop blur downsamples by quality tier and prepares a bounded Kawase ping-pong chain once per scene capture instead of sampling the full-resolution scene eight times every overlay draw. Fullscreen effects and shader registries use real `Effect` binding plus stable handles rather than ignoring the effect argument or resolving string IDs in the hot path.

### Consequences

- Pass dependencies and invalid graphs fail before rendering, while pass execution remains allocation-free.
- Tile atlas pages reduce tile texture buckets without changing registry ownership of non-tile resources; a general multi-category atlas and active-biome residency remain future work.
- Screen-space scene/ping/pong alias slots now map to a fixed-capacity physical transient-target pool with descriptor, generation, lifetime and device/resize validation. Pool ownership is still local to `ScreenSpaceEffectsRenderer`; lifting the same contract to all Playing graph resources remains future work.
- MonoGame submission counters remain CPU-side framework evidence; backend GPU timestamps are still unavailable.

### Validation

Graph tests cover cycles, missing producers, phase violations, multiple writers, culling, lifetimes, deterministic aliasing and stale plans. A representative 15-pass Release graph compiles/queries/executes at 33-56 microseconds p50 and 56-78 microseconds p95 with 0 B. Two identical actors reduce estimated texture switches from 4 to 2; a 200-actor alternating fixture reduces them from 400 to 201 at 0 B. The final 1920x1080 Release traversal records 6.061 ms average, 6.061 ms p95, 6.070 ms p99 and 0.574 ms CPU Draw across 600 measured frames, with 0 invalid resources. The prepared High blur plan reduces estimated 1080p sample work from 16,588,800 to 6,220,800 samples per capture (62.5%).

## ADR-0024: Budget AI Decisions Without Deferring Authoritative Physics

Status: `verified`

### Context

Every active enemy previously performed a complete behavior decision on every 60 Hz tick. Spatial indexing removed global scans, but decision cost still grew linearly with actor count and competed with physics, status and lifecycle work. Simply lowering the whole entity update rate would make collisions, knockback, damage and death timing incorrect.

### Decision

`EntityAiDecisionScheduler` uses fixed-capacity caller-owned arrays and a bounded top-K heap to select behavior work. Nearby and engaged actors retain full-rate decisions; mid/far actors use deterministic cadence tickets. Age-first priority prevents starvation and tick/entity-ID ties make overload replayable. Deferred actors accumulate decision elapsed time, while health, status, lifecycle and central Physics continue on every authoritative tick. The scheduler never reapplies cached velocity, so impulses and collision response remain authoritative.

### Consequences

- Large passive populations spend CPU according to a configured decision budget rather than actor count alone.
- Visual/behavior updates can be less frequent at distance without reducing collision or damage fidelity.
- Perception and navigation remain synchronous inside selected decisions; future jobs must preserve deterministic publish order and bounded storage.

### Validation

The AI/entity scope passes 38/38 in Debug and Release, covering cadence, priority, fairness, elapsed-time preservation, lifecycle exclusion, manager transfer and physics-every-tick behavior. Isolated Release measurements reduce 500 actors from 0.995 to 0.371 ms average with 0.886 ms p99 and 0 B/tick, and 2,000 actors from 4.557 to 1.378 ms average with 3.132 ms p99 and 0 B/tick.

## ADR-0025: Use Bounded Fail-Closed TOI For Continuous Body Collisions

Status: `verified`

### Context

Swept tile collision protected fast bodies from terrain, but discrete body-pair overlap could still tunnel when small or fast bodies crossed within one fixed step. An unbounded event solver or hidden allocations would trade correctness for unpredictable frame time.

### Decision

`PhysicsWorld.StepWithContinuousBodyCollisions` accepts caller-owned result, tile-contact, pair, continuous-contact, candidate, sort and sweep-state storage. Swept broadphase generates candidates, deterministic TOI ordering resolves tiles before body pairs, and bounded re-query passes propagate impulse chains. Revision tracking invalidates stale candidates after velocity, position or tile response changes. Capacity and overlapping-span errors fail before mutation. If the configured pass limit leaves unresolved candidates, affected bodies freeze for the unchecked remainder and explicit telemetry reports the fail-closed event instead of advancing through geometry.

`EntityManager` retains ownership of the fixed contact array after the step. It derives a bounded read-only slice from each body's fixed slot and `ContactsWritten`, then passes that transient slice through the physics-participant synchronization contract. Projectile runtime synchronization therefore cannot inspect a neighboring body's stale contacts or retain contact storage; it resolves a supplied authoritative contact first and otherwise performs its deterministic exact tile sweep without allocating a second contact collection.

### Consequences

- Fast dynamic/kinematic bodies can opt into continuous body-pair collision without garbage collection or unbounded solver work.
- Pass and storage limits are visible correctness limits rather than silent tunneling paths.
- Physics participants receive only their own written tile contacts, and the span lifetime makes caller ownership explicit at the API boundary.
- The general runtime still chooses which fast movers use the more expensive continuous step; settled populations retain the discrete path.

### Validation

The Physics scope passes 38/38 in Debug and Release, including negative coordinates, masks/materials, tile-before-body ordering, touching-at-zero, three-body impulse chains, stale-candidate invalidation, pass-limit freezing, span overlap and positional ABI compatibility. A focused projectile/entity/combat Release scope passes 59/59, including neighbor-contact isolation and continuous fast-pair routing. Isolated Release measurements are 0.665 ms/step at 0 B for 500 fast bodies and 6.311 ms/step at 0 B for a dense 128-body/8,128-pair fixture; the 500-projectile synchronization fixture remains 0 B across 180 ticks with 0.562 ms median-run p99 across three fresh processes.

## ADR-0026: Derive Bounded Tile Collision Shapes From Stable Tile Flags

Status: `verified`

### Context

The tile ABI already reserved platform, half-block and generic slope flags, but most collision paths still treated every solid tile as a full AABB. Adding renderer-specific polygons or allocating per-tile shape objects would break the renderer-neutral Physics boundary and make dense collision cost depend on authored content.

### Decision

`TileInstance.CollisionShape` derives one value-type contract from the persisted `ushort` flags: empty/actuated, full block, one-way platform, half block and the two upward-facing slope orientations. The legacy generic slope bit maps to ascending-right, while contradictory orientation bits fail closed as a full block. `TileCollisionResolver` handles upward-facing partial floors through its existing bounded tile-test budget and caller-owned contact span. Falling and supported bodies land on the authored surface, horizontal motion follows it, the high side remains a wall and upward motion passes through; no second collision world or shape allocation is introduced.

Clients that only support full-tile particle colliders must query the same shape contract and accept `FullBlock` only. This prevents a half block, platform, slope or actuated tile from silently becoming a full particle AABB while richer particle shapes remain unimplemented.

### Consequences

- Existing tile/save flag widths remain compatible; no save-format migration is required.
- Floor slopes and half blocks produce deterministic points and normals through the existing contact result.
- Inverted/ceiling slopes, arbitrary polygons and authored partial-tile rendering/content tools remain explicit later slices.
- Tile-test exhaustion remains fail-closed and visible through the established movement telemetry.

### Validation

The focused resolver scope passes 26/26 in Release, including legacy flag mapping, ambiguous-orientation fail-closed behavior, one-way compatibility, both slope orientations, half-block landing, slope following, high-side blocking, upward pass-through and 0 B across 1,000 caller-storage moves. The gameplay-particle scope passes 9/9 and proves that unsupported partial shapes behave like empty particle space while full blocks still collide. The integrated functional suites pass 1,576/1,576 in Debug and Release; all timing-sensitive Release contracts pass inside the complete suite.

## ADR-0027: Separate Solar Transport From Temporal Presentation

Status: `verified`

### Context

Per-pixel directional ray marching made mask cost proportional to ray length, amplified camera jitter at tile boundaries and encouraged stale-light fallbacks that could black out open terrain. A single brightness term also could not distinguish direct sun from diffuse sky energy or weather attenuation.

### Decision

`Game.Core.Lighting.SolarRadianceModel` resolves renderer-neutral sun direction, elevation, direct irradiance, diffuse irradiance and night/weather attenuation. The client samples visible tile geometry into packed caller-owned buffers once, transports directional occlusion through O(mask-pixel) scanlines, applies local AO and finite shadow decay, and uses bounded supercover rays for point emitters. High quality may spread three endpoint samples for fractional penumbra; Medium uses one.

`LightingTemporalStabilizer` reprojects history in world-mask coordinates and rejects samples whose occlusion/depth classification changed. Mining, placement, streamed materialization and unload continue to dirty authoritative light/chunk state; temporal history cannot hide a newly opened shaft. Dynamic entity/projectile emitters enter through one bounded visible-light collector. Wet-surface radiance uses bounded Beer-Lambert absorption and Schlick weighting over the prepared reflection surface plan.

This contract is deterministic CPU 2D ray transport plus screen-space reflection. It must not be advertised as hardware ray tracing or full path tracing.

### Consequences

- Open sky, cave mouths and enclosed rooms can use separate direct/diffuse terms without scanning the whole world.
- Camera movement reuses stable history without smearing across mined or newly occluded edges.
- Material transmission, normal maps, cached room/portal skylight solving and backend GPU timestamps remain explicit later work.
- Draw performs no world sampling, ray construction or texture creation.

### Validation

Focused solar, shaft, stale-light, temporal-disocclusion, dynamic-light, reflection and allocation contracts pass inside both 1,576-test suites. The 104x58 Release fixture averages about 1.05 ms at 0 B. A 600-frame 1920x1080 traversal averages 0.602 ms for mask construction and 0.207 ms for temporal stabilization, both at 0 B; total lighting preparation averages 1.505 ms.

## ADR-0028: Keep Physical Particles Renderer-Neutral And Capacity-Bounded

Status: `verified`

### Context

Client-only visual particles had useful art behavior but no reusable collision/force contract, while putting particle simulation into gameplay entities would duplicate authority and make high counts expensive. An engine intended for several 2D genres needs deterministic debris, sparks, weather-adjacent effects and hit feedback without allocating one object per particle.

### Decision

`Game.Core.Particles.ParticlePhysicsWorld` owns a preallocated slot pool with generation-safe handles, deterministic spawn admission, semi-implicit Euler integration, gravity/wind/drag, swept circle-versus-tile collision, depenetration, restitution, friction, work budgets, fairness, snapshots and bounded collision/expiration events. Spawn/update storage never grows during a step. Unsupported partial tile shapes are treated as empty until the particle collider explicitly supports them; only the shared `FullBlock` collision shape may become a full particle AABB.

`Game.Client.Rendering.GameplayParticleSystem` remains presentation-only. Tile-break, melee-hit and projectile-hit cues may spawn physical debris and consume snapshots/events, but they cannot create damage, loot, world edits or a second live gameplay simulation. Existing ambient and decorative paths retain their independent fixed visual budget.

### Consequences

- The same Core pool can support sideview, topdown and tool/editor previews without MonoGame types.
- Capacity, collision checks, event count and snapshot count are explicit limits rather than hidden collection growth.
- GPU particle expansion can later consume the same spawn/snapshot contract; it is not required for correctness.
- Rich slope/platform/liquid particle collision remains separate from the current full-block adapter.

### Validation

Core contracts cover deterministic replay, generation-safe reuse, forces, tunneling prevention, bounce/friction, budgets, fairness and 0 B steady execution. Client tests prove tile-break and combat feedback use authoritative world geometry without becoming gameplay authority. A 10,000-active-particle Release fixture records 0.181/0.300/0.475 ms p50/p95/p99 and 0 B.

## ADR-0029: Project Authored Backgrounds Horizontally But Never Vertically

Status: verified

### Context

Biome panoramas are finite illustrations with an authored horizon, ground silhouette and transparent lower boundary. Treating them as generic fill textures caused the last source row to stretch or repeat into world depth, producing the vertical color bars captured in Forest and ultrawide scenes.

### Decision

Parallax composition separates horizontal coverage from vertical coverage. A plane may repeat on X using floor-division-safe world anchors, but preserves source aspect and receives one finite destination rectangle on Y. Any uncovered lower region is transparent and falls through to the cave/atmosphere presentation; neither clamping nor a terminal-row fill is permitted. Projection, scale and crop are deterministic for negative X, ultrawide viewports and camera-height changes.

### Consequences

- Authored horizons remain stable and cannot smear into underground space.
- Deep-world presentation is owned by cave/atmosphere layers instead of accidental panorama pixels.
- Every future plane must declare useful source dimensions and transparent coverage.
- Biome art still needs enough distinct planes to avoid obvious horizontal repetition.

### Validation

Projection and regression matrices cover aspect ratios from compact through 3440x1440, negative X, high/low camera positions and transparent source bottoms. Day, night, Frostwood and ultrawide smokes show no vertical repeat or bottom cutoff.

## ADR-0030: Gate Weather By Resolved Biome Capability

Status: verified

### Context

A global precipitation state allowed snow in every biome, while an opaque weather overlay turned a large part of the viewport gray or white. Weather is world state, but the visible precipitation type must remain compatible with the resolved regional biome and its vertical layer.

### Decision

Weather definitions distinguish rain, snow and blizzard, and living-world resolution evaluates biome eligibility before publishing the frame snapshot. Frozen precipitation requires an explicit compatible profile such as Frostwood. The client consumes bounded premultiplied particle commands clipped to the viewport; it may not simulate a second weather state or draw a fullscreen snow-color rectangle.

### Consequences

- Forest and warm/cave regions cannot display snow solely because a global random roll selected it.
- New biomes opt into precipitation through data rather than client conditionals.
- Accumulation, melt and snow depth remain future authoritative surface-state features.
- Weather particles share explicit capacity and culling budgets.

### Validation

Core transition/eligibility suites and client presentation matrices cover clear, rain, snow and blizzard across compatible/incompatible biomes. The Frostwood smoke contains sparse flakes and snow/ice terrain without the former opaque band.

## ADR-0031: Cache Local Surface Resolution For Visible Lighting

Status: verified

### Context

Correct skylight needs the local terrain surface for each visible column. The first correct implementation called the regional planner for every mask column, repeatedly constructing cave, feature and structure plans and causing a periodic 425,984-byte lighting-preparation allocation plus avoidable CPU cost.

### Decision

InfiniteWorldChunkGenerator exposes a surface-height resolver that retains the current WorldRegionPlan and uses a bounded 2,048-entry direct-mapped tile-X cache. Cache keys include the complete signed coordinate, so negative X and hash collisions are safe. A miss computes the exact planner height; a hit returns it without allocation. Lighting combines that local surface with direct solar, diffuse portal and lunar terms. Buffers remain caller/renderer-owned and reusable.

### Consequences

- Visible-column queries no longer regenerate regional plans.
- Memory is fixed and independent of traversal distance.
- Collisions only reduce hit rate; they cannot return a different coordinate's height.
- Generator/profile replacement must create a new resolver rather than mutate one in place.

### Validation

Parity tests compare cached and direct planner results across region boundaries and negative X. Repeated warmed queries allocate 0 B. The final V8 smoke records 0 B light-mask construction, 1.563 ms average mask time and 1.971 ms average total lighting preparation.

## ADR-0032: Rebase Restored World-Event Time Relatively

Status: verified

### Context

World time and event sidecars can be saved or restored independently, and developer commands may rewind time. Advancing an event executor whose last tick is ahead of the restored simulation tick previously threw and crashed a night smoke.

### Decision

LivingWorldRuntime owns AlignWorldEventClock. When the authoritative clock moves backward, it shifts the executor's last tick, active-event start/end, cooldown deadlines and journal timestamps by the same delta. Remaining duration and cooldown progress are preserved. Last presentation context is invalidated so weather, light and event modifiers resolve again on the next authoritative capture.

### Consequences

- Save compatibility remains additive; no sidecar schema change is required.
- Rewinding time does not restart, truncate or duplicate an active event.
- All callers use the runtime boundary instead of editing executor fields.
- Forward time progression retains existing phase and exact-once semantics.

### Validation

Runtime and simulation-constructor tests cover active duration, cooldown, journal timestamps and an event sidecar ahead of simulation time. The previously crashing night smoke completes successfully.

## ADR-0033: Route Developer Actions As Typed Intents

Status: verified

### Context

A useful in-game developer surface needs autocomplete and broad mutation commands, but letting UI widgets modify World, Player or rendering fields directly would duplicate gameplay authority and make commands difficult to test or host outside MonoGame.

### Decision

Core command specifications own names, categories, arguments, validation, help, examples and typed result intents. The client command palette owns pointer/keyboard interaction, filtering, completion, history and presentation only. PlayingState maps accepted intents onto authoritative session services and explicit client rendering/settings adapters. Item/entity/biome suggestions come from loaded registries rather than hard-coded UI lists.

### Consequences

- Command parsing and domain validation remain renderer-neutral.
- The same registry can drive an editor, remote admin shell or test host.
- UI cannot silently create a second gameplay simulation.
- New mutation families require an intent, handler and regression test.

### Validation

Parser, suggestion, help, adapter, layout and Playing routing suites cover command and content completion, history/output navigation, world/time/weather/biome, inventory, spawn, rules, health/mana, projectile, rendering, lighting and profiler families.

## ADR-0034: Separate Functional Correctness From Isolated Performance Acceptance

Status: `implemented-unverified`

### Context

A complete Debug or Release suite shares one long-lived testhost. On the current Windows host, unrelated setup can leave scheduler or process-state residue that produces non-repeatable p99 pauses in later timing tests. Re-running affected classes in fresh processes passes at unchanged thresholds and 0 B. Treating the combined host as the only performance gate therefore conflates functional regressions with host scheduling variance; loosening budgets would hide real regressions.

### Decision

CI runs the same complete functional partition in Debug and Release with all timing classes excluded. Release then uses `tools/run_isolated_performance_tests.ps1` to discover every performance test class and run each class in a fresh `dotnet test` process. High-variance p99 fixtures measure seven independently warmed windows and gate the median while requiring every window to remain allocation-free. Numeric budgets are unchanged. Combined Debug/Release runs remain useful diagnostic evidence, but they are not the authoritative timing acceptance process.

### Consequences

- Functional failures cannot be masked by timing filters, and timing failures retain their original budgets.
- Each timing class starts without accumulated testhost state; this costs additional CI process startup time.
- Hosted Windows and Ubuntu behavior remains `implemented-unverified` until the committed workflow runs there.

### Validation

The local functional partition passes 1,710/1,710 in Debug and Release. The discovery script finds 21 classes containing 39 tests, and all pass in fresh Release processes. The combat-query, dense-scene and AI scheduling gates also pass together across their seven-window medians with unchanged p99 ceilings and 0 B requirements. Combined-host timing outliers remain recorded as diagnostics; no threshold was raised.
