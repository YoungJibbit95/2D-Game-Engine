# PR-0C: evidence and decision boundary for Lighting V2

Owner: lead engineer. Reviewed base: `38bea1c00dbedb93c570bf0472c0362746224316` (integrated PR-0B).
Decision: investigate the existing pipeline first; an incremental optimization is the preferred next experiment, conditional on the measurements and acceptance criteria below. Chunk RGB transport and GPU composition remain `planned`, with unknown performance. PR-0C introduces diagnostics, not a production lighting prototype.

## Authoritative light and presentation are connected but different

1. [GameSimulation's Lighting phase](../../Game.Core/Runtime/GameSimulation.cs#L531) resolves scalar sunlight, applies cloud attenuation, dirties loaded chunks after a sunlight change of at least eight byte values, and runs bounded, player-visible-priority Core lighting.
2. [LightingSystem](../../Game.Core/Lighting/LightingSystem.cs#L159) prioritizes dirty chunks, recalculates each target with a 12-tile halo, propagates skylight and scalar tile emitters, and writes changed values back into loaded tiles. Missing chunks are tracked dependencies, not automatically open sky. This cost belongs to authoritative simulation.
3. [PlayingState's LightingPrepare scope](../../Game.Client/GameStates/PlayingState.cs#L1804) collects dynamic projectile lights, then tile emitters within the remaining budget, and prepares the client light masks. The client consumes the one authoritative world and entity snapshot; it does not own a second simulation.
4. [TileRayCastShadowMaskBuilder](../../Game.Client/Rendering/Lighting/TileRayCastShadowMaskBuilder.cs#L64) samples tile light/geometry, resolves local terrain surface and solar terms, applies directional and bounded point occlusion, ambient occlusion, RGB point light and bloom. [LightingRenderer](../../Game.Client/Rendering/LightingRenderer.cs#L218) then reprojects history, encodes colors, hashes whole masks and uploads changed textures.
5. [DrawPrepared](../../Game.Client/Rendering/LightingRenderer.cs#L567) alpha-composites the shadow texture, then additively composites colored light, bloom and optional reflection. These are prepared SpriteBatch overlays; the current path has no custom composition shader.

Scalar `TileInstance.Light` contains sky and tile-emitter contributions without RGB or source provenance. It is included in the [deterministic state hash](../../Game.Core/Diagnostics/SimulationStateHasher.cs#L63) and [chunk save payload](../../Game.Core/Saving/ChunkBinarySerializer.cs#L90). PR-0C changes neither its calculation nor its save/hash contract. A future visual experiment must preserve those contracts unless a separately reviewed Core migration explicitly changes them.

## What the timings establish

`Presentation.LightingPrepare` is an inclusive CPU scope: collection, mask construction, temporal processing, optional reflection radiance, encoding, hashing and uploads. Its child scopes must not be added to the parent when reporting total costs. Report call counts as well as durations: adaptive cadence can reduce calls without reducing cost per call.

`Presentation.LightingGpuUpload` currently measures CPU wall time around `Texture2D.SetData` and bookkeeping ([implementation](../../Game.Client/Rendering/LightingRenderer.cs#L382)). This can contain managed/native submission, memory copies, driver overhead and synchronization waits. The current probe cannot separate those components or measure when the GPU completes the transfer. Draw CPU time likewise measures submission, not GPU execution. The profiler's existing average is an exponential moving average; an arithmetic mean or percentile requires recorded samples.

The installed MonoGame DesktopGL **3.8.4.1** API was inspected locally in `MonoGame.Framework.xml`: `GraphicsMetrics` documents counters (`7652-7695`), while `OcclusionQuery` exposes visible pixel counts (`8262-8314`), not elapsed time. The repository has no supported, validated GPU timestamp-query integration; [renderer telemetry](../../Game.Client/Rendering/Diagnostics/RendererMetricsTelemetry.cs#L13) explicitly reports this. **GPU execution/transfer time remains unknown.** A native backend query would need capability checks, graphics-context ownership, asynchronous availability handling, reset-safe query lifetime and independent validation before reporting GPU milliseconds. Stopwatch, Present, readback and occlusion counts cannot substitute.

The unchanged four PR-0B references are a visual/control baseline, not a throughput calibration. Their 60 FPS cap dominates frame intervals. PR-0C's unpaced scenarios must keep deterministic simulation inputs separate from real wall-clock CPU measurements and record resolution, quality, actual sources/rays, uploaded bytes/maps, camera path, warmup, samples and host/driver/binary provenance. Unpaced frames per second alone do not establish a GPU improvement.

| Evidence set | Existing implementation | Incremental candidate | Chunk RGB / GPU candidate |
| --- | --- | --- | --- |
| Four fixed Golden scenes | Preserved PR-0B PNGs and timing sidecars; see [baseline dossier](PR_0B_GOLDEN_SCENES.md) | Unimplemented; performance unknown | Unimplemented; performance unknown |
| PR-0C uncapped CPU preparation/upload | 54 validated native captures at 1080p/1440p: day prepare mean 0.843–1.114 ms with zero post-warmup uploads; dynamic prepare mean 1.298–2.649 ms, upload CPU mean 0.383–1.309 ms; detailed per-scenario ranges in the [measurement dossier](PR_0C_RENDERING_LIGHTING_DIAGNOSTICS.md) | Unimplemented; no before/after claim | Unimplemented; no before/after claim |
| Camera/zoom, source-density and cave probes | Fixed 104x58 Medium mask; no measured resource rebuilds. Ten extrema pairs repeat exactly, with no obvious new seam at inspected endpoints. Source washout and unresolved cave lines persist; six-source any-channel clipping reaches 5.7195% of 1080p ROI | Unimplemented | Unimplemented |
| Genuine GPU execution/transfer timing | Unknown | Unknown | Unknown |

Candidate cells remain unknown until executable matched experiments exist. Current-path evidence establishes a bounded next experiment, not that a replacement is faster. CPU timing variance is material, including one unresolved 1080p day frame-p95 outlier at 22.670 ms; GPU execution remains unknown.

## Quality hypotheses to test without changing references

These are concrete source-derived explanations to test, not proven diagnoses of every visible artifact:

- **Dynamic source brightness and hue.** [Mask construction](../../Game.Client/Rendering/Lighting/TileRayCastShadowMaskBuilder.cs#L238) simultaneously reduces neutral darkness and adds point RGB; bloom is another additive layer. Emissive strength uses raw distance attenuation rather than the visibility/flicker-scaled intensity. Fully blocked rays skip the source, but partial visibility reduces RGB without equivalent bloom attenuation. Warm bloom can also alter a blue source's hue. Compare source count/overlap, partial occlusion, RGB saturation and bloom coverage; record the existing values before proposing energy or artistic changes.
- **Sky/emitter ambiguity.** [Sky visibility](../../Game.Client/Rendering/Lighting/TileRayCastShadowMaskBuilder.cs#L139) is partly inferred from combined scalar tile light. A static emitter can therefore affect the quantity interpreted as sky access. A new representation must explicitly distinguish sky visibility/daylight from emission, rather than relabeling the existing byte as RGB radiance.
- **Partial shapes and missing terrain.** Core skylight uses `tile.IsSolid`; the client uses [collision-shape samples](../../Game.Client/Rendering/Lighting/TileRayCastShadowMaskBuilder.cs#L330). Client missing samples default to zero/non-solid, whereas Core retains unknown-sky dependencies. Diagnose slopes/half-blocks, shafts and chunk materialization/unload separately from natural darkness. Do not infer correctness from one fixed cave screenshot.
- **Camera/zoom stability.** [Camera2D](../../Game.Client/Rendering/Camera2D.cs#L194) floors/ceils the mask's visible rectangle while the world transform retains fractional position. Screen-resolution mask sampling and endpoint quantization can change during subpixel motion or zoom. Use deterministic pan/reversal/zoom traces and matched checkpoints, with static geometry and controlled moving lights.
- **History limits.** [Temporal stabilization](../../Game.Client/Rendering/Lighting/LightingTemporalStabilizer.cs#L17) uses shadow/RGB value-delta thresholds and world-coordinate reprojection; it has no explicit geometry/material/residency identity. ADR-0027's geometry/depth rejection wording should be read with this implementation limit. Measure rejected/history samples and settling behavior after changes; gradual numerical changes can still retain history.

The four original PNGs and JSON fixtures stay immutable. New diagnostic captures are separate artifacts. No automatic reference replacement, masking, comparison-tolerance relaxation or visual correction belongs in this baseline PR.

## Alternatives and migration contracts

| Choice | Potential benefit, not measured gain | Costs and risks |
| --- | --- | --- |
| Incrementally cache current preparation | Reuse camera-independent samples, actual unchanged outputs or source collection; retain the current visual model and rollback path | Dependency completeness is difficult; temporal convergence, flicker, sunlight/weather and geometry changes must still advance correctly. Full content hashes currently skip uploads only after full build/encode/hash. |
| Chunk-based RGB lightfield | World-anchored static transport can amortize camera motion; dirty chunks can support bounded partial uploads and explicit sky/emission channels | New propagation/border contracts, dynamic-light treatment, unknown residency and solve latency; coarse tile fields may lose existing point shadows, partial-shape detail or solar quality. Memory and upload locality depend on residency and atlas policy. |
| Simplified GPU composition | A shader can sample prepared light channels and combine them with scene color in a defined pass | It moves work rather than proving a gain; GPU cost is unknown. Define color space, blending, emission/bloom energy, filtering and pass order. Existing custom shader compilation/package validation must be established for Windows/Ubuntu before shipping. |

Keep Core authoritative and renderer-neutral. Any experimental RGB field is a transient presentation product of authoritative material/source snapshots, not a second live world or gameplay simulation. Keep scalar lighting, world generation, saves and original draw path active and unchanged behind an explicit opt-in comparison mode. Count any additional CPU solve; do not conceal duplicate transport as a cache benefit.

### Dirty regions and stable identity

- [Chunk.SetTileLight](../../Game.Core/World/Chunk.cs#L40) does not advance an output revision. `NeedsLightUpdate` is scheduling state that can be cleared; it is not a committed-output version. A safe cache needs actual output versions and source/geometry dependencies. Halo writes can change neighbors, so version every changed output chunk rather than only the processed target.
- Keys must include world/session generation, signed chunk coordinate and residency identity, content/geometry/light versions, solve configuration, solar/weather settings and relevant source signatures. Reject stale asynchronous results after unload, replacement or session change. Bound work, memory and queue depth; prioritize visible work without losing deferred invalidations.
- Use [CoordinateUtils](../../Game.Core/World/CoordinateUtils.cs#L33) floor division and positive local coordinates for negative X. Test both directions across zero and negative chunk edges. Integer truncation gives incorrect chunk ownership.
- Separate propagation halos from texture filtering gutters. A one-cell gutter cannot satisfy an arbitrarily long light or skylight dependency. Define deterministic border exchange/convergence, unknown-neighbor semantics and dirty propagation for source removal, shafts and column residency. Preserve conservative missing-sky behavior.
- Static emitters can invalidate their bounded influence regions. Moving sources dirty both old and new footprints or use a separate dynamic layer. Quantized flicker and global solar/weather changes require explicit policies, not accidental stale reuse. Camera/zoom alone should not invalidate a world-anchored static solve, but still changes sampling/composition.

### Resource lifetime and computed payload sizes

The existing renderer owns front/upload/spare textures for each mask ([allocation](../../Game.Client/Rendering/LightingRenderer.cs#L152), [rotation](../../Game.Client/Rendering/LightingRenderer.cs#L712)). It allocates nine textures for shadow/RGB/bloom and twelve with reflections. Nominal RGBA8 texture payload is **36 or 48 bytes per mask pixel**; current CPU color/float arrays use approximately **52 or 56 bytes per mask pixel**, excluding reflection surfaces and object/driver overhead. These are source-derived payload estimates, not measured resident GPU memory.

At the current 16,384-pixel High cap, nine mask textures contain 589,824 bytes of nominal color payload. For a proposed 32x32-tile RGBA8 chunk field, one texture contains 4,096 bytes; a one-tile filtering gutter makes it 34x34x4 = 4,624 bytes. Multiply by resident chunks, channel planes and buffering before comparing. A halo solve's CPU input/workspace is additional. No measured bandwidth or operation-throughput claim follows from these byte counts.

Current resources are recreated for device identity/viewport/profile changes and disposed with PlayingState ([resource matching](../../Game.Client/Rendering/LightingRenderer.cs#L633), [release](../../Game.Client/Rendering/LightingRenderer.cs#L649), [state disposal](../../Game.Client/GameStates/PlayingState.cs#L1114)). No explicit device-reset handler was found in these lighting classes. A new texture/atlas/query cache must invalidate GPU contents, upload hashes, history and pending work together on reset; test that lifecycle rather than assuming a prior content hash means the new resource is populated. Graphics resource creation/upload/disposal stays on the graphics thread, outside Draw-time world solving. Release/reset/unload must be bounded and exact-once.

## Conditional next PR and exit criteria

After this diagnostic baseline is reviewed, select one medium-sized optimization from the measured dominant CPU stage. Prefer output/dependency versioning and reuse within the existing path if it can preserve pixels. If measured dynamic mask work dominates and invalidates the proposed reuse, reject that optimization and specify a bounded alternative experiment before implementation. Do not combine renderer, lighting and worldgen replacement.

Acceptance for that next PR:

1. Preserve the original four fixtures and PNG bytes; controlled-environment before/after captures remain pixel exact. Existing portable and renderer-neutral contracts retain their thresholds. Any intentional visual change requires explicit human review and separately approved new references.
2. Compare matched uncapped Release runs, with warmup, multiple independent processes, equal scene/quality/source inputs and raw samples. Report complete inclusive preparation, CPU upload, simulation, Draw and frame costs without double counting; include call counts, allocation, bytes and variance. Claim improvement only if the target cost decreases reproducibly and p95 CPU/frame behavior does not worsen outside measured run variance. Genuine GPU cost remains unknown until validated queries exist.
3. Prove invalidation after mining/building, emitter movement/removal, sunlight/weather/settings changes, cross-boundary halo writes, negative-X residency changes and session replacement. Unrelated outputs retain versions; deferred work is eventually processed. Pan/reversal/zoom introduces no new seams or persistent trails.
4. Demonstrate bounded residency/queues and correct resize, disable/re-enable, device reset and unload disposal. No new steady-state hot-path allocation, synchronous Draw-time world work or resource creation is introduced.
5. Keep an optional experiment isolated and benchmarkable with an immediate switch back to the original path. Preserve Core/save/hash behavior and all existing Debug/Release, Windows/Ubuntu, asset and performance gates. Unimplemented alternatives remain documented as unknown, not accepted substitutes for these checks.
