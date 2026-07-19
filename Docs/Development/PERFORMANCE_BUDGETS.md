# YjsE Performance Budgets

Last updated: 2026-07-19 after the compiled render-graph, tile-atlas, submission, backdrop-blur and central entity-physics pass

## Evidence Rules

Performance value states are only `target`, `measured`, and `unknown`. A measured calibration does not automatically qualify a production budget. Every acceptance claim must identify revision/tree state, host, runtime, configuration, scenario, warmup, samples, collection method and raw artifact.

Current performance-baseline capability status: `partial`.

Current evidence host:

- Revision `a05d098f04800d8eb9fce3576efe7f4201c7b20f` plus the current lighting-fix working tree.
- Windows 10.0.26100, AMD Ryzen 5 5500, 6 cores/12 logical processors.
- .NET SDK 8.0.420, runtime 8.0.28, workstation GC, Release.
- Graphics smoke: NVIDIA GeForce RTX 3070 Ti/PCIe/SSE2, including 1920x1080 high-refresh runs.
- Raw ignored artifacts: `artifacts/render-core-final-1080p.json`, `artifacts/render-core-final-blur-1080p.json`, `artifacts/performance-quick-current.json`, `artifacts/performance-calibration-current.json`, `artifacts/performance-quick-physics-2026-07-19.json`, `artifacts/performance-v5-covered-release-*.json`, `artifacts/renderer-upgrade-*.json`, `artifacts/benchmarkdotnet/**` and scene reports.

## Budget Table

