# YjsE Dependency Map

Last updated: 2026-07-14 after final integrated Debug/Release, publish and runtime-contract validation

Capability status values are limited to `verified`, `implemented-unverified`, `partial`, `planned`, `blocked`, and `deprecated`.

## Project Graph

```text
yjse.game.json / external game root
              |
              v
         Game.Data
       sample/dev content
              |
              v
Game.Client ---------> Game.Core <--------- Game.Tests
MonoGame adapter       renderer-neutral       | correctness and
and concrete UI        engine/runtime         | contract tests
      ^                                        |
      +----------------------------------------+
             client rendering contract tests
                            ^
                            |
                      Game.Benchmarks
                      lightweight harness
```

| Dependency rule | Status | Evidence | Enforcement gap |
| --- | --- | --- | --- |
| `Game.Client` may reference `Game.Core` | `verified` | Project reference in `Game.Client.csproj` | No automated graph test |
| `Game.Tests` may reference `Game.Core` and `Game.Client` | `verified` | Project references in `Game.Tests.csproj` | Client tests remain contract-level; no live graphics device in xUnit |
| `Game.Benchmarks` may reference `Game.Core` | `verified` | Project reference in `Game.Benchmarks.csproj` | Quick scenarios do not yet cover streaming or persistence |
| `Game.Core` must not reference `Game.Client` | `verified` | Audit-start source/project scan | Add forbidden-reference CI check |
| `Game.Core` must not reference MonoGame/XNA | `verified` | Audit-start scan found no such reference | Add source/assembly boundary test |
| `Game.Data` is replaceable content, not a compiled engine dependency | `verified` | `yjse.game.json`, project loader and isolated publish | Runtime publish excludes `asset_briefs` and `art_direction`; no installer yet |
| External games select content through `yjse.game.json`/environment contracts | `partial` | Project resolver, per-pack sprite roots and OS-temp publish smoke | No separate external-repository fixture yet |

## Repository Projects

### Game.Core

Status: `partial`

Owns renderer-neutral simulation, world, content contracts, gameplay, persistence, runtime services, diagnostics and UI models.

Direct package dependencies:

| Package | Purpose | Status |
| --- | --- | --- |
| K4os.Compression.LZ4 1.3.8 | Save/chunk compression | `verified` |
| MessagePack 3.1.8 | Save/chunk payload serialization | `verified` |
| MoonSharp 2.0.0 | Future Lua/mod scripting foundation | `implemented-unverified` |
| Serilog 4.3.1 | Logging contract | `implemented-unverified` |
| Udun.FastNoiseLite 1.0.1 | World-generation noise | `verified` |

Boundary risks:

- The active event snapshot, cooldowns and bounded journal do not yet have a versioned recovery-safe persistence sidecar.
- Immutable frame snapshots and spatial/query preparation still allocate 6,237 B in the current representative calibrated fixed tick.
- Rare-loot event modifiers and player-action event triggers are not yet routed through authoritative gameplay.

### Game.Client

Status: `partial`

Owns the MonoGame host, graphics device, input adapter, rendering, states and concrete widgets. It references `Game.Core` and copies the selected content pack into output.

Direct package dependencies:

| Package | Purpose | Status |
| --- | --- | --- |
| MonoGame.Framework.DesktopGL 3.8.4.1 | Cross-platform client host/rendering | `implemented-unverified` |
| ImGui.NET 1.91.6.1 | Debug UI foundation | `implemented-unverified` |
| Serilog 4.3.1 | Client logging | `verified` |
| Serilog.Sinks.Console 6.1.1 | Console sink | `verified` |

Boundary risks:

- The final Release publish and five current semantic scene smokes succeed; local visual QA covers Amber Grove and Twilight Marsh while hosted and encounter-focused evidence remain separate.
- `PlayingState` adapts the session-owned `GameSimulation` and immutable snapshot, but some world/render adapters still read the authoritative live world by design.
- Texture groups, budgets, pinning and LRU eviction are active; atlas pages and measured GPU memory/batch/texture-switch counters remain absent.

### Game.Data

Status: `partial`

Owns replaceable sample/dev JSON and sprite content. Runtime code reaches it through content-root/project contracts; it is not a C# project reference.

Key dependency direction:

```text
definitions -> registries -> cross-reference validator -> session bootstrap/runtime users
sprite briefs -> sprite manifest -> PNG files -> audit -> client texture resolution
```

Boundary risks:

- Not every manifest asset has a verified runtime user, atlas assignment or provenance record.
- Authoring intermediates must not enter runtime packaging.
- Linux case sensitivity is only verified after Ubuntu gates run.

