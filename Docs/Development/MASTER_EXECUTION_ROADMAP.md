# YjsE Master Execution Roadmap

Last updated: 2026-07-15 after world-event persistence, exact-once gameplay routing, streaming-aware lighting and active entity animation

## Evidence And Status Contract

The source-of-truth order is current code, validation from the current checkout, the capability matrix, architecture decisions, the work ledger, and then older documentation. Capability status values are limited to `verified`, `implemented-unverified`, `partial`, `planned`, `blocked`, and `deprecated`.

The audit-start baseline and later work must remain distinct:

- Audit-start HEAD: `f9a06072a1172d2bd8d064cabad92235443dd8c8` on `master`.
- Audit-start Windows Debug and Release builds: passed with zero warnings and errors.
- Audit-start Windows Debug and Release tests: 459 passed, 2 failed, 461 total in each configuration.
- Current local Debug and Release builds pass with zero warnings/errors; the expanded suites pass 995/995 in each configuration and the current implementation scope passes format verification.
- Focused local gates cover UI interaction, layered animation, guard/projectiles, distributed spawning and 200-entity scale, regional structures, mined-occluder lighting/reflections, living-world events, RNG/save resume, texture residency and content separation.
- Linux validation status: `implemented-unverified`; the workflow exists, but no hosted Ubuntu result has been recorded yet.
- Current control-plane validation: exact SDK selection, locked restore, Debug/Release 995-test suites, scoped format verification, strict asset/preview gates, published-client scene smokes, local visual QA and benchmark profiles pass locally. Hosted evidence remains separate.

## Product Goal

Build YjsE into a stable, deterministic, reusable .NET 8 2D engine with renderer-neutral simulation, replaceable game content, safe persistence, measurable performance, and production-capable client and tooling boundaries.

## Reference Game Goal

The primary reference game is a Terraria-like sideview sandbox. It proves infinite horizontal worlds, streaming, rendering, mining, building, physics, liquids, lighting, inventory, crafting, equipment, combat, enemies, loot, saves, mod content, UI, audio, and particles through one coherent runtime path.

Farming, topdown, dialogue, and shop modules remain compilable and tested. They do not receive new genre foundations while the active epic exit gate remains open.

## Epic Sequence

| Epic | Goal | Status | Depends on | Exit summary |
| --- | --- | --- | --- | --- |
| 0 | Engineering Control Plane | `partial` | none | Reproducible SDK, Windows/Linux CI, green Debug/Release tests, analyzer/format gates, strict asset gate, nonblank client smoke, benchmark project, recorded baselines |
| 1 | Runtime Convergence And Critical Hot Paths | `verified` | Epic 0 local gates | One session-owned simulation, immutable snapshots, background streaming, deterministic replay, texture ownership and Draw boundaries pass locally; continued allocation reduction is tracked in Epic 2 |
| 2 | Living Sandbox And Developer Experience | `partial` | Epic 1 | Persist phased world events, finish modifier routing, externalize presentation authoring and calibrate renderer/entity/streaming costs |
| 3 | Streaming And Region Persistence V2 | `planned` | Epic 1 | Bounded background pipeline, recovery-safe region format, migrations and compatibility tests |
| 4 | World Simulation, Physics And Entity Scaling | `planned` | Epics 1 and 3 | Measured scalable entities, physics materials, liquids, lighting scheduling and spatial queries |
| 5 | Content, Modding And Editor Tooling | `planned` | Epics 1 through 4 | Validated packaging, provenance, load order, sandboxing and production content tools |
| 6 | Terraria-like Reference Game Vertical Slice | `planned` | Epics 1 through 5 | Complete playable progression loop with production runtime evidence |
| 7 | Topdown/Farming Client Production Integration | `planned` | Epic 6 | Existing modules integrated through shared runtime and production client contracts |
| 8 | Splitscreen And Multi-Session | `planned` | Epics 1, 6 and 7 | Isolated sessions, input, viewports, saves and resource ownership |
| 9 | Networking | `planned` | explicit product decision and deterministic runtime | Approved networking model with authority, replication and security contracts |

## Epic 0 Milestones