| Metric | Target | Measured | Value state | Evidence and limitation |
| --- | --- | --- | --- | --- |
| Simulation frequency | 60 Hz fixed step | 1/60 s authoritative client/harness delta | `measured` | Variable rendering and custom limiting do not replace `FixedUpdateRunner` ownership |
| Fixed Tick CPU average | <= 4 ms | 0.102 ms calibration; 0.100 ms quick | `measured` | Current Release harness after columnar snapshots and query-buffer reuse |
| Fixed Tick CPU p99 | <= 8 ms | 0.257 ms calibration; 0.200 ms quick | `measured` | Both current profiles remain well below the CPU gate |
| Quick-smoke Fixed Tick p95 | <= 4 ms configured gate | 0.133 ms | `measured` | Current Release quick profile, 1,200 iterations after 120 warmups |
| Update Time | <= 8 ms initial client budget | 0.389-0.430 ms average | `measured` | Two Release V3 traversals, 1920x1080/165 cap, real user settings, overlays and scripted movement; rare peaks 9.706-16.564 ms include upload/OS tails |
| Draw Time at 60 FPS | <= 16.67 ms | 1.816-1.852 ms CPU average at 165 FPS | `measured` | Same traversals; peaks 7.355-7.468 ms and CPU scope is not a GPU timestamp |
| Draw Time stretch target | <= 8.33 ms at 120 FPS | 6.166-6.231 ms average frame interval | `measured` | p95 6.075-7.138 ms, p99 10.722-11.418 ms and maximum 12.518-13.354 ms across two 600-frame samples |
| High-refresh 1080p pacing | >= 95% frames within 120 Hz while targeting 120-165 FPS | 97.3-97.9% within 120 Hz; 94.1-97.5% within 144 Hz | `measured` | Low-latency VSync policy, MMCSS scheduling and waitable timer; exact 165 deadline is intentionally not claimed as an every-frame guarantee |
| Final V5 1080p pacing | >= 95% frames within 120 Hz | 6.061 ms average, 6.062 ms p95, 6.325 ms p99; 99.80% within 120 Hz and 99.22% within 144 Hz | `measured` | Release, 1920x1080, 165 cap, 120 warmups plus 600 measured frames, full-viewport V5 Forest and scripted traversal |
| Final V5 1440p pacing | >= 95% frames within 120 Hz | 6.078 ms average, 6.064 ms p95, 8.625 ms p99; 98.83% within 120 Hz and 97.27% within 144 Hz | `measured` | Release, 2560x1440, same sample shape; max 14.956 ms remains a visible OS/upload tail |
| Final V5 CPU Draw | <= 8.33 ms stretch target | 0.609 ms at 1080p; 0.745 ms at 1440p | `measured` | CPU scope only; backend GPU timestamps remain unavailable |
| Compiled-core 1080p pacing | >= 95% frames within 120 Hz while targeting 165 FPS | 6.061 ms average, 6.061 ms p95, 6.086 ms p99; 99.61% within 120 Hz and 99.41% within 144 Hz | `measured` | Final compiled render graph/tile atlas/depth-stack Forest traversal, 120 warmups plus 600 measured frames |
| Compiled-core 1080p CPU Draw | <= 8.33 ms stretch target | 0.641 ms average, 3.385 ms peak | `measured` | Background 0.056 ms, Tilemap 0.291 ms, Entities 0.015 ms, Lighting 0.072 ms; Draw scopes are CPU time, not GPU timestamps |
| UI backdrop blur sample work | reduce full-resolution samples by >= 50% | 16.59M legacy versus 6.22M planned at High 1080p/radius 8, 62.5% lower | `measured` | Deterministic planner count for one scene capture; actual GPU cost awaits backend timestamps |
| Open-pause 1080p pacing | >= 95% frames within 120 Hz | 6.098 ms average, 6.063 ms p95, 9.723 ms p99; 98.89% within 120 Hz | `measured` | 60 warmups plus 180 measured frames with prepared downsample/Kawase blur and full settings UI active |
| Render-graph compile/execute fixture | 0 B steady state and < 0.25 ms p99 | 33-56 microseconds p50, 56-78 microseconds p95, last 174 microseconds p99 and 0 B | `measured` | Representative 15-pass Release graph including compile, lifetime query and generic struct execution |
| Entity visual submission | 0 B steady state and fewer material switches | 400 to 201 estimated switches for 200 alternating actors; 0 B | `measured` | Fixed-capacity stable shadow/actor plan with lossless overflow fallback |
| Central 500-actor physics update | improve retained 4.449 ms p99 baseline without tick allocation growth | 1.536/2.939/1.688 ms p99 across three runs; 1.688 ms median and 2.9 B/tick | `measured` | Enemies/items use one EntityManager-owned PhysicsWorld batch; isolated timing remains host-sensitive |
| Post-upgrade Forest 720p pacing | 120 FPS target with bounded tails | 8.333 ms average, 10.557 ms p95, 11.776 ms p99 | `measured` | Release, 1280x720, 120 cap, 60 warmups plus 120 measured frames, scripted traversal, RTX 3070 Ti; limiter/OS frame interval rather than GPU timing |
| Post-upgrade Forest CPU Draw | <= 8.33 ms stretch target | 1.922 ms average | `measured` | Same smoke with detailed background, bounded particles, lighting/radiance, reflections and responsive HUD active |
| Post-upgrade Forest 1440p pacing | 120 FPS target with bounded tails | 8.452 ms average, 10.251 ms p95, 14.682 ms p99 | `measured` | Release, 2560x1440, 120 cap, 60 warmups plus 120 measured frames, scripted traversal; one Forest sample, not the full quality/biome matrix |
| Post-upgrade Forest 1440p CPU Draw | <= 8.33 ms stretch target | 2.050 ms average | `measured` | Same smoke; 63.3 average Draw submissions, 4,784.2 SpriteBatch commands and 63.3 texture binding changes, with three visible actors at capture |
| Medium 1080p lighting-mask CPU | <= 6 ms Release | 2.542 ms average | `measured` | Release xUnit regression fixture, 1920x1080 viewport, 104x58 mask, 8 warmups and 40 samples; excludes GPU upload/Draw |
| Medium 1080p lighting-mask allocation | 0 B steady state | 0 B | `measured` | Same fixture; tile sampling, AO, directional shadow sweep, point-ray preparation and separable penumbra reuse caller-owned buffers |
| Traversal lighting-mask CPU | <= 1.25 ms average | final V5 0.535 ms at 1080p; 0.601 ms at 1440p | `measured` | Current Forest traversal includes bounded point rays, AO and smooth penumbra and is back inside the target |
| Reflection-radiance CPU | <= 0.35 ms average | 0.297 ms | `measured` | Bounded low-resolution colored-light/daylight sampling over planned water/wet surfaces in the current Forest smoke |
| Traversal lighting GPU upload CPU scope | <= 0.5 ms average | final V5 0.370 ms at 1080p; 0.210 ms at 1440p | `measured` | Average is inside target; the 1080p process retained a rare 13.479 ms peak, and this remains CPU upload scope rather than GPU time |
| Presentation preparation dispatch | once per client frame, never per fixed substep | one `LateUpdate` after any fixed-step count | `measured` | State-manager regression proves dispatch cardinality; runtime profiler exposes `Presentation.LateUpdate` separately from `Simulation.FixedUpdate` |
| Presentation cadence decisions | 0 B and negligible CPU | 27.4 ns/decision, 0 B | `measured` | 200,000 frames and 600,000 lighting/reflection/atmosphere/capture decisions in 16.431 ms; 10,000 telemetry captures also 0 B |
| Presentation frame admission | 0 B; avoid clustering expensive refreshes | 0 B over 1,000 budgeted schedules | `measured` | Fixed-unit admission defers optional work and forces initial, explicit or starvation-protected refreshes; focused tests cover both paths |
| GC allocations per Fixed Tick | ideally 0 B steady state | 2,690 B calibration average; latest quick 3,125 B average | `measured` | Calibration/quick frame snapshots are 1,621/2,419 B and remain the primary debt |
| Quick-smoke Fixed Tick allocation | <= 16,384 B average configured gate | 3,125 B average; 3,696 B p99 | `measured` | Latest Release physics/worldgen quick profile stays below its regression guardrail; this does not satisfy the ideal 0 B target |
| Immutable snapshot direct enumeration | 0 B steady state | 0 B | `measured` | Struct enumerator over defensive public snapshots and exclusive internal handoff storage |
| Tile/interaction flag queries | 0 B steady state | 0 B | `measured` | Hot enum flags use bitwise checks; allocation regression test covers 100,000 tile queries |
| Phase telemetry scope allocation | 0 B steady-state per sample | 0 B | `measured` | 100,000 enabled measurements after tiered-JIT stabilization; aggregates use fixed arrays |
| Phase telemetry BDN dry | smoke only; calibrated delta pending | 788.5 us off; 618.9 us on; 12 KB both | `measured` | Final one-operation dry smoke after one warmup; reversed timing order is noise-compatible and is not a comparative performance claim |
| State hash CPU average | checkpoint-only; numeric budget pending | 0.974 ms | `measured` | Calibration includes named RNG stream state in addition to loaded runtime state |
| State hash allocation | reduce after correctness | 3,509 B average | `measured` | Diagnostic checkpoint path exports/sorts state and is not per-frame |
| Deterministic replay | exact equality at every checkpoint | 1,200 ticks, checkpoints every 60, exact match | `measured` | Two sessions end at `0x26927FF799797AC9`; 239.922 ms average for the dual-session trace; bounded input-log capture is separately covered |
| Quick-smoke determinism | exact equality at every checkpoint | 240 ticks, checkpoints every 60, exact match | `measured` | Both quick traces end at `0x6D481199215B363A` |
| Typed event publish allocation | 0 B steady state | 0 B average and p99 | `measured` | 10,000 publishes after 1,000 warmups with one typed subscriber and reused event |
| GC allocations per render frame | no unbounded allocation | scheduler and frame telemetry 0 B | `measured` | Full renderer allocation remains unmeasured; cadence and telemetry hot paths are independently allocation-free |
| Sprite commands | scenario-specific budget pending baseline | 4,578.8 at 1080p; 5,513.9 at 1440p | `measured` | Final V5 Forest; this is command count, not batches or unique sprites |
| Actual batches/draw calls | < 100 meaningful submissions | 59.79 at 1080p; 62.06 at 1440p | `measured` | MonoGame CPU-side submission delta; not a GPU-time measurement |
| Texture switches | bounded; numeric target pending baseline | 59.79 at 1080p; 62.06 at 1440p | `measured` | MonoGame `TextureCount` delta is a framework state-change counter, not a unique texture count |
| Texture resources | one resource per canonical loaded source | 209 resources: 208 PNG files plus 1 fallback | `measured` | Current Forest/Crystal/UI Release smokes, 0 invalid resources |
| Texture frame descriptors | no first-use Draw materialization | 1,072/1,072 preloaded | `measured` | Current smoke content after the expanded panorama set |
| Texture resource load time | numeric budget pending atlas/GPU calibration | 330.393 ms latest process | `measured` | Current post-upgrade Forest Release process-start preload; not steady rendering |
| Texture load allocations | reduce setup allocation after correctness | 21,548,944 B | `measured` | Current all-content preload allocation around resource creation; not steady Draw allocation |
| Decoded RGBA payload | obey per-group configured budgets | 19,609,600 B total | `measured` | Exact current decoded estimate after the expanded panorama set; residency telemetry splits UI/World/Entities/Backgrounds/Effects |
| GPU texture memory | budget pending atlas/group design | unknown | `unknown` | Requires graphics-resource accounting/capture |
| Visible chunks | bounded by viewport and margin | unknown | `target` | Camera/zoom-labelled streaming trace required |
| Cached chunks | bounded by configured cache budget | unknown | `target` | Cache behavior tests exist; runtime count trace missing |
| Visible chunk preparation | <= 1 ms average | 0.003 ms average, 0.054 ms peak | `measured` | Bounded LateUpdate preparation in current V3 traversal; Draw performs no cache build |
| Final V5 tilemap Draw | <= 1 ms average, 0 B steady state | 0.338 ms at 1080p; 0.430 ms at 1440p; 0 B | `measured` | Precomputed texture buckets and visible chunk spans avoid per-frame regrouping/materialization |
| Chunk rebuild time | numeric target pending dirty traversal | visible steady traversal had no rebuild tail | `unknown` | Per-dirty-chunk distribution with mutation reason is still required |
| Streaming queue | bounded with no monotonic growth | apply queue defaults to 64; operations, time and bytes are independently bounded | `measured` | Focused queue-pressure/retry gates plus two 65-position bidirectional settlement traces cover bounded behavior; per-stage region attribution remains open |
| Streaming initial-window average | numeric target pending service trace | 9.896 ms calibration; 11.476 ms quick | `measured` | Current setup/generation fixture; content and OS variance prevent a speedup claim |
| Streaming initial-window p99 | numeric target pending service trace | 11.607 ms calibration; 12.235 ms quick | `measured` | Same small surface fixture; planner separately passed a 4,097-position bidirectional trace |
| Streaming initial-window allocation | reduce after profiling | 1,796,413 B calibration; 1,832,173 B quick | `measured` | Calibration is 30.0% below the earlier 2,564,943 B setup checkpoint |
| Streaming cold camera trace | numeric target pending calibrated run | BDN dry 131.856 ms/13,576,584 B; short 15.908 ms/11.71 MB | `measured` | Dry allocation is 8.2% below retained same-checkout history; short timing uses repeated warmed operations and is not directly comparable |
| Streaming warm camera trace | numeric target pending calibrated run | BDN dry 48.631 ms/7,475,264 B; short 51.823 ms/7.76 MB | `measured` | Dry allocation is 26.2% below retained history; three-sample short timing has high variance |
| Streaming cold settle distribution | <= 250 ms p99 provisional | 99.3-203.3 ms p99 across two 65-position runs | `measured` | Negative-to-positive-X background generation; p95 16.3-72.0 ms exposes OS/job-scheduling variance |
| Streaming warm settle distribution | <= 16.67 ms p99 provisional | 0.014-0.015 ms p99 across two 65-position runs | `measured` | Reverse traversal retains route chunks; zero over-budget samples |
| Chunk load time | numeric target pending baseline | cumulative telemetry implemented, distribution unknown | `unknown` | Cold/warm region read distribution artifact required |
| Chunk generate time | numeric target pending baseline | cumulative telemetry implemented, distribution unknown | `unknown` | Deterministic seed/profile-labelled distribution artifact required |
| Chunk apply time | bounded by main-thread time/byte/operation budgets | all three budgets enforced; defaults 32 operations, 4 ms and 512 KiB | `measured` | Scripted-clock and byte-budget tests prove deferral plus oversize forward progress; real camera distribution remains open |
| Chunk save time | numeric target pending baseline | cumulative telemetry implemented, distribution unknown | `unknown` | Representative payload distribution required |
| Region file bytes | bounded by live chunks plus documented overhead | unknown | `target` | Inventory before/after save/compaction scenarios required |
| Lighting workspace | bounded by largest processed region | reusable light/solid/visited/queue arrays | `measured` | No per-source dictionary/queue growth; capacity increases only when a larger region is first observed |
| Lighting propagation time | numeric target pending RGB/region work | 15.62 ms at 4 chunks; 28.50 ms at 12 chunks | `measured` | Final BDN dry single-operation evidence with vertical seeds, two-pass indirect skylight and point lights; not a calibrated distribution |
| Lighting propagation allocation | minimize steady-state churn | 40 B at both 4 and 12 chunks | `measured` | Down from 210.53/618.23 KB through reusable workspaces, bitwise flags and indexed source traversal |
| Procedural background Draw | <= 1.25 ms average | authored-density V5 0.1105 ms at 1080p and 0.1068 ms at 3440x1440; 0 B | `measured` | 120 measured frames after 120 warmups; source scale is camera/terrain invariant, native through 1440p and whole-pixel on 2160p-class output; top/bottom coverage is separate from the panorama |
| Particle Draw | <= 1 ms | 0.055 ms average | `measured` | Same smoke; fixed 192/640/1,536 capacity tiers, hard draw budgets and offscreen culling |
| Responsive UI Draw | <= 2.5 ms | 0.147 ms average | `measured` | Same smoke; adaptive glass HUD, cached text and segmented meters active |
| Liquid active cells | bounded by loaded/dirty regions and fixed workspace capacity | 128-cell representative frontier | `measured` | Active/deferred cells reuse one workspace; default initial/compatibility seeding is capped at 16,384 tile checks per step and can be tuned lower |
| Liquid step time | <= 1 ms p99 for 128 active cells | 0.021 ms p50, 0.047 ms p95, 0.069 ms p99; 0 B/step | `measured` | Final isolated Release active-cell path; compatibility full-region probe is 2.163 ms p50/14.597 ms p99 and seed work remains hard-budgeted |
| Entity spawn maintenance | <= 2 ms for 500 active actors | 0.184 ms p99, 0 B/tick | `measured` | Final isolated Release cap-maintenance distribution |
| Entity AI update | <= 12 ms p99 for 500 active actors | 4.488 ms p99, 0.5 B/tick | `measured` | Final isolated Release population; the full-suite timing gate is intentionally rerun serially after OS-load outliers |
| Spatial queries | 0 B for representative caller-owned queries | pickup magnetism 0 B; 500-projectile swept combat 0.290 ms average/0.532 ms p99/0 B; 1,000-body overlap 0.244 ms/query/0 B | `measured` | Reused buffers, deterministic TOI/broadphase ordering, nearest bounded homing and incremental spatial membership; dense adversarial distributions remain open |
| Generic physics step | <= 2 ms for 1,000 settled bodies | 0.587 ms/step and 0 B | `measured` | Release fixture over 120 measured steps; fixed capacity fails fast instead of introducing deferred-time slowdown |
| Save time | no interrupted-write full-save loss; numeric target pending | unknown | `target` | Full/dirty/autosave scenarios with payload sizes required |
| Content load average | numeric target pending representative mod set | 56.547 ms | `measured` | Current quick load includes validated runtime clips, rigs, character/entity profiles, event triggers and all prior content |
| Content load p95 | numeric target pending representative mod set | 61.223 ms | `measured` | Current quick sample; tail remains provisional |
| Content load allocation | reduce after correctness and schema work | 2,778,368 B average | `measured` | Includes deterministic runtime-animation/entity-profile construction plus event-trigger validation |
| Simple world generation average | <= 4 ms for the 256x128 reference profile | 0.694 ms | `measured` | Latest quick sample after chunk-local `WorldGenerationWorkspace`, seed 1337, regional structures enabled |
| Simple world generation p95 | <= 4 ms for the 256x128 reference profile | 0.862 ms | `measured` | Latest executable quick profile |
| Simple world generation allocation | <= 1,400,000 B for 256x128 | 1,252,512 B | `measured` | Down from 17,052,112 B; focused workspace gate reports 1,252,488 B while harness accounting adds 24 B |
| Startup time | numeric target pending process timer | unknown | `unknown` | Smoke exits successfully but does not record process-start-to-frame duration |