### Game.Tests

Status: `partial`

Owns Core correctness, repository content contracts, and selected `Game.Client` rendering/CLI contracts. Production dependency direction is unchanged.

Direct package dependencies:

| Package | Purpose | Status |
| --- | --- | --- |
| Microsoft.NET.Test.Sdk 18.5.1 | Test host | `verified` |
| xUnit 2.9.3 | Test framework | `verified` |
| xunit.runner.visualstudio 3.1.5 | Test discovery/runner adapter | `verified` |

Audit-start evidence was 459/461 in both Debug and Release. Current post-integration local evidence is 995/995 in both configurations; hosted matrix evidence remains pending.

### Game.Benchmarks

Status: `verified`

Owns the Release harness for content loading, representative simple world generation, initial background streaming, state hashes, deterministic replay and fixed-simulation timing/allocation distributions. It references `Game.Core` and writes environment-labelled JSON reports.

Boundary risks:

- Local Windows Release quick and calibration profiles write versioned JSON with constant inputs and environment/revision metadata; hosted results remain pending.
- The harness is not a substitute for client-render/GPU, recovery persistence or long-session measurements.
- The final quick FixedTick gates pass at p95 0.1917 ms against 4 ms and 11,099 B average allocation against 16,384 B; streaming/spawn dry smokes are not calibrated distributions.

## Runtime Dependency Flow

| Flow | Status | Current boundary | Next acceptance criterion |
| --- | --- | --- | --- |
| Input -> commands -> Core simulation | `verified` | Variable Update latches commands/item-use requests; one session-owned fixed-tick simulation applies them | Keep one-host and phase-order tests green |
| Core state -> render snapshot/queries -> Client renderer | `verified` | Active player/entity/farm/HUD presentation consumes immutable snapshots; tile/lighting/streaming adapters retain explicit live-world access | Add previous-frame interpolation without exposing mutable simulation state |
| Project/base/mod roots -> loaders -> registries -> runtime | `verified` | Repository tests and OS-temp project smoke cover current data/provenance | Hosted content gate and external-game fixture |
| Sprite ID -> per-pack source root -> canonical resource -> frame -> draw | `partial` | Resource/frame ownership plus bounded residency groups are verified; atlas/counters remain | Atlas pages plus measured batches, texture switches and GPU memory |
| World mutations -> dirty regions -> streaming/save/render work | `verified` | Draw is free of generation/decode/I/O; cancellable background load/generate/save jobs feed a bounded Update apply queue | Add retry/backoff plus calibrated apply time/byte limits |
| Session state -> save coordinator -> filesystem -> load coordinator | `partial` | Round trips exist; recovery/migrations incomplete | Atomic/recovery-safe compatibility suite |
| Typed Core events -> client audio/particles/UI | `partial` | Feedback integration is incomplete | Typed event consumers with bounded allocation |

## Control-Plane Dependencies

```text
global.json
    -> Directory.Build.props + .editorconfig
    -> restore
    -> format
    -> Debug/Release build
    -> focused asset/content/negative-X tests
    -> full tests
    -> client nonblank smoke
    -> quick benchmark report
    -> quick and integrated calibration artifacts
```

| Control capability | Status | Dependency |
| --- | --- | --- |
| Exact SDK | `verified` | Local SDK 8.0.420 selected; hosted result pending |
| Analyzer and format gates | `verified` | Local C# style and Debug/Release warning gates pass |
| Windows/Ubuntu CI | `implemented-unverified` | GitHub-hosted runners and workflow execution |
| Full correctness gate | `verified` | Local Debug and Release are 995/995; hosted Windows/Ubuntu remain separate |
| Nonblank smoke | `verified` | Five current OS-temp semantic scenes and local visual QA pass; virtual-display Ubuntu scenes remain separate |
| Benchmarks | `verified` | Local quick/calibration profiles pass; hosted artifacts pending |
| Package locked restore | `verified` | Four lockfiles exist and local `--locked-mode` restore passes; hosted result pending |

## Prohibited Dependencies

- `Game.Core` -> MonoGame/XNA/graphics device/client widgets.
- Renderer/client overlay -> authoritative gameplay rules not represented in Core.
- Draw -> synchronous disk I/O, chunk decoding, world generation or region serialization.
- One sprite frame -> a duplicate GPU texture when source rectangles can share one resource.
- Concrete game balance/progression -> hard-coded engine/client switches.
- Save-format revision -> no migration/compatibility/recovery path.
- Non-primary genre module -> unrelated expansion while the active reference gate is open.
