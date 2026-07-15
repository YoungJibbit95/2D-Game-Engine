# YjsE Performance Budgets

Last updated: 2026-07-15 after bounded feedback/replay, phased combat and retry-budgeted streaming integration

## Evidence Rules

Performance value states are only `target`, `measured`, and `unknown`. A measured calibration does not automatically qualify a production budget. Every acceptance claim must identify revision/tree state, host, runtime, configuration, scenario, warmup, samples, collection method and raw artifact.

Current performance-baseline capability status: `partial`.

Current evidence host:

- Revision `f9a06072a1172d2bd8d064cabad92235443dd8c8` plus a dirty working tree.
- Windows 10.0.26100, AMD Ryzen 5 5500, 6 cores/12 logical processors.
- .NET SDK 8.0.420, runtime 8.0.28, workstation GC, Release.
- Graphics smoke: NVIDIA GeForce RTX 3070 Ti/PCIe/SSE2, 640x360.
- Raw ignored artifacts: `artifacts/performance-quick-current.json`, `artifacts/performance-calibration-current.json`, `artifacts/benchmarkdotnet/**` and `artifacts/smoke-current/*.json` scene reports.

## Budget Table

| Metric | Target | Measured | Value state | Evidence and limitation |
| --- | --- | --- | --- | --- |
| Simulation frequency | 60 Hz fixed step | 1/60 s harness delta | `measured` | Scenario metadata only; client pacing not measured |
| Fixed Tick CPU average | <= 4 ms | 0.065 ms calibration; 0.099 ms quick | `measured` | Current Release harness after feedback/replay/combat/streaming integration |
| Fixed Tick CPU p99 | <= 8 ms | 0.129 ms calibration; 0.346 ms quick | `measured` | Both current profiles remain well below the CPU gate |
| Quick-smoke Fixed Tick p95 | <= 4 ms configured gate | 0.131 ms | `measured` | Current Release quick profile, 1,200 iterations after 120 warmups |
| Update Time | <= 8 ms initial client budget | unknown | `target` | Need client telemetry export separated from Draw |
| Draw Time at 60 FPS | <= 16.67 ms | unknown | `target` | Nonblank pixels are proven; CPU/GPU Draw timing is not |
| Draw Time stretch target | <= 8.33 ms at 120 FPS | unknown | `target` | Same scene/hardware measurement required |
| GC allocations per Fixed Tick | ideally 0 B steady state | 6,237 B calibration average; 7,250 B quick average | `measured` | Calibration frame snapshots are 3,132 B and remain the primary debt |
| Quick-smoke Fixed Tick allocation | <= 16,384 B average configured gate | 7,250 B average; 7,912 B p99 | `measured` | Current Release quick profile stays below its regression guardrail; this does not satisfy the ideal 0 B target |
| Immutable snapshot direct enumeration | 0 B steady state | 0 B | `measured` | Struct enumerator over defensive public snapshots and exclusive internal handoff storage |
| Tile/interaction flag queries | 0 B steady state | 0 B | `measured` | Hot enum flags use bitwise checks; allocation regression test covers 100,000 tile queries |
| Phase telemetry scope allocation | 0 B steady-state per sample | 0 B | `measured` | 100,000 enabled measurements after tiered-JIT stabilization; aggregates use fixed arrays |
| Phase telemetry BDN dry | smoke only; calibrated delta pending | 788.5 us off; 618.9 us on; 12 KB both | `measured` | Final one-operation dry smoke after one warmup; reversed timing order is noise-compatible and is not a comparative performance claim |
| State hash CPU average | checkpoint-only; numeric budget pending | 0.974 ms | `measured` | Calibration includes named RNG stream state in addition to loaded runtime state |
| State hash allocation | reduce after correctness | 3,509 B average | `measured` | Diagnostic checkpoint path exports/sorts state and is not per-frame |
| Deterministic replay | exact equality at every checkpoint | 1,200 ticks, checkpoints every 60, exact match | `measured` | Two sessions end at `0x26927FF799797AC9`; 239.922 ms average for the dual-session trace; bounded input-log capture is separately covered |
| Quick-smoke determinism | exact equality at every checkpoint | 240 ticks, checkpoints every 60, exact match | `measured` | Both quick traces end at `0x6D481199215B363A` |
| Typed event publish allocation | 0 B steady state | 0 B average and p99 | `measured` | 10,000 publishes after 1,000 warmups with one typed subscriber and reused event |
| GC allocations per render frame | no unbounded allocation | unknown | `target` | Backbuffer capture is diagnostic setup, not steady render measurement |
| Sprite commands | scenario-specific budget pending baseline | unknown | `unknown` | Add pass counters in renderer |
| Actual batches/draw calls | < 100 meaningful batches after atlas work | unknown | `target` | Must count SpriteBatch flushes/draw calls, not commands |
| Texture switches | bounded; numeric target pending baseline | unknown | `unknown` | No instrumented counter yet |
| Texture resources | one resource per canonical loaded source | 199 resources: 198 PNG files plus 1 fallback | `measured` | Pre-final Wave 04/05 isolated published-client scene smoke; manifest graph did not change afterward, final package smoke is pending |
| Texture frame descriptors | no first-use Draw materialization | 1,062/1,062 preloaded | `measured` | Includes 122 explicit Wave 05 source-rect frames plus 12 implicit default frames; final package smoke pending |
| Texture resource load time | numeric budget pending atlas/GPU calibration | 129.901 ms | `measured` | Pre-parser Forest scene smoke on local NVIDIA/Windows host; five-scene range 123.269..151.893 ms |
| Texture load allocations | reduce setup allocation after correctness | 6,638,968 B | `measured` | Forest thread allocation around resource creation; not steady Draw allocation |
| Decoded RGBA payload | obey per-group configured budgets | 5,453,824 B total | `measured` | Exact decoded estimate; residency telemetry splits UI/World/Entities/Backgrounds/Effects |
| GPU texture memory | budget pending atlas/group design | unknown | `unknown` | Requires graphics-resource accounting/capture |
| Visible chunks | bounded by viewport and margin | unknown | `target` | Camera/zoom-labelled streaming trace required |
| Cached chunks | bounded by configured cache budget | unknown | `target` | Cache behavior tests exist; runtime count trace missing |
| Chunk rebuild time | numeric target pending baseline | unknown | `unknown` | Per-chunk/per-frame distribution with dirty reason required |
| Streaming queue | bounded with no monotonic growth | apply queue defaults to 64; operations, time and bytes are independently bounded | `measured` | 38 focused tests cover queue pressure, oversize progress, retries and terminal failures; long camera trace still missing |
| Streaming initial-window average | numeric target pending service trace | 7.762 ms calibration; 8.841 ms quick | `measured` | Current setup/generation fixture after retry-budget integration |
| Streaming initial-window p99 | numeric target pending service trace | 8.235 ms calibration; 9.167 ms quick | `measured` | Same small surface fixture; planner separately passed a 4,097-position bidirectional trace |
| Streaming initial-window allocation | reduce after profiling | 2,564,943 B calibration average | `measured` | Includes world/service/tasks/chunk snapshots; not steady camera movement |
| Streaming cold camera trace | numeric target pending calibrated run | 135.732 ms, 14,791,032 B | `measured` | Earlier same-session BDN dry operation across negative/positive X before final Wave 05 integration; retained as smoke history, not a final-tree distribution |
| Streaming warm camera trace | numeric target pending calibrated run | 42.372 ms, 10,133,640 B | `measured` | Earlier same-session BDN dry operation across origin before final Wave 05 integration; retained as smoke history, not a final-tree distribution |
| Chunk load time | numeric target pending baseline | cumulative telemetry implemented, distribution unknown | `unknown` | Cold/warm region read distribution artifact required |
| Chunk generate time | numeric target pending baseline | cumulative telemetry implemented, distribution unknown | `unknown` | Deterministic seed/profile-labelled distribution artifact required |
| Chunk apply time | bounded by main-thread time/byte/operation budgets | all three budgets enforced; defaults 32 operations, 4 ms and 512 KiB | `measured` | Scripted-clock and byte-budget tests prove deferral plus oversize forward progress; real camera distribution remains open |
| Chunk save time | numeric target pending baseline | cumulative telemetry implemented, distribution unknown | `unknown` | Representative payload distribution required |
| Region file bytes | bounded by live chunks plus documented overhead | unknown | `target` | Inventory before/after save/compaction scenarios required |
| Lighting workspace | bounded by largest processed region | reusable light/solid/visited/queue arrays | `measured` | No per-source dictionary/queue growth; capacity increases only when a larger region is first observed |
| Lighting propagation time | numeric target pending RGB/region work | 15.62 ms at 4 chunks; 28.50 ms at 12 chunks | `measured` | Final BDN dry single-operation evidence with vertical seeds, two-pass indirect skylight and point lights; not a calibrated distribution |
| Lighting propagation allocation | minimize steady-state churn | 40 B at both 4 and 12 chunks | `measured` | Down from 210.53/618.23 KB through reusable workspaces, bitwise flags and indexed source traversal |
| Liquid active cells | bounded by loaded/dirty regions | unknown | `target` | Active-cell time series required |
| Liquid step time | numeric target pending representative baseline | unknown | `unknown` | Dirty-region-labelled distribution required |
| Entity spawn maintenance | <= 4 ms for 200-entity reference soak | 3.646 ms, 64 B | `measured` | Earlier same-session BDN dry operation with two activity sources before final Wave 05 integration; calibrated final-tree distribution still required |
| Entity AI allocation | <= 12 KiB/tick for 200 reference actors | focused soak passes | `measured` | `LivingWorldAiScaleTests` enforces the allocation ceiling; CPU distribution remains unknown |
| Spatial queries | bounded allocations; throughput target pending | unknown | `unknown` | Query/result/elapsed/allocation distribution required |
| Save time | no interrupted-write full-save loss; numeric target pending | unknown | `target` | Full/dirty/autosave scenarios with payload sizes required |
| Content load average | numeric target pending representative mod set | 56.547 ms | `measured` | Current quick load includes validated runtime clips, rigs, character/entity profiles, event triggers and all prior content |
| Content load p95 | numeric target pending representative mod set | 61.223 ms | `measured` | Current quick sample; tail remains provisional |
| Content load allocation | reduce after correctness and schema work | 2,778,368 B average | `measured` | Includes deterministic runtime-animation/entity-profile construction plus event-trigger validation |
| Simple world generation average | numeric target pending reference profile | 28.044 ms | `measured` | Current quick sample, 256x128, seed 1337, regional structures enabled |
| Simple world generation p95 | numeric target pending reference profile | 29.452 ms | `measured` | Current quick sample |
| Simple world generation allocation | reduce after profiling | 17,052,112 B | `measured` | Regional planning/materialization remains a profiling target |
| Startup time | numeric target pending process timer | unknown | `unknown` | Smoke exits successfully but does not record process-start-to-frame duration |

