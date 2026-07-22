# YjsE Performance Budgets

Last updated: 2026-07-22 after Session 0 quick-smoke capture and class-isolated Release performance validation

## Evidence Rules

Performance value states are only `target`, `measured`, and `unknown`. A measured calibration does not automatically qualify a production budget. Every acceptance claim must identify revision/tree state, host, runtime, configuration, scenario, warmup, samples, collection method and raw artifact.

Functional correctness and timing acceptance are separate partitions. Debug and Release run the same functional filter; `tools/run_isolated_performance_tests.ps1` then runs each discovered performance class in a fresh Release process. Timing thresholds remain unchanged, and seven-window medians are used only by high-variance fixtures where every window also satisfies the allocation contract.

Current performance-baseline capability status: `partial`.

Current evidence host:

- Revision `9ce29ffd7a626a5ba1eed53c442919587b4c0ed4` plus the current mixed working tree.
- Windows 10.0.26100, AMD Ryzen 5 5500, 6 cores/12 logical processors.
- .NET SDK 8.0.420, runtime 8.0.28, workstation GC, Release.
- Graphics smoke: NVIDIA GeForce RTX 3070 Ti/PCIe/SSE2, including 1920x1080 high-refresh runs.
- Raw ignored artifacts: Session 0 `artifacts/performance-session0.json`; retained long baseline `artifacts/performance-lighting-physics-v7-final.json`; post-V8 `artifacts/performance-lighting-surface-cache-v8.json` and PNG; day/night/Frostwood/ultrawide V8 scene reports; current asset-audit reports; prior renderer reports; benchmark profiles and `artifacts/benchmarkdotnet/**`.

## Budget Table

