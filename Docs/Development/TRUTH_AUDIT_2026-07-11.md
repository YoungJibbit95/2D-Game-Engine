# YjsE Truth Audit - 2026-07-11

## Baseline Identity

- Repository: `YoungJibbit95/2D-Game-Engine` / YjsE
- Branch: `master`
- HEAD at audit start: `f9a06072a1172d2bd8d064cabad92235443dd8c8`
- Upstream: `origin/master`
- Host: Windows 11 build 26100, `win-x64`
- Selected SDK: .NET SDK `8.0.420` through the repository `global.json`

## Working Tree At Audit Start

The audit started with a large pre-existing dirty working tree. It contains an inventory/crafting/item overhaul, new feedback/particle foundations, content additions, and nine regenerated production-sprite candidates. No pre-existing change may be discarded or treated as a clean baseline.

- 56 tracked files differed from HEAD.
- 47 collapsed `??` status entries represented 70 untracked files.
- The largest tracked deltas were in `CraftingOverlay`, `InventoryOverlay`, `CraftingSystem`, `Inventory`, and `PlayerInventory`.
- Nine tracked PNG files had been replaced with larger sprite candidates while their manifest/brief dimensions were still unchanged.

The exact file list remains available from `git status --short`; session-level summaries belong in `WORK_LEDGER.md` rather than being duplicated here.

## Initial Validation Evidence

Commands run before productive code changes:

```powershell
dotnet --info
dotnet restore YjsE.sln
dotnet build YjsE.sln -c Debug --no-restore
dotnet test YjsE.sln -c Debug --no-build
dotnet build YjsE.sln -c Release --no-restore
dotnet test YjsE.sln -c Release --no-build
```

Results:

- Restore: passed.
- Debug build: passed with 0 warnings and 0 errors.
- Release build: passed with 0 warnings and 0 errors.
- Debug tests: 459 passed, 2 failed, 461 total.
- Release tests: 459 passed, 2 failed, 461 total.

The two baseline failures are:

1. `GameplayFeedbackRouterTests.MiningLifecycle_EmitsThresholdedCuesAndCompletion`: expected four cues, received three.
2. `SpriteAssetTests.RepositorySpriteAudit_RecognizesGeneratedStarterAssets`: nine PNG dimensions disagree with `Game.Data/assets/sprites.json`.

No claim that the older documented count of 406 tests is current or green is valid. The observed current suite contains 461 tests and is red at the audit baseline.

## Confirmed Project Direction

- `Game.Client` references `Game.Core`; `Game.Core` does not reference `Game.Client`.
- `Game.Tests` references only `Game.Core` among repository projects.
- No MonoGame/XNA/`Texture2D`/`SpriteBatch` reference was found under `Game.Core` in the initial dependency scan.
- The repository currently has no tracked `.github` CI workflow, benchmark project, or explicit client nonblank-smoke tool.
- The existing `global.json` requests SDK `8.0.100` but permits `latestMajor`; this is not reproducible enough for the stated control-plane goal even though this host selected `8.0.420`.

## Audit Work Still In Progress

The following sections are intentionally not marked complete until code-path inspection and runtime validation finish:

- Actual `GameSimulation` ownership in the live MonoGame path.
- Synchronous I/O/world generation/decoding reachable from `Draw`.
- Texture resource lifetime, placeholder sharing, and multi-frame sheet behavior.
- Hot-path allocation and string-key construction.
- Content cross-reference validation, save fixtures, and negative-X suites as named gates.
- Client startup and proof of a nonblank rendered frame.
- Measured performance and allocation baselines.

Final findings and changed acceptance status are recorded in the capability matrix and work ledger at session close.

## Final Integrated Findings

The audit-start facts above remain immutable baseline evidence. The following describes the working tree after the requested control-plane and first Epic 1 slice.

### Validation Outcome