## Interpretation

- The fixed-tick fixture now includes guard/combat, living-world spawn context, active enemies, drops, inventory, equipment and farm plots, but it is not the separate 200-entity soak.
- The configured quick-smoke guardrails pass: 0.131 ms p95 is below 4 ms and 7,250 B average allocation is below 16,384 B. These are regression limits, not ideal production budgets.
- Snapshot ownership transfer, struct enumeration/value snapshots and bitwise flag checks reduced the quick checkpoint by 3,858 B/tick while preserving read-only public snapshot semantics.
- The remaining 4,376 B frame-snapshot and 1,830 B entity-phase averages are the next allocation targets.
- Named streams plus atomic random/event sidecar recovery, mid-trace continuation and dual-session replay now prove the default session-owned deterministic path locally.
- Typed event publication is now allocation-free in the isolated steady-state fixture; subscription changes deliberately allocate snapshots outside the hot path.
- Background streaming is operationally bounded across concurrency, queue length, operations, elapsed apply time, decoded bytes and retry attempts, but the initial-window benchmark is a setup fixture rather than a representative camera trace.
- Texture correctness is now measurable through 199 resources and 1,062 frames. The all-content preload is acceptable for the current sample but is not a scalable policy.
- No before/after speedup is claimed. Before this slice, texture load/resource/frame telemetry did not exist.
- Quick profile results are executable CI regression smoke against configured guardrails; they are not calibrated production distributions.

## Next Measurement Package

Epic 2 Milestone 4 now has recovery-safe world events, bounded feedback/replay diagnostics, a reusable phased combat runtime and retry/time/byte-budgeted streaming. The next performance slice must export representative and 200-entity phase distributions, reduce snapshot/query allocations without mutable leakage, and record long cold/warm camera traces from the new streaming telemetry. Renderer work still needs unobscured encounter captures, actual draw-call, texture-switch, GPU-time and 1080p/1440p evidence before presentation budgets can tighten.
