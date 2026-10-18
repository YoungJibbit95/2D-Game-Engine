# PR-0B / YJSE-P0-002: reproducible Golden Scenes

Date: 2026-10-18. Branch: `codex/yjse-p0-002-golden-scenes`.
Base: `502720e8885c5250a6bc4469847c3694681f28e7`, the manually merged PR-0A on `origin/master`.
Frozen capture implementation: `9385ed33bbdfc78d40501c1379b4b429086c0a60`.
Pull request: [#7](https://github.com/YoungJibbit95/2D-Game-Engine/pull/7).
Hosted implementation acceptance: **pass**, all five jobs in [run 39109396729](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/39109396729) at `7655578e863d5ed7786553adfc099ecb95d03f5f`. Subsequent evidence and CI-isolation commits retain normal CI; [current PR checks](https://github.com/YoungJibbit95/2D-Game-Engine/pull/7/checks) govern the latest head. Human review/manual integration only.

The original `main` checkout at `F:\Coding\2D Pixel Game Engine` and its 96 modified tracked / 185 untracked files are preserved. This PR uses `F:\Coding\YJsE-PR-0B`. Feature-branch commits, push and PR creation are explicitly authorized; no automatic merge or force-push is permitted.

## Scope and fixture contract

Four versioned fixtures extend `tools/run_client_smokes.ps1`; they use the same published MonoGame client, strict texture preload, real backbuffer readback, fresh PNG/JSON checks, logs and process deadline as the existing three smoke states. There is no alternative renderer or second gameplay simulation. Core, rendering architecture, lighting calculations, world generation, assets and save formats are unchanged.

All fixtures use seed **1337**, 960x540, zoom 2, clear weather with zero intensity, fixed 1/60-second presentation steps, 180 presentation frames, 60 warmup frames and 120 measured frame intervals. Playing advances the authoritative `GameSimulation` exactly once per frame to tick 180; World Select has simulation tick 0. Golden captures require an empty absolute `YJSE_USER_DATA_ROOT`, so saves, bindings and video settings cannot leak from a user's profile. Physical input, console input and autosave are disabled for these diagnostic captures. Default isolated settings and actual viewport dimensions are checked.

| Scene | Biome/profile | Time of day | Camera X / Y (world pixels) | Dynamic lights | State hash at tick 180 |
| --- | --- | --- | --- | --- | --- |
| `forest_day` | Forest | 0.5 (noon) | -1046522 / 926 | none | `45FBDB83749AB51A` |
| `forest_night` | Forest | 0 (midnight) | -1046522 / 926 | 2 | `4FF97C2E20086F72` |
| `cave_lit` | Crystal Depths | 0.5 | -1045562 / 1886 | 2 | `C34F60940318003D` |
| `world_select` | empty isolated world library | presentation only | fixed UI viewport | none | not applicable |

The existing forced-biome resolver chooses these negative-X positions. A bounded chunk neighborhood is synchronously prepared before the measurement window; 55 chunks remain resident in the three current gameplay fixtures, with no asynchronous streaming requests during capture. Camera, zoom and world clock remain fixed. Weather retains its deterministic seed-derived wind, which is recorded and compared. Autonomous enemy spawning is disabled through existing settings; player physics and the authoritative simulation still execute normally.

Night and cave spawn two existing authoritative projectile types (`arcane_mote`/Magic and `firebolt`/Fire), initially 96 world pixels to either side of the camera, moving outwards at 6 pixels/second. Existing projectile updates, renderers and light collection are used. Acceptance requires two actual collected dynamic lights, not just two requested lights. This is a controlled moving-light stimulus, not a combat playthrough. World Select intentionally covers the empty-library layout; populated lists, scrolling, selection and interaction remain future fixtures.

## Running and inspecting comparisons

From the repository root, with .NET 8.0.420, PowerShell 7, Python and the existing `Game.Data/art_direction/requirements.txt` installed:

```powershell
dotnet restore YjsE.sln --locked-mode
dotnet publish Game.Client/Game.Client.csproj -c Release --no-restore -o ../YJsE-PR-0B-publish
./tools/run_golden_scenes.ps1 -PublishRoot ../YJsE-PR-0B-publish -OutputDirectory artifacts/golden-scenes
```

Ubuntu software rendering uses `LIBGL_ALWAYS_SOFTWARE=1 SDL_AUDIODRIVER=dummy xvfb-run -a pwsh -NoProfile -File tools/run_golden_scenes.ps1 -PublishRoot <publish-root> -OutputDirectory artifacts/golden-scenes`. Hosted Windows uses PR-0A's signed process-local MSYS2 Mesa installation. The wrapper records native Windows GPU/driver identity, Windows Mesa DLL hashes or the Ubuntu Mesa package version; `YJSE_RENDER_ENVIRONMENT` can explicitly identify another controlled implementation. Actual adapter, profile, architecture, runtime and MonoGame versions are also captured. Rolling Mesa packages are recorded, not claimed to be frozen.

Each run gets a GUID directory. Each scene starts **two fresh client processes** and always requires exact equality between their decoded pixels and `GoldenScene`/`CaptureEnvironment` metadata. An exact failure is never retried as a tolerant success. The second capture is also compared against `tools/golden-scenes/baselines/reference/<scene>.png` and its JSON sidecar:

- `-BaselineMode auto` (default): exact if the full render environment matches; otherwise portable.
- `-BaselineMode exact`: requires identical render environment and every decoded pixel. PNG compression bytes are irrelevant.
- `-BaselineMode portable`: still requires identical fixture, camera, clock, content/settings fingerprints, state hash and light/chunk counts. Pixel acceptance requires mean absolute RGBA channel error <= 2.0 and at most 1% of pixels with any channel error > 12, on the 0-255 scale. Dimensions and original image mode must match. There is no blur, alignment, resize, masking or automatic threshold relaxation.

Portable comparison accommodates rasterization/driver differences; it cannot guarantee detection of every small visual defect. A fresh-process exact check can detect nondeterminism but alone cannot detect a stable regression; the separate versioned-baseline check supplies that gate. Renderer-neutral state checks stay exact on every platform.

`exact/` and `baseline/` each retain `comparison.json`, an 8x amplified `diff.png` (including alpha-only errors) and `preview.png` in reference/candidate/diff order. JSON includes raw and pixel SHA256, changed-pixel count/fraction/bounds, mean/max error, thresholds, fixture/environment differences and timing diagnostics. Capture logs and original sidecars remain beside them; suite `summary.json` lists failures even if a later scene fails. CI uploads all of these artifacts on failure as well as success.

## Baseline provenance and timing limits

The four checked-in PNGs were captured from a clean build of `9385ed3` and each matched a second fresh process exactly. They were visually inspected. Sidecars retain unmodified fixture/environment data, frame timing and selected CPU scopes (`Frame.Update`, `Frame.Draw`, `Simulation.FixedUpdate`, lighting preparation/mask/upload, background and tilemap). The complete capture contains further renderer/resource telemetry; the checked-in subset avoids unrelated repetitive metrics and absolute machine paths. PNG hashes, source revision, actual loaded Client/Core assembly informational versions and SHA256 hashes are recorded. The wrapper's checkout revision is separate from binary provenance, so an older published bundle cannot masquerade as newly built code.

Reference host: Windows 10.0.26100, AMD Ryzen 5 5500 (12 logical processors), NVIDIA RTX 3070 Ti, driver 32.0.16.1692, .NET 8.0.28, MonoGame 3.8.4.1, Reach profile, Release. Values below are milliseconds from the recorded second captures; full precision and peaks are in the sidecars.

| Scene | Frame mean / p95 / p99 | CPU Update EMA | CPU Draw EMA | Lighting preparation EMA |
| --- | --- | --- | --- | --- |
| Forest day | 16.667 / 16.668 / 16.675 | 0.731 | 0.907 | 0.604 |
| Forest night | 16.667 / 16.668 / 16.669 | 1.510 | 0.903 | 1.626 |
| Lit cave | 16.667 / 16.668 / 16.668 | 2.634 | 0.635 | 2.740 |
| World Select | 16.667 / 16.667 / 16.668 | 0.002 | 0.329 | not applicable |

CPU scopes and frame intervals use real `Stopwatch` time. Frame percentiles use nearest rank; profiler averages are **exponential moving averages**, not arithmetic means. The 60 FPS limiter dominates frame means and does not establish throughput or a 120/144/165 Hz performance result. Screenshot readback is excluded from the CPU Draw scope. GPU execution time remains **unknown**; the upload scope measures CPU submission/wait time. JIT, caching, background load and scheduling affect these short 120-sample observations (night maximum was 20.402 ms). They are reviewable reference observations, not calibrated regression thresholds or evidence of an optimization. Existing isolated Release performance budgets and the quick Core harness remain the quantitative timing gates. Future renderer/lighting work needs matched before/after longer runs and genuine GPU timing where available.

### Updating a baseline

Use `-CaptureOnly` explicitly to bootstrap captures into ignored artifacts; CI never passes this switch. Review the scene semantics and exact repeated-process result before copying an actual candidate PNG/JSON to the reference directory. Keep provenance and real measurement values; document intentional fixture/content changes and inspect the diff. Do not replace a failing reference or raise tolerances automatically. A changed content fingerprint or state hash requires explaining and reviewing the underlying change even when pixels look similar. Re-run the normal suite against the proposed references, plus Windows/Ubuntu Debug/Release CI. `BaselineArtifact.PNG_SHA256` is provenance; pixel comparison intentionally ignores PNG compression differences.

## Validation and handoff

| Gate | Result / evidence |
| --- | --- |
| Locked restore, Debug/Release build | pass, zero warnings/errors |
| Functional / strict allocation suites | local Debug/Release pass 1,731 ordinary + two isolated strict allocation cases (same 1,733 total), including eight new fixture/clock/isolation/determinism cases; original combined 1,733 suite also passed locally and in complete hosted run 39109396729 |
| Image comparator regressions | pass, 13/13; compression independence, one-pixel/alpha/size/mode/metadata changes, portable thresholds and invalid input |
| Style and whitespace | style verify and `git diff --check` pass |
| Existing strict audits/previews | pass, both manifests zero hard issues, unchanged UI/Wave05 references and seven preview regression tests |
| Existing Release timing gates | pass, all 21 isolated classes / 39 tests at unchanged budgets; quick harness passes with matching 240-tick hash `0x2B10E91090EC0C22` |
| Website | `npm ci` and `npm run validate` pass (zero Svelte diagnostics); npm reports four advisories, one moderate/three high, in the unchanged dependency lockfile |
| Native Release Golden capture | pass, four exact pairs from clean frozen code; logs `artifacts/golden-frozen.log` |
| Debug against versioned baselines | pass, four fresh-process exact pairs and four exact versioned-reference comparisons with rebuilt frozen-code Debug binaries; `artifacts/golden-debug-final.log` |
| Ordinary published three-state smokes | pass, native Release Main Menu/World Select/Playing, 263 resources / 1,518 descriptors / zero invalid resources; `artifacts/ordinary-final.log` |
| Hosted Windows/Ubuntu Debug/Release | pass, complete run 39109396729 at 7655578; each cell passes 1,733 functional tests, 13 comparator regressions, ordinary smokes and all four Golden exact-pair + portable-baseline gates. Both Release jobs also pass 21 isolated classes / 39 tests and quick harness; both Debug jobs pass strict audits, unchanged previews and seven preview regressions |

All four hosted Golden artifacts from that exact PR run were downloaded and checked: every repeated capture has zero changed pixels; every fixture/state comparison has zero differences. Native-to-Mesa portable mean RGBA error is 0.019109/0.020667 for Forest day, 0.095647/0.097894 for night and 0.111442/0.114834 for cave (Windows/Ubuntu respectively); World Select is pixel-exact across all environments. Debug and Release produce the same per-OS image metrics. At most 47 of 518,400 pixels exceed channel delta 12 (0.0091%); the maximum delta of 209 is confined to a few UI glyph-edge pixels. Independently inspected Night/Cave previews and amplified diffs preserve terrain, camera and both lights, with low-amplitude light-falloff differences. Driver precision is a plausible explanation, not a traced proof of the cause. No tolerance was raised or reference replaced to pass hosted CI.

The first push run [39109371377, attempt 1](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/39109371377/attempts/1) failed the unchanged `FallingLeafPlannerTests.Plan_RemainsAllocationFreeWithCallerOwnedBuffer` on Ubuntu Release (4,744 measured bytes versus 0; 1,732/1,733 functional tests). The parallel complete PR run passed 1,733/1,733 on identical code. One unchanged-job rerun passed functional/performance steps before normal push-concurrency cancellation. The next evidence-only push [39109839235](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/39109839235) instead failed the unchanged `GameplayParticleSystemTests.AmbientForestCanopy_RemainsAllocationFreeAfterWarmup` (2,960 bytes versus 0), while its parallel PR run passed. Both measured paths use the same unchanged allocation-free leaf planner. There is no allocation stack trace identifying the cause; neither thread interference nor JIT is claimed as a proven explanation.

The final workflow therefore uses the **existing** `run_isolated_performance_tests.ps1 -StrictAllocationOnly` runner for these two `Category=StrictAllocation` Facts, in Windows/Ubuntu Debug **and** Release. Each case gets its own fresh `dotnet test` process, exact FQN filter, GUID output and TRX proving exactly one executed/passing test. Empty discovery, invalid names, process failures, missing TRX and incorrect result counts fail the gate. Test bodies, warmup loops and exact **0 B** assertions are unchanged. There are no retries or tolerance changes. Functional coverage is partitioned into **1,731 ordinary + two strict isolated tests**, with the existing 21 Release timing classes / 39 tests unchanged. This controls process history and improves diagnosis; it does not establish the cause of the earlier unassigned allocations. An isolated failure requires allocation tracing, not blind reruns. Local Debug/Release ordinary partitions pass 1,731 each, and strict runs each pass both cases (`artifacts/allocation-debug.log`, `allocation-release.log`); the normal current-head matrix is required for hosted acceptance of this final partition.

Two genuine subagents had separate responsibilities: an isolated image-comparator implementation and a read-only runtime/reproducibility review. The lead integrated and executed validation. Independent frozen-code/baseline review and hosted visual-artifact review have no open blocking findings; reviewers did not execute builds/tests themselves.

Rollback: revert the PR commits. No asset, Core, save-format or renderer migration needs reversal. After manual integration, the next separate PR should establish the explicitly scoped Renderer V2/Lighting V2 comparison using these fixtures and matched measurements; Worldgen V2 remains separate. Do not automatically start or merge those changes.