- Both baseline failures were repaired without discarding the pre-existing inventory/crafting/asset work.
- Locked restore, C# style verification and Debug/Release builds pass; both builds report 0 warnings and 0 errors.
- The expanded suite passes 485/485 in both Debug and Release.
- Focused local gates pass: sprite 11/11, content/cross-reference 9/9, negative-X/world/save 48/48.
- Strict Python audit v2 reports 0 hard issues, 0 true duplicate groups and 6 explicit source-alias groups.
- A published Release client physically outside the repository loads its own `yjse.game.json`/`Game.Data` and produces a validated nonblank frame.

### Dependency And Packaging Truth

- `Game.Core` remains renderer-neutral and does not reference MonoGame or `Game.Client`.
- `Game.Tests` now also references `Game.Client` intentionally to exercise texture/resource and smoke-option contracts. Production dependency direction is unchanged.
- `Game.Benchmarks` references `Game.Core` only.
- Client output/publish contains the project manifest and runtime content. Authoring-only `asset_briefs` and `art_direction` trees are excluded.
- Per-sprite `SourceRoot` is assigned by the base/mod loader and retained through override merge. The renderer no longer guesses every sprite from one base root.

### Active Runtime Truth

- `PlayingState` is still the active MonoGame gameplay state and still orchestrates world time, player, entities, pickups, farming and other phases directly.
- `GameSimulation` is tested and benchmarked, but it is not yet the single authoritative live-client host. Epic 1 Milestone 3 remains `partial`/not started in the client.
- Camera follow and visible-chunk planning now execute in Update before the streaming call. `Draw` no longer invokes `EnsureVisibleChunks`.
- `ChunkStreamingService.Update` still synchronously reads/generates/saves/unloads chunks. Moving work out of Draw is verified; background streaming is not implemented.

### Texture And Draw-I/O Truth

- Texture resources are keyed by canonical per-pack source path; frame descriptors are keyed separately by asset ID and frame index.
- All 683 current frame descriptors plus the system fallback are materialized before Draw. Unknown-ID fallback lookup no longer performs a first-use file check or allocation in Draw.
- Wrong decoded dimensions, absolute authored paths and source-root escapes fail with diagnostics.
- Six duplicate source files were removed; stable roles use `sourceAliasOf` and share canonical paths.
- Current correctness-first preload holds all 125 resources until state disposal. This is safe for the current sample but unbounded for production-scale waves.

### Hot-Path And Determinism Truth

- Chunk render-cache trim/LRU no longer creates per-frame `ToHashSet`, `Keys.ToArray` or LINQ collections.
- A 10,000-tick synthetic calibration measures about 1,040 B allocation per fixed tick, so the steady-allocation target is not met.
- Uncontrolled/default RNG creation remains in active or reachable paths including `GameSimulation`, `PlayerItemUseSystem`, `SpawnScheduler`, `StatusEffectApplier`, farming defaults and client `Random.Shared` conveniences. Named serializable RNG streams and replay/state hashes remain future Epic 1 work.

### Persistence Truth

- Existing save/load and negative-X contracts pass in the full and focused suites.
- This session did not change a save format.
- Recovery-safe migrations, rotation/corruption reporting and background dirty-save jobs remain incomplete.

### Measured Scope

- Base content load calibration: 27.132 ms average, 31.179 ms p99 over 8 post-warmup samples.
- Simple 256x128 world generation: 13.448 ms average, 15.194 ms p99 over 8 post-warmup samples.
- Synthetic fixed tick: 0.00371 ms average, 0.0056 ms p99 over 10,000 post-warmup ticks; this is not a representative reference-game capacity result.
- All-content texture preload: 86.966 ms, 2,819,248 B measured load allocations, 2,064,384 B decoded RGBA payload estimate.

The exact resume contract and all limitations are maintained in `WORK_LEDGER.md`; hosted Windows/Ubuntu workflow evidence remains unverified because no commit or push was authorized.
