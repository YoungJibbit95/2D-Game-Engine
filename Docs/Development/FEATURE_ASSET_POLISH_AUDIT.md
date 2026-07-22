# Feature And Asset Polish Audit

Last reviewed: 2026-07-22

This audit is a visual and runtime-oriented companion to the capability matrix. Current code and fresh validation remain authoritative. Capability states use the repository vocabulary: `verified`, `implemented-unverified`, `partial`, `planned`, `blocked`, and `deprecated`.

## Current Visual Baseline

| Area | State | Evidence | Remaining quality gap |
| --- | --- | --- | --- |
| Biome panoramas | `verified` | Forest, Amber Grove, Twilight Marsh and Crystal Depths bind twelve V6 Far/Mid/Near planes plus eight V7 mountain/floating-island feature planes. All V7 sources are native `1024x256`, binary-alpha and carry manifest, brief, deterministic generator, provenance, contact sheet and 8/8 strict audit. Current Forest V7 gameplay passes at 1920x1080 with 259 resources, 1,514 frames and 0 invalid resources; prior/current 1440p evidence remains green. | Complete cross-biome clear/rain/snow/mining comparisons and active-biome residency are still missing. |
| Parallax composition | `verified` | Active feature and V6 planes preserve authored aspect through uniform fullscreen depth projection, point sampling and unmirrored repeats. Transparent vertical coverage removes lower-row replication; camera zoom, jumping, terrain depth and negative X do not change scale. Current Forest visual QA shows continuous sky-to-terrain composition and Background Draw averages 0.092 ms at 0 B. | Capture the same five-plane traversal/weather matrix for Amber, Marsh and Crystal at 1080p/1440p. |
| Foreground trees | `verified` | Tree V3/V5 replaces the rejected rectangular, over-sparse and flat intermediate iterations with twelve allocation-free silhouettes: 7-12-tile trunks, acyclic stair branches, one root spur and a dense upper crown overlapping two nearer branch lobes. Finite and infinite placement keep 13-tile center spacing. Nine foliage sheets now use stable local A/B/C macro volumes, irregular exposed contours and presentation-only wood/leaf sockets without an internal tile grid; three curved trunk sheets retain native `16x16` frames. Forest oak, Amber living wood and Marsh mangrove palettes remain distinct, while bounded wind-driven falling leaves reuse the fixed particle pool. Final V7 1080p Release captures, 9/9 foliage and 3/3 trunk audits pass. | Add restrained bark/decor overlays and a longer seasonal/weather fall-leaf comparison without increasing atlas batches. |
| Player character | `verified` | The active Wave 06 player uses five synchronized native `24x40` layers, 16 source-rectangle poses, authored state/action profiles and no legacy frame upscaling. Body, clothes, hair, armor and equipment share one fixed-tick clock; 5/5 strict assets and runtime smokes pass. | Rename the compatibility renderer owner and capture every action/armor combination in an encounter matrix. |
| Entity presentation | `partial` | Bounded preparation covers state animation, elite outline, hurt tint, shadow, bob, squash/stretch, wing motion and projectile rotation. Legacy Boar Polish V1 retains its four-frame runtime ABI; the latest Forest smoke visibly resolves a native forest boar and rabbit with no invalid resources. | Elite/attack/hit/death states and the broader biome actor set still need encounter captures; several legacy silhouettes remain weak. |
| Menus and overlays | `partial` | Shared pointer/gamepad semantics, rounded surfaces, gradients, glow, blur budgets and compact-resolution behavior exist. The main menu now stages its controls over a project-owned `1672x941` pixel-adventure panorama with edge tree settlements, floating islands, waterfalls, ruins and bounded lantern/firefly/star animation; aspect-fill planning covers 16:9, 4:3 and ultrawide without steady-state allocation, while a procedural scene remains the missing-asset fallback. A real 1920x1080 client capture and 23/23 focused UI/asset tests pass. Gameplay Escape dismisses the topmost character, crafting or inventory overlay before pause handling. The mobility dock remains backed by one 48x16 three-frame sheet with responsive tests from 320x180 through 4K. | Inventory/crafting/shop/dialogue surfaces still retain inconsistent density, hierarchy and spacing, with no screenshot-regression matrix or full click-path smoke. The main-menu panorama is state-owned and loaded only during `LoadContent`; it is intentionally outside bulk sprite preloading. |
| Combat feedback | `partial` | Guard block/parry/break and projectile launch now enter bounded visual/audio queues exactly once. Bounce/pierce/expire/destroy adapters are typed, tested and steady-state 0 B without client-side gameplay authority. | Attach all terminal projectile paths, capture a visible combat matrix and add production audio. |
| Water and reflection presentation | `partial` | `LiquidPresentationPlanner` prepares bounded run-coalesced body, depth, surface and left/right shore commands in LateUpdate. Forest/Amber/Marsh/Crystal palettes share the existing reflection contract and Draw only consumes prepared commands. Focused tests and 1080p runtime budgets pass at 0 B. | Deterministic spawn captures did not contain a visible water body, so shoreline/depth appearance still needs a dedicated water-heavy 1080p/1440p proof before this can be called visually verified. |
| Building and environment interaction | `partial` | Mining/placement are transactional and data driven. Valid mining uses a 0.25 duration factor, targets exposed background walls as well as foreground tiles and emits physical debris feedback. | Furniture anchors, liquid placement, richer previews and durable reconciliation IDs remain visibly incomplete. |
| Audio | `partial` | Mixer, soundscape selection, crossfade, voice limits and missing-asset telemetry are active. | No production sound files are shipped, so the polished visual scenes remain silent. |