| Metric | Target | Measured | Value state | Evidence and limitation |
| --- | --- | --- | --- | --- |
| Simulation frequency | 60 Hz fixed step | 1/60 s authoritative client/harness delta | `measured` | Variable rendering and custom limiting do not replace `FixedUpdateRunner` ownership |
| Fixed Tick CPU average | <= 4 ms | 0.102 ms retained calibration; 0.309 ms Session 0 v3 quick | `measured` | Both Release harness scenarios remain below the gate; different scenario versions are not a speedup comparison |
| Fixed Tick CPU p99 | <= 8 ms | 0.257 ms retained calibration; 2.756 ms Session 0 v3 quick | `measured` | Both profiles remain below the CPU gate; the newer v3 quick trace retains its host tail |
| Quick-smoke Fixed Tick p95 | <= 4 ms configured gate | 0.570 ms | `measured` | Session 0 Release v3 quick profile; still well below the configured guardrail |
| Update Time | <= 8 ms initial client budget | 0.389-0.430 ms average | `measured` | Two Release V3 traversals, 1920x1080/165 cap, real user settings, overlays and scripted movement; rare peaks 9.706-16.564 ms include upload/OS tails |
| Draw Time at 60 FPS | <= 16.67 ms | 1.816-1.852 ms CPU average at 165 FPS | `measured` | Same traversals; peaks 7.355-7.468 ms and CPU scope is not a GPU timestamp |
| Draw Time stretch target | <= 8.33 ms at 120 FPS | 6.166-6.231 ms average frame interval | `measured` | p95 6.075-7.138 ms, p99 10.722-11.418 ms and maximum 12.518-13.354 ms across two 600-frame samples |
| High-refresh 1080p pacing | >= 95% frames within 120 Hz while targeting 120-165 FPS | 97.3-97.9% within 120 Hz; 94.1-97.5% within 144 Hz | `measured` | Low-latency VSync policy, MMCSS scheduling and waitable timer; exact 165 deadline is intentionally not claimed as an every-frame guarantee |
| Final V5 1080p pacing | >= 95% frames within 120 Hz | 6.061 ms average, 6.062 ms p95, 6.325 ms p99; 99.80% within 120 Hz and 99.22% within 144 Hz | `measured` | Release, 1920x1080, 165 cap, 120 warmups plus 600 measured frames, full-viewport V5 Forest and scripted traversal |
| Final V5 1440p pacing | >= 95% frames within 120 Hz | 6.078 ms average, 6.064 ms p95, 8.625 ms p99; 98.83% within 120 Hz and 97.27% within 144 Hz | `measured` | Release, 2560x1440, same sample shape; max 14.956 ms remains a visible OS/upload tail |
| Final V5 CPU Draw | <= 8.33 ms stretch target | 0.609 ms at 1080p; 0.745 ms at 1440p | `measured` | CPU scope only; backend GPU timestamps remain unavailable |
| Compiled-core 1080p pacing | >= 95% frames within 120 Hz while targeting 165 FPS | 6.061 ms average, 6.061 ms p95, 6.070 ms p99; 99.61% within 120 Hz and 99.61% within 144 Hz | `measured` | Final compiled render graph/tile atlas/V6 depth-stack Forest traversal, 120 warmups plus 600 measured frames |
| Compiled-core 1080p CPU Draw | <= 8.33 ms stretch target | 0.574 ms average, 2.313 ms peak | `measured` | Background 0.043 ms, Tilemap 0.250 ms, Entities 0.013 ms, Lighting 0.060 ms; Draw scopes are CPU time, not GPU timestamps |
| Current pooled-core 1080p pacing | >= 95% frames within 120 Hz | 6.073 ms average, 6.062 ms p95, 7.023 ms p99, 12.372 ms max; 99.80% within 120 Hz and 98.83% within 144 Hz | `measured` | Release Forest, 1920x1080, 165 cap, 120 warmups plus 600 measured frames; current mixed tree loads 247 resources/1,500 frames |
| Current pooled-core 1440p pacing | >= 95% frames within 120 Hz | 6.076 ms average, 7.112 ms p95, 8.784 ms p99, 13.716 ms max; 98.05% within 120 Hz and 94.53% within 144 Hz | `measured` | Same clean serial process at 2560x1440; 120 Hz gate passes, while the 144 Hz stretch gate misses 95% by 0.47 percentage points |
| Current V7 lighting/physics 1080p pacing | >= 95% frames within 120 Hz and 144 Hz while targeting 165 FPS | 6.091 ms average, 6.065 ms p95, 9.069 ms p99, 20.426 ms max; 98.44% within 120 Hz and 96.88% within 144 Hz | `measured` | Release Forest, 1920x1080, 120 warmups plus 600 measured frames; CPU Draw 0.609 ms and Update 0.637 ms average; rare host tails retained |
| Post-V8 visual-correctness 1080p pacing | >= 95% frames within 120 Hz while targeting 165 FPS | 6.061 ms average, 8.202 ms p95, 9.168 ms p99; 96.7% within 120 Hz | measured | Release Forest, 1920x1080, 165 cap, 120 warmups plus 120 measured frames after finite parallax, local-surface portals and biome weather; short sample complements rather than replaces the retained 600-frame V7 baseline |
| Post-V8 light-mask construction | <= 2.5 ms average and 0 B steady state | 1.563 ms average, 2.622 ms peak and 0 B | measured | Local-column surface cache, direct/diffuse/lunar transport, AO, point rays and sky-portal propagation; periodic 425,984-byte scratch allocation is removed |
| Post-V8 total lighting preparation | <= 3.0 ms average with no recurring scratch allocation | 1.971 ms average; last allocation sample 0 B | measured | Includes mask/color preparation and upload CPU scopes; no value is presented as backend GPU time |
| Cached surface-height resolver | exact planner parity and 0 B after warmup | 0 B across repeated negative/positive-X queries | measured | Bounded 2,048-entry direct-mapped cache reuses the current regional plan; collisions affect hit rate only |
| Reliable 500-projectile combat contact | <= 2 ms p99 and 0 B | 0.608 ms p99 and 0 B | measured | Deterministic earliest entity/tile TOI, background-wall pass-through, typed magic damage and preserved impact velocity |
| 500-projectile physics synchronization after contact hardening | <= 10 ms p99 and 0 B | 0.687 ms p99 and 0 B | measured | Isolated Release fixture; remains far inside the retained regression gate |
| Current pooled-core CPU Draw | <= 8.33 ms stretch target | 0.789 ms at 1080p; 0.780 ms at 1440p | `measured` | CPU scope only; post-V8 local-surface lighting is measured separately inside its preparation budget and no GPU time is inferred |
| UI backdrop blur sample work | reduce full-resolution samples by >= 50% | 16.59M legacy versus 6.22M planned at High 1080p/radius 8, 62.5% lower | `measured` | Deterministic planner count for one scene capture; actual GPU cost awaits backend timestamps |
| Open-pause 1080p pacing | >= 95% frames within 120 Hz | 6.298 ms average, 7.566 ms p95, 11.945 ms p99; 96.11% within 120 Hz | `measured` | Final 60-warmup/180-measured-frame sample with prepared downsample/Kawase blur and full settings UI active; 0.807 ms CPU Draw average, one 22.862 ms OS/UI tail retained |
| Render-graph compile/execute fixture | 0 B steady state and < 0.25 ms p99 | 33-56 microseconds p50, 56-78 microseconds p95, last 174 microseconds p99 and 0 B | `measured` | Representative 15-pass Release graph including compile, lifetime query and generic struct execution |
| Transient target Acquire plus Bind | 0 B steady state and < 1 us p99 | 0.300 us p50, 0.500 us p95/p99 and 0 B | `measured` | 10,000 Release iterations against fixed-capacity descriptor/alias storage; graphics-device creation is outside the steady reuse loop |
| Entity visual submission | 0 B steady state and fewer material switches | 400 to 201 estimated switches for 200 alternating actors; 0 B | `measured` | Fixed-capacity stable shadow/actor plan with lossless overflow fallback |
| Central 500-actor physics update | improve retained 4.449 ms p99 baseline without tick allocation growth | 1.536/2.939/1.688 ms p99 across three runs; 1.688 ms median and 2.9 B/tick | `measured` | Enemies/items use one EntityManager-owned PhysicsWorld batch; isolated timing remains host-sensitive |
| Post-upgrade Forest 720p pacing | 120 FPS target with bounded tails | 8.333 ms average, 10.557 ms p95, 11.776 ms p99 | `measured` | Release, 1280x720, 120 cap, 60 warmups plus 120 measured frames, scripted traversal, RTX 3070 Ti; limiter/OS frame interval rather than GPU timing |
| Post-upgrade Forest CPU Draw | <= 8.33 ms stretch target | 1.922 ms average | `measured` | Same smoke with detailed background, bounded particles, lighting/radiance, reflections and responsive HUD active |
| Post-upgrade Forest 1440p pacing | 120 FPS target with bounded tails | 8.452 ms average, 10.251 ms p95, 14.682 ms p99 | `measured` | Release, 2560x1440, 120 cap, 60 warmups plus 120 measured frames, scripted traversal; one Forest sample, not the full quality/biome matrix |
| Post-upgrade Forest 1440p CPU Draw | <= 8.33 ms stretch target | 2.050 ms average | `measured` | Same smoke; 63.3 average Draw submissions, 4,784.2 SpriteBatch commands and 63.3 texture binding changes, with three visible actors at capture |
| Medium 1080p lighting-mask CPU | <= 6 ms Release | about 1.05 ms average | `measured` | Release xUnit fixture, 104x58 mask; direct/diffuse scanline transport, AO and point rays remain 0 B and exclude GPU upload/Draw |
| Medium 1080p lighting-mask allocation | 0 B steady state | 0 B | `measured` | Same fixture; tile sampling, AO, directional shadow sweep, point-ray preparation and separable penumbra reuse caller-owned buffers |
| Traversal lighting-mask CPU | <= 1.25 ms average | 0.602 ms average, 4.085 ms peak | `measured` | Final 1920x1080 V7 600-frame traversal; O(mask-pixel) solar transport, bounded point rays, AO and smooth penumbra; 0 B average allocation |
| Lighting temporal stabilization | <= 0.5 ms average and 0 B | 0.207 ms average, 2.169 ms peak and 0 B | `measured` | World-space reprojection with depth/occlusion disocclusion rejection over 165 prepared frames in the final V7 traversal |
| Reflection-radiance CPU | <= 0.35 ms average | 0.297 ms | `measured` | Bounded low-resolution colored-light/daylight sampling over planned water/wet surfaces in the current Forest smoke |
| Traversal lighting GPU upload CPU scope | <= 0.5 ms average | final V5 0.370 ms at 1080p; 0.210 ms at 1440p | `measured` | Average is inside target; the 1080p process retained a rare 13.479 ms peak, and this remains CPU upload scope rather than GPU time |
| Presentation preparation dispatch | once per client frame, never per fixed substep | one `LateUpdate` after any fixed-step count | `measured` | State-manager regression proves dispatch cardinality; runtime profiler exposes `Presentation.LateUpdate` separately from `Simulation.FixedUpdate` |
| Presentation cadence decisions | 0 B and negligible CPU | 27.4 ns/decision, 0 B | `measured` | 200,000 frames and 600,000 lighting/reflection/atmosphere/capture decisions in 16.431 ms; 10,000 telemetry captures also 0 B |
| Presentation frame admission | 0 B; avoid clustering expensive refreshes | 0 B over 1,000 budgeted schedules | `measured` | Fixed-unit admission defers optional work and forces initial, explicit or starvation-protected refreshes; focused tests cover both paths |
| GC allocations per Fixed Tick | ideally 0 B steady state | 2,690 B retained calibration average; Session 0 v3 quick 3,417 B average | `measured` | Session 0 FrameSnapshot averages 2,297 B and remains the primary allocation debt |
| Quick-smoke Fixed Tick allocation | <= 16,384 B average configured gate | 3,417 B average; 4,080 B p99 | `measured` | Session 0 Release v3 quick profile stays below its regression guardrail; this does not satisfy the ideal 0 B target |
| Immutable snapshot direct enumeration | 0 B steady state | 0 B | `measured` | Struct enumerator over defensive public snapshots and exclusive internal handoff storage |
| Tile/interaction flag queries | 0 B steady state | 0 B | `measured` | Hot enum flags use bitwise checks; allocation regression test covers 100,000 tile queries |
| Phase telemetry scope allocation | 0 B steady-state per sample | 0 B | `measured` | 100,000 enabled measurements after tiered-JIT stabilization; aggregates use fixed arrays |
| Phase telemetry BDN dry | smoke only; calibrated delta pending | 788.5 us off; 618.9 us on; 12 KB both | `measured` | Final one-operation dry smoke after one warmup; reversed timing order is noise-compatible and is not a comparative performance claim |
| State hash CPU average | checkpoint-only; numeric budget pending | 0.974 ms retained calibration; 2.101 ms Session 0 v3 quick | `measured` | Both include named RNG stream state; the v3 quick p99 is 6.782 ms |
| State hash allocation | reduce after correctness | 3,578 B Session 0 average | `measured` | Diagnostic checkpoint path exports/sorts state and is not per-frame |
| Deterministic replay | exact equality at every checkpoint | 1,200 ticks, checkpoints every 60, exact match | `measured` | Two sessions end at `0x26927FF799797AC9`; 239.922 ms average for the dual-session trace; bounded input-log capture is separately covered |
| Quick-smoke determinism | exact equality at every checkpoint | 240 ticks, checkpoints every 60, exact match | `measured` | Session 0 v3 traces end at `0x2B10E91090EC0C22`; retained v2 traces use their scenario-specific prior hash |
| Typed event publish allocation | 0 B steady state | 0 B average and p99 | `measured` | 10,000 publishes after 1,000 warmups with one typed subscriber and reused event |
| GC allocations per render frame | no unbounded allocation | scheduler and frame telemetry 0 B | `measured` | Full renderer allocation remains unmeasured; cadence and telemetry hot paths are independently allocation-free |
| Sprite commands | scenario-specific budget pending baseline | 4,578.8 at 1080p; 5,513.9 at 1440p | `measured` | Final V5 Forest; this is command count, not batches or unique sprites |
| Actual batches/draw calls | < 100 meaningful submissions | 59.79 at 1080p; 62.06 at 1440p | `measured` | MonoGame CPU-side submission delta; not a GPU-time measurement |
| Texture switches | bounded; numeric target pending baseline | 59.79 at 1080p; 62.06 at 1440p | `measured` | MonoGame `TextureCount` delta is a framework state-change counter, not a unique texture count |
| Texture resources | one resource per canonical loaded source | 259 resources: 258 PNG files plus 1 fallback | `measured` | Current V7 Forest Release smoke; 0 invalid resources |
| Texture frame descriptors | no first-use Draw materialization | 1,514/1,514 preloaded | `measured` | Current V7 worldscape, mobility, entity and UI content |
| Texture resource load time | numeric budget pending atlas/GPU calibration | 637.615 ms latest process | `measured` | Current V7 all-content process-start preload; not steady rendering |
| Texture load allocations | reduce setup allocation after correctness | 46,705,584 B | `measured` | Current all-content resource creation; not steady Draw allocation |
| Decoded RGBA payload | obey per-group configured budgets | 43,344,896 B total | `measured` | Exact current decoded estimate; residency telemetry splits UI/World/Entities/Backgrounds/Effects |
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
| Procedural background Draw | <= 1.25 ms average | 0.092 ms average, 0.250 ms peak; 0 B | `measured` | Final 600-frame V7 trace with two fullscreen feature planes plus Far/Mid/Near depth planes, no bottom repetition and point-sampled authored-distance projection |
| Particle Draw | <= 1 ms | 0.026 ms average, 0.233 ms peak; 0 B | `measured` | Final V7 traversal; visual tiers, culling and physical feedback snapshots remain bounded |
| Physical particle step, 10,000 active | <= 1 ms p99 and 0 B steady state | 0.181/0.300/0.475 ms p50/p95/p99 and 0 B | `measured` | Release renderer-neutral pool with semi-implicit integration, gravity/wind/drag, swept tile collision, bounce/friction, budgets and bounded events |
| Responsive UI Draw | <= 2.5 ms | 0.147 ms average | `measured` | Same smoke; adaptive glass HUD, cached text and segmented meters active |
| Liquid active cells | bounded by loaded/dirty regions and fixed workspace capacity | 128-cell representative frontier | `measured` | Active/deferred cells reuse one workspace; default initial/compatibility seeding is capped at 16,384 tile checks per step and can be tuned lower |
| Liquid step time | <= 1 ms p99 for 128 active cells | 0.021 ms p50, 0.047 ms p95, 0.069 ms p99; 0 B/step | `measured` | Final isolated Release active-cell path; compatibility full-region probe is 2.163 ms p50/14.597 ms p99 and seed work remains hard-budgeted |
| Liquid presentation prepare | <= 1 ms average, 0 B | 0.253-0.384 ms at Forest/Amber/Marsh 1080p; 0.668 ms at Amber 3440x1440; 0 B | `measured` | Bounded LateUpdate planner coalesces body/depth/surface/shore runs; deterministic captures did not include visible water, so this is runtime-budget evidence rather than visual acceptance |
| Entity spawn maintenance | <= 2 ms for 500 active actors | 0.184 ms p99, 0 B/tick | `measured` | Final isolated Release cap-maintenance distribution |
| Entity AI decisions, 500 actors | <= 4 ms p99 and 0 B/tick | 0.995 to 0.371 ms average (-62.7%), 0.886 ms p99, 0 B/tick | `measured` | Isolated Release before/after on the same scheduler fixture; Physics/status/lifecycle remain full-rate while decisions are budgeted |
| Entity AI decisions, 2,000 actors | <= 8 ms p99 and 0 B/tick | 4.557 to 1.378 ms average (-69.8%), 3.132 ms p99, 0 B/tick | `measured` | Isolated Release overload/fairness fixture with deterministic age-first scheduling |
| Spatial queries | 0 B for representative caller-owned queries | pickup magnetism 0 B; 500-projectile swept combat 0.290 ms average/0.532 ms p99/0 B; 1,000-body broadphase 0.331 ms/query/0 B | `measured` | Reused buffers, deterministic TOI/broadphase ordering, nearest bounded homing and incremental spatial membership; dense adversarial distributions remain open |
| Generic physics step | <= 2 ms for 1,000 settled bodies | 0.988 ms/step and 0 B | `measured` | Release fixture with deterministic body contacts/material response; fixed capacity fails fast before mutation instead of introducing deferred-time slowdown |
| Partial tile-floor resolution | 0 B with bounded tile tests | 0 B across 1,000 moves | `measured` | Release caller-storage fixture covers an upward slope; the 26-test resolver scope also covers half blocks, both orientations, high-side walls and one-way compatibility |
| Continuous physics, 500 fast bodies | <= 2 ms/step and 0 B | 0.665 ms/step and 0 B | `measured` | Isolated Release swept broadphase plus bounded multi-pass body TOI with caller-owned workspaces |
| Projectile physics synchronization, 500 active projectiles | <= 10 ms p99 and 0 B/tick | 0.562 ms median-run p99 and 0 B across 180 ticks | `measured` | Three isolated Release processes after caller-owned per-body contact-slice integration; observed p99 0.461-1.214 ms |
| Continuous physics, dense pairs | bounded fail-closed cost and 0 B | 6.311 ms/step and 0 B for 128 bodies/8,128 pairs | `measured` | Adversarial Release fixture; pass-limit exhaustion freezes unresolved bodies and emits telemetry instead of tunneling |
| Mobility ability dock planning | 0 B steady state | 0 B across 10,000 plans | `measured` | Fixed three-entry span planner includes deterministic order and responsive layout from 320x180 through 2560x1440; rendering still requires a gameplay capture |
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
- The configured Session 0 v3 quick-smoke guardrails pass: 0.570 ms p95 is below 4 ms and 3,417 B average allocation is below 16,384 B. These are regression limits, not ideal production budgets.
- Version-cached inventory/farm snapshots, columnar copy-on-change entity storage and caller-owned pickup queries reduce quick/calibration allocation by 57.2%/56.9% while preserving historical snapshot immutability and detailed AI fields.
- The remaining 2,297 B Session 0 quick / 1,621 B retained calibration frame-snapshot averages are the next allocation targets; 500 moving entities cost 5,000.2 B/tick because their position column legitimately changes each frame.
- Named streams plus atomic random/event sidecar recovery, mid-trace continuation and dual-session replay now prove the default session-owned deterministic path locally.
- Typed event publication is now allocation-free in the isolated steady-state fixture; subscription changes deliberately allocate snapshots outside the hot path.
- Background streaming is operationally bounded across concurrency, queue length, operations, elapsed apply time, decoded bytes and retry attempts. Cold/warm settlement now has a real distribution, while region-read/generate/apply/save attribution remains open.
- Texture correctness is now measurable through 259 resources and 1,514 frames. The 43.34 MB decoded all-content preload is acceptable for the current sample but is not a scalable residency policy.
- The light mask samples geometry once, separates direct/diffuse solar energy and propagates directional occlusion in linear scanlines; point-light quality remains one ray on Medium and three spread samples on High. World-reprojected temporal filtering rejects disocclusions, dynamic projectiles feed bounded radiance and unchanged maps skip upload.
- View-keyed chunk streaming, independent presentation cadence and the new per-frame admission budget remove or spread work that otherwise scales or clusters at high render frequency. Starvation protection prevents indefinitely stale visual data.
- Procedural background, tilemap, liquid-presentation and responsive-layout hot paths use fixed caller-owned or renderer-owned buffers. The V6 background averages 0.089-0.113 ms at 1080p and 0.134 ms at 3440x1440 and stays 0 B; all-content depth-plane residency is now the larger scaling concern.
- The retained V7 Forest trace keeps 98.44% of the rolling 512-frame window inside 120 Hz and 96.88% inside 144 Hz while targeting 165 FPS. The post-V8 short gate restores a 6.061 ms mean and 96.7% within 120 Hz after richer local-surface/portal lighting, while removing the recurring 425,984-byte scratch allocation. Every-frame 165 Hz, actual GPU timing, long 1440p/all-biome tails and VSync-controlled monitor cadence remain unproven.
- No before/after speedup is claimed. Before this slice, texture load/resource/frame telemetry did not exist.
- Quick profile results are executable CI regression smoke against configured guardrails; they are not calibrated production distributions.

## Next Measurement Package

Epic 2 Milestone 5 now also has finite vertical parallax, biome-gated frozen weather, local-surface/sky-portal/lunar lighting, transactional mana, equipment mobility, deterministic wand/projectile contact and typed developer intents. Session 0 accepts Release correctness through 1,710 functional tests plus 21 fresh performance-class processes containing 39 tests; combined-host timing outliers remain diagnostics and no budget was loosened. The next package should combine dense scheduled AI, fast bodies/projectiles and physical particles in one authoritative fixture; add material transmission/normal maps and persistent room/portal caches; and capture Inventory, Crafting, Settings, Developer Menu, water, snow, torch and combat scenes at 720p/1080p/1440p. GPU time stays unknown until backend timestamp queries exist.