## Interpretation

- The fixed-tick fixture now includes guard/combat, living-world spawn context, active enemies, drops, inventory, equipment and farm plots, but it is not the separate 200-entity soak.
- The configured quick-smoke guardrails pass: latest 0.159 ms p95 is below 4 ms and 3,125 B average allocation is below 16,384 B. These are regression limits, not ideal production budgets.
- Version-cached inventory/farm snapshots, columnar copy-on-change entity storage and caller-owned pickup queries reduce quick/calibration allocation by 57.2%/56.9% while preserving historical snapshot immutability and detailed AI fields.
- The remaining 2,419 B quick / 1,621 B calibration frame-snapshot averages are the next allocation targets; 500 moving entities cost 5,000.2 B/tick because their position column legitimately changes each frame.
- Named streams plus atomic random/event sidecar recovery, mid-trace continuation and dual-session replay now prove the default session-owned deterministic path locally.
- Typed event publication is now allocation-free in the isolated steady-state fixture; subscription changes deliberately allocate snapshots outside the hot path.
- Background streaming is operationally bounded across concurrency, queue length, operations, elapsed apply time, decoded bytes and retry attempts. Cold/warm settlement now has a real distribution, while region-read/generate/apply/save attribution remains open.
- Texture correctness is now measurable through 209 resources and 1,072 frames. The 19.61 MB decoded all-content preload is acceptable for the current sample but is not a scalable policy.
- The medium light mask samples each solid tile once and uses one point-shadow ray; High uses three endpoint-spread samples for fractional penumbra visibility. Colored reflection radiance is independently bounded and uploaded only when its content hash changes.
- View-keyed chunk streaming, independent presentation cadence and the new per-frame admission budget remove or spread work that otherwise scales or clusters at high render frequency. Starvation protection prevents indefinitely stale visual data.
- Procedural background, tilemap and responsive-layout hot paths use fixed caller-owned or renderer-owned buffers. The authored-density V5 background averages 0.1105 ms at 1080p and 0.1068 ms at 3440x1440 in the latest short visual traversals and stays 0 B; all-content panorama residency is now the larger scaling concern.
- The current V5 Forest traces keep 99.80%/98.83% of retained 1080p/1440p frames inside 120 Hz while targeting 165 FPS. They do not establish every-frame 165 Hz, actual GPU timing, all-biome traversal tails or VSync-controlled monitor cadence.
- No before/after speedup is claimed. Before this slice, texture load/resource/frame telemetry did not exist.
- Quick profile results are executable CI regression smoke against configured guardrails; they are not calibrated production distributions.

## Next Measurement Package

Epic 2 Milestone 5 now has compact streaming windows, columnar immutable snapshots, deterministic spawn warm-start, active-cell liquid, intent-separated physics, swept/time-ordered combat, bounded nearest homing and chunk-local finite generation, with a green 1381-test Debug/Release tree and final V5 1080p/1440p traces. The next measurement package covers centralized enemy/item physics, body-body impulses/platform materials, large regional generation, liquid pressure/equalization and dense adversarial combat. GPU time stays unknown until a backend-specific timestamp-query contract exists.