## Asset Audit Findings

The 2026-07-19 strict main-manifest audit covers 190 definitions and reports:

- 0 missing files;
- 0 dimension mismatches;
- 0 hard-issue assets and 0 hard issues;
- 156 pass and 34 review assets;
- 104 palette-review findings, 32 weak-dark-silhouette findings and 2 low-canvas-occupancy findings.

Supplemental strict audits cover V6 background planes 12/12, V7 background features 8/8, Wave 06 character layers 5/5, terrain materials 6/6, connected foliage variants 9/9, curved trunk sheets 3/3 and Mobility Accessories 4/4 with 0 hard issues. The main-scope Legacy Boar family additionally passes its 2/2 native-frame audit and both IDs remain Quality Tier `pass`. Biome backgrounds and generated material packs intentionally use focused palettes beyond the small legacy global sprite palette; they are judged with their briefs, previews, runtime readability and performance evidence rather than being quantized back to that coarse palette.

Final V7 Release smokes load 259 resources and 1,514 frames with 0 invalid resources. The latest Forest 1920x1080 scene visibly combines five background planes, Tree V3/V5, native boar/rabbit actors and the current HUD; `Render.Background` averages 0.092 ms, `Render.Tilemap` 0.206 ms and particles 0.026 ms at 0 B. Retained cross-biome/1440p captures remain valid for unchanged depth/tree content.

## Ordered Polish Plan

Completed through this slice: V6 depth stacks plus V7 mountain/island feature planes at stable authored distance, Wave 06 player activation, Terrain Polish V1, biome Tree V3 silhouettes/trunks/foliage/falling leaves, buffered/coyote/variable-height movement, centralized enemy/item/projectile physics with isolated contact slices, upward-facing partial tile collision shapes, prepared biome liquid presentation, Mobility Accessories V1 and Legacy Boar Polish V1.

1. **Dense authoritative physics integration:** combine scheduled AI, fast projectile/body pairs and real EntityManager contact slices in one bounded 0 B scene fixture; then author and render half blocks/both upward slopes without stretching tile art.
2. **V7/boar/mobility runtime proof:** extend the accepted Forest scene to equipped Double Jump/Flight/Glide plus normal/elite boar attack/hit/death captures at 1080p/1440p; the base V7/normal-boar integration and residency count are now proven.
3. **Water-heavy visual proof:** capture authored fixtures with visible shorelines at 1080p/1440p, then tune only from those comparisons.
4. **Background residency and traversal:** capture all four V6 biomes during jumping/mining at 720p/1440p, then bound residency to active/adjacent biome planes.
5. **UI scale and hierarchy:** finish migration of inventory, crafting, character, shop and dialogue surfaces onto shared layout primitives; add deterministic click-path and accessibility-scale smokes.
6. **Combat and entity proof:** attach all projectile terminal adapters and capture melee/ranged/magic, guard/parry/break, hit/death and biome encounter states.
7. **Environment mechanics/audio/soft debt:** finish furniture, walls and liquid placement, add project-owned audio, then resolve the remaining review assets in runtime-priority batches.

## Exit Gates For Every Polish Slice

- Runtime reference and ownership are explicit; no showcase-only asset is called finished.
- New raster assets have brief, exact dimensions, manifest metadata, preview, audit and provenance.
- No disk I/O, decode, generation or unbounded allocation enters Draw or another known hot path.
- Debug and Release focused tests pass, followed by the full suite when the shared tree is stable.
- Visible work exits with a real client capture at representative resolution; performance-sensitive work includes before/after p95/p99 and memory evidence.