| Milestone | Status | Acceptance criteria | Current evidence |
| --- | --- | --- | --- |
| Truth Audit | `verified` | Branch/HEAD/tree, dependency direction, runtime use, I/O and allocation paths, build/test/smoke evidence captured | Baseline and final integrated findings recorded with explicit remaining debt |
| Durable Development Documents | `verified` | Roadmap, matrix, ledger, budgets, ADRs, reference scope and dependency map agree | Locally cross-reviewed and updated with one exact resume point |
| Reproducible SDK | `verified` | Exact stable SDK selected with no major or preview roll-forward | `global.json` pins 8.0.420 and local selection passed; hosted selection remains separate |
| Locked Package Restore | `verified` | Every project has a lockfile and local restore runs in locked mode | Four lockfiles generated; local locked restore passed; hosted result pending |
| Analyzer And Format Gates | `verified` | C# 12, stable .NET 8 analyzers, warnings-as-errors and style verification | Local Debug/Release builds and C# style verification pass; hosted result pending |
| Windows And Linux CI | `implemented-unverified` | Debug/Release restore, build and test on both hosted OS families | Workflow implemented; no hosted run recorded |
| Debug And Release Tests | `partial` | Full suite green in both configurations and both CI OS jobs | Local 995/995 in Debug and Release; hosted matrix pending |
| Sprite Asset Gate | `partial` | Repository manifest, briefs, sources, metadata and duplicate policy fail CI on violation | Local C# and strict Python gates pass with 0 hard issues; hosted results pending |
| Client Nonblank Smoke | `partial` | Isolated publish, bounded frame capture and semantic scene/source/target assertions on Windows/Linux | Five current local OS-temp Windows scene smokes and visual QA pass; hosted Windows/Ubuntu runs remain separate |
| Benchmark Project | `verified` | Release benchmark covers fixed tick, content load, deterministic world generation, initial streaming, state hash and replay | Local quick and integrated calibration profiles write versioned JSON |
| Performance Baselines | `partial` | Versioned results populate required fields or explicitly retain `unknown` | Content/world/streaming/tick/replay/texture samples recorded; save, calibrated entity distributions and GPU render remain unknown |

## Epic 0 Exit Gate

Epic 0 remains `partial` until all of the following are true:

1. The pinned SDK is selected locally and by both hosted runners.
2. Committed project lockfiles restore successfully in locked mode locally and on both hosted runners.
3. Format and analyzer gates pass without suppressing unexplained warnings.
4. Full Debug and Release tests pass on Windows and Ubuntu.
5. Asset, content/cross-reference, save and negative-X gates pass explicitly.
6. A deterministic client smoke proves a nonblank rendered frame.
7. A benchmark target records the first versioned baselines.
8. Capability Matrix and Work Ledger link the exact evidence and next action.

## Epic 1 Dependency Order

The local Epic 1 dependency chain is complete while the hosted Epic 0 workflow remains an explicit pending gate:

1. Texture resource correctness and source-rectangle frame ownership - locally verified.
2. Streaming requests and I/O removal from Draw - verified with cancellable background jobs and bounded Update apply.
3. Active client convergence on one session-owned `GameSimulation` - verified.
4. Named deterministic RNG streams and replay/state hashes - verified.
5. Measured core hot-path allocation guardrails - verified for the current quick limits; reduction and phase export continue in Epic 2.

Any bounded dependency work returns automatically to the interrupted milestone.

## Deliberately Excluded While Runtime Gates Are Open

- New genre foundations.
- Large production asset waves without immediate runtime users.
- Networking implementation.
- Save-format changes without migration and compatibility evidence.
- Blind micro-optimizations without a baseline.
- Claims that the full suite, Linux client, nonblank frame, or performance budgets are verified before evidence exists.

## Risks

| Risk | Status | Mitigation |
| --- | --- | --- |
| Large pre-existing dirty tree obscures ownership | `partial` | Preserve user changes, use exclusive scopes, record audit-start HEAD and validate coherent slices |
| Targeted tests are mistaken for a green full suite | `verified` | Audit-start evidence remains separate; current full Debug/Release suites are 995/995 |
| Hosted workflow differs from Windows development host | `implemented-unverified` | Use exact SDK pin and Windows/Ubuntu matrix without machine-specific targeting-pack paths |
| Client build is mistaken for rendering proof | `partial` | Five current local OS-temp semantic scenes are green; detailed visual QA and hosted Windows/Ubuntu scenes remain mandatory |
| Runtime profiler is mistaken for a benchmark baseline | `partial` | Versioned calibration is separate from runtime telemetry; final BDN telemetry on/off numbers are single-operation dry smoke, not a speed comparison |
| Asset volume grows without runtime integration | `partial` | Require manifest, brief, runtime reference, audit, preview and performance impact per representative wave |

## Current Priority

The current priority is Epic 2 Milestone 4: route authoritative world-event/combat/rare-drop outcomes into bounded presentation/audio commands, make all item weapons consume reusable attack phases/combos/sweeps, add replay input-log/desync diagnostics, and harden streaming with retry/backoff plus elapsed-time/byte apply limits. Export calibrated representative/200-entity/streaming distributions while retaining the passing FixedTick gates. Hosted Windows/Ubuntu status remains pending rather than inferred.
