# PR-0D: Dependency-safe lighting preparation reuse

Base: human-integrated PR-0C, `de83468aa7e5d19f43634ae1df6935299f9d83d2`.
Branch: `codex/yjse-p0-004-lighting-reuse`; isolated worktree `F:\Coding\YJsE-PR-0D`.
Date: 2026-10-18. Feature commits/push/PR are authorized; human review/manual merge only.

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

## Fallback and reproduction

Set `YJSE_DISABLE_LIGHTING_PREPARATION_REUSE=1` before client startup for the original CPU path. Embedding code can set `LightingRenderer.PreparationReuseEnabled=false` immediately; either policy transition invalidates reuse. The preallocated payload remains until resource release. No setting/save migration is needed. Revert the PR to remove the feature entirely.

```powershell
dotnet restore YjsE.sln --locked-mode
dotnet build YjsE.sln -c Release --no-restore
dotnet publish Game.Client/Game.Client.csproj -c Release --no-build --no-restore -o ../YJsE-PR-0D-publish
./tools/run_golden_scenes.ps1 -PublishRoot ../YJsE-PR-0D-publish -OutputDirectory artifacts/control-goldens -BaselineMode exact
./tools/run_lighting_diagnostics.ps1 -PublishRoot ../YJsE-PR-0D-publish -Width 1920 -Height 1080 -Frames 1800 -WarmupFrames 300 -Repeats 3 -PreparationReuse off -OutputDirectory artifacts/off-1080p
./tools/run_lighting_diagnostics.ps1 -PublishRoot ../YJsE-PR-0D-publish -Width 1920 -Height 1080 -Frames 1800 -WarmupFrames 300 -Repeats 3 -PreparationReuse on -OutputDirectory artifacts/on-1080p
python tools/lighting_reuse_compare.py --before-root <clean-base-GUID-root> --off-root <OFF-GUID-root> --on-root <ON-GUID-root> --output-dir artifacts/comparison-1080p
```

BEFORE uses a separately published clean-base binary; the current diagnostic runner's default `inherit` mode remains compatible with legacy captures. OFF/ON require the same actual loaded DLL versions/hashes and attested modes, identical settings/content/state/fixtures, renderer/driver/CPU/configuration, windows and source fixture hashes. The exporter validates every capture/report/PNG binding, requires all nine scenes with three fresh processes each, and writes 18 exact BEFORE/ON and OFF/ON image comparisons before accepting its dossier. Legacy cache counters are unavailable, not invented zeros. Original references are never overwritten.

The nine PR-0C fixtures retain their fixed seed, simulation ticks, clock/weather, camera paths, source counts and unpaced policy. Static source flicker continues normally. Separate lifecycle validation uses the same runner with `-PreparationReuse on -CompareReuseFallback -IncludeReuseLifecycle`: advancing time, natural projectile expiry halfway through the fixture, actual `GraphicsDevice.Reset` every 113 ticks and fresh-process OFF comparison. Default 480/120 inputs produce three measured resource rebuilds, source count 2 to 0 and continued checked-prefix hits.

## Measurement evidence

Calibration is pending. No performance gain is claimed by this initial implementation record. Final per-process data and ranges will distinguish Prepare invocations, Mask CPU, CPU Upload, frame intervals, p95/p99, allocation, lookups/bypasses and actual AO/portal hits. GPU time remains unknown. `SetData` CPU duration cannot partition driver work and waiting; logical bytes are not measured bus traffic. Inclusive parent/child scopes overlap.

Three fresh processes are observations, not confidence intervals or pooled percentiles. Fixed-clock fixtures alone do not establish live advancing-time performance. The separate advancing-clock/reset/source-removal fixture proves path correctness/recovery, not steady-state timing or continuous absence of flashes during device reset. Real API reset is not a forced driver/context loss; unforced hardware failure remains untested.

## Validation and review

- Integrated master baseline has green [CI 39115839608](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/39115839608), locked restore, zero-warning Release build and 1,738 ordinary tests. Four before Golden pairs and exact references pass.
- New cache regressions compare all five float planes bitwise across raw aliases/setters/load/cleared flags, negative boundaries, missing/replaced chunks, world/context/profile changes, mutable surface closures, source/flicker/frame input changes, temporal settling/gaps and capacity limits. Warm hits and sample misses each measure exactly 0 B in the existing isolated allocation runner.
- Native initial nine-scene probe: 18 ON captures and nine OFF comparisons pass exactly. Actual lifecycle ON/OFF captures pass with three rebuilds, source removal, advancing time and 267/270 portal hits in the ON run. The first direct lifecycle launch stopped before frame zero because the required renderer environment initializer was omitted; the corrected existing initialization succeeds. This setup failure is not a passed runtime test.
- Local locked restore, zero-warning Debug/Release builds and **1,749 ordinary tests per configuration** pass. All four strict allocation cases pass separately in Debug/Release, all 21 Release timing classes/39 tests and quick harness pass at unchanged budgets. Python 29 diagnostic, eight A/B-export, thirteen image-comparison and seven preview cases pass. Style verification, strict main/Wave05 audits (zero hard issues), unchanged previews and immutable Core/Data/Golden checks pass. Log: `artifacts/precalibration-gates.log`. Final hosted-head checks govern review readiness.
- Two independent agents reviewed dependencies and implementation; dedicated file ownership covered cache tests and Python tooling. Review added strict child-mode attestation so an accidental OFF/OFF capture cannot be accepted as an ON/OFF comparison. Remaining CI/native calibration is required; pending tests are not passed.

The CI expands each OS/configuration diagnostic job from 18 to 30 captures: nine original ON pairs plus one OFF each, and the lifecycle ON pair plus OFF. Original Golden/functional/performance/asset gates remain active. Render-step deadlines increase from eight to twelve minutes for that explicit additional work; timing/allocation/image thresholds are unchanged.

## Next decision

After repeated native measurements and final Windows/Ubuntu Debug/Release gates, accept this bounded prefix reuse only for the measured behavior. Report no-hit camera/zoom overhead and timing uncertainty alongside wins. If CPU improvement is negligible or invalidation needs broader contracts, retain the original path and choose another bounded experiment. RGB fields, new shaders, lighting energy changes, cave-line fixes, worldgen and automatic Golden replacement are outside this PR. Human review/manual integration remains mandatory.
