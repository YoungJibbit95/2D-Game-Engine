# PR-0D: Dependency-safe lighting preparation reuse

Base: human-integrated PR-0C, `de83468aa7e5d19f43634ae1df6935299f9d83d2`.
Branch: `codex/yjse-p0-004-lighting-reuse`; isolated worktree `F:\Coding\YJsE-PR-0D`.
Date: 2026-10-18. Feature commits/push/PR are authorized; human review/manual merge only.
PR: [#9](https://github.com/YoungJibbit95/2D-Game-Engine/pull/9). Frozen calibration implementation: `1cdded8d248e28ac172183eb58d63636d63047d0`.
Rebuilt policy/test freeze: `4fd2d6c88bf2b927ac9f755ef52fa8ec18effbbf`.

Decision: **keep the original path as the default; ship reuse as an explicit, reversible experiment.** The observations do not establish a general Prepare/frame/FPS benefit. Static prefix hits are real, but moving-camera costs, divergent CPU-upload/frame tails and substantial BEFORE/OFF variation prevent a default rollout.

## Delivered unit and compatibility

The reusable unit is the **raw ambient-occlusion and diffuse sky-portal prefix**, before either array becomes a final light channel. Each preparation still samples authoritative tiles and resolves surface heights. Directional occlusion, sky radiance, point-light collection/evaluation, flicker, bloom, temporal stabilization, history copies, reflections, encoding, upload hashes, uploads and drawing follow the original path. This does not skip a complete `LightingPrepare` invocation or reuse temporally stabilized pixels.

Core, assets, saves, content IDs, world generation and the original four Golden fixture/PNG/sidecar trees are unchanged. Rendering settings have no new serialized property, preserving scene/settings identity. **This PR does not fix bloom washout or the cyan cave lines.** Pixel equivalence preserves those existing observations.

The initial audit rejected setter-only output versions: `Chunk.Tiles` exposes a mutable array, generation workspaces write it directly, `World.Chunks` exposes a castable dictionary, and loads/halo writes can bypass scheduling flags. Adding incomplete counters would certify stale inputs. The bounded cache therefore compares actual packed Light/occlusion samples and dependency chunk identities, not `NeedsLightUpdate`, a hash, or a partial mutation/version graph. Source/time/weather outputs are not cached and need no new source revision contract.

## Exact dependencies and invalidation

| Dependency | Handling |
| --- | --- |
| World/session | Exact World identity; `PlayingState` explicitly resets reuse on session application, even for the same World object |
| Geometry and committed Core light | Original sampling order/coordinates; compare every fresh packed byte-light/point-occlusion sample exactly. Direct array aliases, setters, load and eventual halo writes are observed |
| Chunk residency | Ordered references including missing entries over the sampled chunk rectangle; replacement, load/unload and negative boundaries cannot be certified by equal counts |
| Surface model | Invoke the resolver at the original stage on every preparation; compare actual clamped column values and model presence. Mutable closure identity is insufficient |
| Camera/profile/resources | Exact visible rectangle and complete profile; conservative reset for camera position/zoom, viewport and resource generation changes |
| Sun, weather, ambient, source movement/removal, flicker | Continue all original per-pixel evaluation; these do not invalidate an independent geometry prefix unless sampled values or surfaces actually change |
| Temporal history/incoming bloom | Continue original state/order/settling, including the low-direct-light branch's existing incoming-bloom dependency. Never restore final/stabilized channels |
| GPU reset/recreation | Balanced DeviceResetting/DeviceReset subscriptions invalidate cache/resources; the existing Update-time resource preparation recreates textures and resets history/upload hashes |

One cache entry is bounded to 16,384 mask pixels and 128 chunk identities; unsupported footprints reset/bypass and calculate normally. No growth, LRU, second simulation, GPU texture or Draw-time I/O is introduced. Storage is allocated with renderer resources and released with them. Numeric arrays use `12*N + 4*columns` bytes; two fixed reference arrays add `256*IntPtr.Size`, excluding object/array headers. The 104x58 Medium mask therefore reserves **74,848 bytes of source-derived cache payload** on x64. This is not measured heap residency or VRAM.

`CacheCopyBytes` counts numeric sample/AO/portal/surface bulk payload copies, excluding reference stores/clears and comparison reads. It is not total cache memory traffic. Fresh dependency sampling costs remain inside `LightingMaskCpu`; misses include validation/storage overhead. Nothing is described as a zero-cost cache hit.

`DependencySamples` counts the sample footprint of a reuse lookup, not all tile reads. OFF/bypass reports zero validation samples even though the original tile sampling still executes. AO/portal hit ratios use actual preparations/lookups, not presentation-frame count. `OccluderSamples` continues counting work actually executed, so bypassing cached AO reduces that work counter without skipping source or directional-light evaluation.

## Fallback and reproduction

The original CPU path remains the default. Explicitly set `YJSE_DISABLE_LIGHTING_PREPARATION_REUSE=0` before startup to opt into this experiment, or use `-PreparationReuse on` in the existing runner. Unset/other values, including `1`, retain the original CPU path. Embedding code can set `LightingRenderer.PreparationReuseEnabled=true/false` immediately; either policy transition invalidates reuse. The preallocated payload remains until resource release, including when OFF. No setting/save migration is needed. Revert the PR to remove the feature entirely.

```powershell
dotnet restore YjsE.sln --locked-mode
dotnet build YjsE.sln -c Release --no-restore
dotnet publish Game.Client/Game.Client.csproj -c Release --no-build --no-restore -o ../YJsE-PR-0D-publish
$env:YJSE_DISABLE_LIGHTING_PREPARATION_REUSE = '0'
./tools/run_golden_scenes.ps1 -PublishRoot ../YJsE-PR-0D-publish -OutputDirectory artifacts/control-goldens -BaselineMode exact
./tools/run_lighting_diagnostics.ps1 -PublishRoot ../YJsE-PR-0D-publish -Width 1920 -Height 1080 -Frames 1800 -WarmupFrames 300 -Repeats 3 -PreparationReuse off -OutputDirectory artifacts/off-1080p
./tools/run_lighting_diagnostics.ps1 -PublishRoot ../YJsE-PR-0D-publish -Width 1920 -Height 1080 -Frames 1800 -WarmupFrames 300 -Repeats 3 -PreparationReuse on -OutputDirectory artifacts/on-1080p
python tools/lighting_reuse_compare.py --before-root <clean-base-GUID-root> --off-root <OFF-GUID-root> --on-root <ON-GUID-root> --output-dir artifacts/comparison-1080p
```

BEFORE uses a separately published clean-base binary; the current diagnostic runner's default `inherit` mode remains compatible with legacy captures. OFF/ON require the same actual loaded DLL versions/hashes and attested modes, identical settings/content/state/fixtures, renderer/driver/CPU/configuration, windows and source fixture hashes. The exporter validates every capture/report/PNG binding, requires all nine scenes with three fresh processes each, and writes 18 exact BEFORE/ON and OFF/ON image comparisons before accepting its dossier. Legacy cache counters are unavailable, not invented zeros. Original references are never overwritten.

Calibration also requires reported Release configuration, software cap `0`, presentation interval `Immediate`, identical OS build and logical processor count. Requested pacing is attested; external driver/OS pacing and waits remain unmeasured. This PR's local builds and tests do not run alongside the accepted timing cohorts; the host is not exclusive. The initial BEFORE-1080p cohort is excluded wholesale before analysis because short Python tooltests overlapped its capture period; a complete fresh 27-process cohort replaces it. This is not a selected retry of an unfavorable sample. Process roles run sequentially, not randomized/interleaved; host scheduling, temperature, power and order effects remain uncertainties.

The loaded Client/Core assembly versions and SHA256 values identify the executable. `CheckoutRevision` describes the runner checkout and can include `+dirty` while evidence/exporter/docs files change; it must not be mistaken for the source of a separately published executable. OFF and ON use identical frozen DLLs. Core source is unchanged although its assembly hash differs from BEFORE because the informational source revision changes.

After calibration, the only production-runtime change from `1cdded8` is the startup default: reuse now requires the explicit `0` opt-in. The measured ON/OFF paths use explicit environment values and their cache/preparation math is unchanged. Rebuilt native controls verify this policy separately; calibration rows retain the actual frozen DLL identities rather than being relabelled as final-head binaries. CI original Golden captures explicitly enable reuse, then diagnostic primary captures inherit the unset flag and attest default OFF; explicit ON fallback captures verify parity. Ordinary smokes retain their existing checks; their non-Golden sidecars do not carry benchmark-mode metadata.

The nine PR-0C fixtures retain their fixed seed, simulation ticks, clock/weather, camera paths, source counts and unpaced policy. Static source flicker continues normally. Separate lifecycle validation uses the same runner with `-PreparationReuse on -CompareReuseFallback -IncludeReuseLifecycle`: advancing time, natural projectile expiry halfway through the fixture, actual `GraphicsDevice.Reset` every 113 ticks and fresh-process OFF comparison. Default 480/120 inputs produce three measured resource rebuilds, source count 2 to 0 and continued checked-prefix hits.

## Measurement evidence

Accepted calibration contains **162 fresh processes**: nine unchanged PR-0C scenarios, BEFORE/OFF/ON, three repetitions and two resolutions. Each captures 1,800 fixed simulation ticks/presentation frames, discards 300 warmup frames and measures 1,500 frames. All roles execute **1,125 Prepare/Mask/CPU-upload scopes per process**; reuse skips no full preparation. Medium remains 104x58 at both resolutions. Native host: Windows 10.0.26100, Ryzen 5 5500 / 12 logical processors, .NET 8.0.28, MonoGame 3.8.4.1 Reach, NVIDIA RTX 3070 Ti / driver 32.0.16.1692. Hosted Mesa results validate correctness, not cross-platform performance gains.

Versioned [1080p per-process CSV](Measurements/PR_0D/1080p/per_process.csv) / [range and identity JSON](Measurements/PR_0D/1080p/comparison.json) and [1440p CSV](Measurements/PR_0D/1440p/per_process.csv) / [JSON](Measurements/PR_0D/1440p/comparison.json) retain every observation, including unfavorable rows. They separately report invocation/amortized mean/p95/p99/max for Prepare, Mask CPU and CPU Upload; frame interval mean/p95/p99; allocations; upload counts/bytes; cache lookups/hits/misses/bypasses/copy/payload. Legacy BEFORE cache counters are unavailable, not zero.

All six cohort repeat checks pass: 108 exact within-role comparisons. The exporter additionally accepts **36 exact cross-role image/state comparisons**, BEFORE/ON and OFF/ON for the first process of every scene/resolution, with SHA-bound raw reports/PNGs and identical scene/settings/content/environment contracts. Approved references remain unchanged. Raw traces, masks, amplified diffs and previews stay in ignored `artifacts`; the versioned JSON's `artifact_directory` values refer to the original exporter output, not checked-in image directories. [Measurement index](Measurements/PR_0D/README.md) records exact roots and reproduction/retention limits.

The tables show **ranges of individual process statistics**, rounded to three decimals. They are not pooled percentiles or confidence intervals. Full precision and all scenarios' p95/p99/allocation observations remain in the CSV/JSON.

### 1080p: Mask CPU per preparation (ms)

| Scene | BEFORE | OFF | ON | ON AO/portal hits per process |
| --- | --- | --- | --- | --- |
| forest_day | 0.863-0.927 | 0.546-0.557 | 0.422-0.594 | 1125/1125 |
| forest_night | 0.902-1.028 | 0.661-0.682 | 0.563-0.653 | 1125/1125 |
| cave_lit | 0.742-0.908 | 0.628-0.635 | 0.572-0.650 | 1125/1125 |
| forest_pan | 0.947-0.989 | 0.671-0.703 | 0.787-0.898 | 0/1125 |
| forest_zoom | 0.943-1.100 | 0.656-0.702 | 0.759-0.768 | 0/1125 |
| cave_panzoom | 0.914-0.946 | 0.646-0.678 | 0.701-0.789 | 0/1125 |
| dense_lights | 0.827-1.100 | 0.873-0.889 | 0.800-0.924 | 1125/1125 |
| night_no_bloom | 0.666-0.793 | 0.650-0.705 | 0.557-0.665 | 1125/1125 |
| core_dirty | 0.571-0.671 | 0.570-0.590 | 0.380-0.436 | 1125/1125 |

### 1080p: representative inclusive and tail costs (ms)

| Scene / role | Prepare mean | Prepare p95 / p99 | CPU upload mean | Frame p95 / p99 |
| --- | --- | --- | --- | --- |
| forest_night / before | 1.874-2.119 | 3.157-3.624 / 3.780-4.816 | 0.633-0.726 | 4.579-5.516 / 5.288-7.011 |
| forest_night / off | 1.200-1.241 | 1.915-1.969 / 2.322-2.573 | 0.276-0.293 | 2.574-2.782 / 3.003-3.342 |
| forest_night / on | 1.249-1.423 | 2.163-2.536 / 2.652-3.623 | 0.369-0.473 | 3.214-6.091 / 3.886-24.148 |
| forest_pan / before | 2.308-2.687 | 3.889-4.782 / 5.190-6.081 | 0.949-1.310 | 5.605-6.468 / 7.026-7.825 |
| forest_pan / off | 1.300-1.460 | 2.180-2.566 / 2.561-3.441 | 0.363-0.491 | 2.945-3.402 / 3.463-4.291 |
| forest_pan / on | 1.699-1.921 | 3.005-3.395 / 3.703-4.246 | 0.609-0.678 | 4.257-4.858 / 5.273-5.835 |

### 1440p: Mask CPU per preparation (ms)

| Scene | BEFORE | OFF | ON | ON AO/portal hits per process |
| --- | --- | --- | --- | --- |
| forest_day | 0.566-0.593 | 0.510-0.608 | 0.480-0.529 | 1125/1125 |
| forest_night | 0.713-0.776 | 0.655-0.828 | 0.592-0.699 | 1125/1125 |
| cave_lit | 0.668-0.861 | 0.673-0.714 | 0.479-0.595 | 1125/1125 |
| forest_pan | 0.700-0.801 | 0.740-0.794 | 0.734-0.770 | 0/1125 |
| forest_zoom | 0.755-0.799 | 0.672-0.762 | 0.691-0.945 | 0/1125 |
| cave_panzoom | 0.630-0.681 | 0.632-0.855 | 0.564-0.714 | 0/1125 |
| dense_lights | 0.847-0.943 | 0.712-0.892 | 0.670-0.725 | 1125/1125 |
| night_no_bloom | 0.676-0.765 | 0.674-0.925 | 0.510-0.591 | 1125/1125 |
| core_dirty | 0.611-0.671 | 0.628-0.881 | 0.514-0.584 | 1125/1125 |

### 1440p: representative inclusive and tail costs (ms)

| Scene / role | Prepare mean | Prepare p95 / p99 | CPU upload mean | Frame p95 / p99 |
| --- | --- | --- | --- | --- |
| forest_night / before | 1.527-1.698 | 2.777-3.087 / 3.374-3.751 | 0.507-0.584 | 3.900-4.367 / 4.584-5.203 |
| forest_night / off | 1.459-1.757 | 2.538-3.249 / 2.912-4.265 | 0.511-0.663 | 3.435-4.797 / 3.982-6.020 |
| forest_night / on | 1.671-1.881 | 2.903-3.256 / 3.551-4.858 | 0.634-0.962 | 4.734-25.246 / 5.632-26.927 |
| forest_pan / before | 1.638-1.906 | 3.100-3.565 / 3.915-4.328 | 0.623-0.811 | 4.224-5.241 / 5.285-6.180 |
| forest_pan / off | 1.639-1.874 | 2.887-3.369 / 3.739-4.378 | 0.588-0.733 | 4.497-4.854 / 5.428-5.908 |
| forest_pan / on | 1.715-1.830 | 3.002-3.428 / 3.780-4.142 | 0.654-0.757 | 4.202-4.527 / 5.031-5.554 |



### Interpretation, work and allocations

BEFORE/OFF variation is substantial even though OFF performs no reuse: 1080p day Mask means shift from 0.863-0.927 to 0.546-0.557 ms. Consequently, BEFORE/ON differences cannot be attributed to caching. Same-binary OFF/ON is the more useful control, but sequential order and uncontrolled host conditions still limit causal claims.

Some OFF/ON mask observations are favorable: `core_dirty` at 1080p changes from 0.570-0.590 to 0.380-0.436 ms, with inclusive Prepare 0.737-0.775 to 0.531-0.605 ms. This fixture forces Core recomputation with unchanged sampled output; its hits do not authorize ignoring real geometry/light-output mutations. `cave_lit` at 1440p records Mask 0.673-0.714 OFF versus 0.479-0.595 ON. These are observed subsets, not a general speedup claim.

Contrary evidence is retained: 1080p night Mask means fall from 0.661-0.682 to 0.563-0.653 ms, while inclusive Prepare rises from 1.200-1.241 to 1.249-1.423 and CPU Upload rises from 0.276-0.293 to 0.369-0.473 ms. No upload-count reduction occurs. All three no-hit 1080p motion scenarios have higher ON Mask ranges than OFF; 1440p motion ranges overlap or change direction. Do not assign all differences to cache overhead or infer a GPU/driver cause from these CPU scopes.

Large tails remain: ON night repeat 1 at 1080p has frame p95/p99 6.0912/24.1483 ms; ON night repeat 2 at 1440p has 25.2458/26.9265 ms, mean 17.9786 ms. The latter process records Prepare max 129.9306 and CPU Upload max 128.569 ms, versus Mask max 0.9572 ms. This localizes the reported scoped stall to the CPU-upload interval without identifying driver/GPU/OS causality. ON cave at 1440p also reaches frame p99 24.3043 ms. Requested software cap 0/Immediate does not prove absence of external pacing or a refresh-rate guarantee.

Every ON process has 1,125 lookups and 6,786,000 validation samples. Six stationary scenarios hit AO and portal 1,125/1,125 after warmup and copy 54,288,000 numeric bytes/process; the three camera paths hit 0/1,125, miss 1,125 times and copy 81,900,000 bytes/process. OFF has 1,125 bypasses/no lookups. All ON/OFF processes retain the 74,848-byte estimated cache payload; timed long series record zero resource rebuilds. These fixture hit rates are not a gameplay hit-rate estimate.

Captured Prepare is **not allocation-free**. All 81 processes at 1080p record 640 bytes in Prepare, while Mask/Upload record zero. Representative raw traces place the 640 bytes on the first measured frame; profiler setup is a possible explanation, not proven attribution. At 1440p, 78 processes also record 640 bytes; ON cave repeat 0 records Prepare/Mask 4,416/3,776, ON cave repeat 2 Prepare/Upload 6,304/5,664, and BEFORE cave-panzoom repeat 2 Prepare/Mask 1,504/864 bytes. These sparse allocations remain unattributed; allocation p99 is zero in all three exported lighting scopes. The separate warmed hit/miss test establishes only its narrower exact-0-B cache contract.

GPU time remains **unknown**. `SetData` CPU duration cannot partition submission/copy work and waiting; logical bytes are not measured bus traffic. Inclusive parent/child scopes overlap and must not be summed.

Fixed-clock fixtures alone do not establish live advancing-time performance. The separate advancing-clock/reset/source-removal fixture proves path correctness/recovery, not steady-state timing or continuous absence of flashes during device reset. Real API reset is not a forced driver/context loss; unforced hardware failure and interactive in-session window resizing remain untested. Viewport/profile/resource-generation changes have bitwise unit coverage.

## Validation and review

- Integrated master baseline has green [CI 39115839608](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/39115839608), locked restore, zero-warning Release build and 1,738 ordinary tests. Four before Golden pairs and exact references pass.
- New cache regressions compare all five float planes bitwise across raw aliases/setters/load/cleared flags, negative boundaries, missing/replaced chunks, world/context/profile changes, mutable surface closures, source/flicker/frame input changes, temporal settling/gaps and capacity limits. Warm hits and sample misses each measure exactly 0 B in the existing isolated allocation runner.
- Native initial nine-scene probe: 18 ON captures and nine OFF comparisons pass exactly. Actual lifecycle ON/OFF captures pass with three rebuilds, source removal, advancing time and 267/270 portal hits in the ON run. The first direct lifecycle launch stopped before frame zero because the required renderer environment initializer was omitted; the corrected existing initialization succeeds. This setup failure is not a passed runtime test.
- Before calibration, local locked restore, zero-warning Debug/Release builds, 1,749 ordinary plus four strict cases/configuration, all 21 Release timing classes/39 tests and quick harness pass at unchanged budgets. Python 29 diagnostic, eight A/B-export, thirteen image-comparison and seven preview cases pass. Style verification, strict main/Wave05 audits (zero hard issues), unchanged previews and immutable Core/Data/Golden checks pass. Log: `artifacts/precalibration-gates.log`.
- Rebuilt policy/test freeze `4fd2d6c` passes locked restore, zero-warning Debug/Release builds, **1,748 ordinary plus five strict isolated cases per configuration**, style and immutable-input checks. Updated exporter requirements pass all eight cases. [Native receipt](Measurements/PR_0D/native_controls.json) binds unset flag => OFF using a Golden sidecar, original four optimized Golden pairs/exact references, three ordinary smokes, exact default/ON day parity and ON/OFF advancing-clock/source-expiry/reset parity. Both lifecycle captures show three rebuilds; ON has 267/270 prefix hits. Logs: `artifacts/final-policy-gates.log`, `artifacts/final-policy-native.log`.
- The first rebuilt mixed Debug suite failed the unchanged World-Select allocation Fact (1,152 B instead of 0; 1,748/1,749 passed). Planner and test body are unchanged from the base. A fresh isolated diagnostic passes; allocation cause remains unknown. Only its `StrictAllocation` trait was added, applying ADR-0034's existing separate-process gate. Warmup, 10,000 iterations and exact zero assertion remain unchanged, without retries/tolerance. Coverage stays 1,753 contracts (1,748+5 instead of 1,749+4). Failed evidence is preserved in `artifacts/final-policy-gates-first-attempt.log`; it is not reported as a pass.
- Local receipt-wrapper setup/status/metadata/path attempts also failed before acceptance, including one frame-zero Golden rejection for missing renderer identification. Those are not successful runtime tests. The corrected wrapper uses the existing renderer environment initializer and actual Golden benchmark metadata; only the passing identified captures above count as controls.
- Two independent agents reviewed dependencies, implementation, test partition and measured claims; dedicated ownership covered cache tests and Python tooling. Strict child-mode attestation prevents accepting OFF/OFF as an ON/OFF comparison. No open reviewed blocker remains; final hosted-head checks still govern handoff.
- Frozen implementation passes all five jobs in both [push run 39117722782](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/39117722782) and [PR run 39117787230](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/39117787230). Downloaded PR artifacts confirm 1,749+4 tests/cell, 30 diagnostic captures and 20 exact comparisons/cell, actual lifecycle rebuilding/source expiry/time advance and four exact Golden repeats plus portable references (maximum mean RGBA error 0.114835). Final policy/evidence-head run links/status are recorded in [PR #9](https://github.com/YoungJibbit95/2D-Game-Engine/pull/9); the earlier implementation runs do not substitute for those checks.

The final CI expands each OS/configuration diagnostic job from 18 to 30 captures: nine original **default OFF pairs plus one explicit ON each**, and the lifecycle OFF pair plus ON. It requires all 20 default captures to attest OFF and ten fallback captures to attest ON. The original four Golden pairs explicitly opt in and retain the original references. Original functional/performance/asset gates remain active. Render-step deadlines increase from eight to twelve minutes for the additional captures; timing/allocation/image thresholds are unchanged.

## Next decision

Accept this as a bounded, pixel-preserving **opt-in experiment**, not a default performance rollout. Actual hits and some favorable Mask observations do not outweigh missing general Prepare/frame benefit, no-hit work and unresolved cohort/tail variation.

After human review/manual integration, the next proposed bounded experiment is a conservative camera-motion eligibility bypass that avoids dependency snapshot/storage work on known cold frames, coupled with order-balanced/interleaved ON/OFF replications through the existing runner. Keep the same pixels, original path and nine scenarios; require repeatable target CPU benefit without unexplained p95/p99 regressions before enabling by default. First attribute remaining CPU-upload stalls separately; genuine GPU timing stays unknown until validated support exists. No next implementation begins automatically. RGB fields, new shaders, lighting-energy changes, cave-line fixes, worldgen and automatic reference replacement remain outside this PR.
