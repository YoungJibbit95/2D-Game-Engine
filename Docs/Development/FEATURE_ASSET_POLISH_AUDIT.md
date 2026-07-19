# Feature And Asset Polish Audit

Last reviewed: 2026-07-19

This audit is a visual and runtime-oriented companion to the capability matrix. Current code and fresh validation remain authoritative. Capability states use the repository vocabulary: `verified`, `implemented-unverified`, `partial`, `planned`, `blocked`, and `deprecated`.

## Current Visual Baseline

| Area | State | Evidence | Remaining quality gap |
| --- | --- | --- | --- |
| Biome panoramas | `verified` | Forest, Amber Grove, Twilight Marsh and Crystal Depths now bind twelve independent V6 Far/Mid/Near planes (`1536x384`, `1024x256`, `512x128`) derived from the project-owned panoramas. Every plane has a brief, exact manifest dimensions, runtime reference, contact sheet, generator and provenance. | Bound residency to the active/adjacent biome set; the current all-content smoke estimates 34,744,320 B decoded across 235 resources. |
| Parallax composition | `verified` | Every V6 plane preserves its exact 4:1 aspect with one uniform X/Y authored-distance scale, point sampling and seam-safe unmirrored repeats. The scale is selected from viewport height and layer depth, never from camera zoom, jumping or terrain coverage; top/bottom fill is separate and does not stretch the image. Focused Parallax passes 80/80 in Debug and Release at 0 B. | Complete traversal/jump/mining comparisons for every biome and quality tier; retain the legacy procedural path only for content without authored planes. |
| Foreground trees | `verified` | Finite and infinite generation share one deterministic allocation-free planner with twelve root/branch/crown silhouettes, negative openings and offset clusters. Forest oak, Amber living wood/autumn foliage and Marsh mangrove/loose foliage resolve through biome data. Connected autotile edges no longer expose a square grid, and bounded wind-driven falling leaves reuse the fixed particle pool. Forest/Amber/Marsh 1080p captures and 3/3 strict foliage audits pass. | Add restrained trunk/decor overlays and a longer seasonal/weather fall-leaf comparison without increasing atlas batches. |
| Player character | `verified` | The active Wave 06 player uses five synchronized native `24x40` layers, 16 source-rectangle poses, authored state/action profiles and no legacy frame upscaling. Body, clothes, hair, armor and equipment share one fixed-tick clock; 5/5 strict assets and runtime smokes pass. | Rename the compatibility renderer owner and capture every action/armor combination in an encounter matrix. |
| Entity presentation | `partial` | Bounded preparation covers state animation, elite outline, hurt tint, shadow, bob, squash/stretch, wing motion and projectile rotation. | No encounter matrix proves every active actor, attack, hit, death and elite state; several silhouettes remain visually weak in the strict audit. |
| Menus and overlays | `partial` | Shared pointer/gamepad semantics, rounded surfaces, gradients, glow, blur budgets and compact-resolution behavior exist. World Library/Create World now add responsive metadata cards, cached labels, caret/focus behavior and seed validation; 22/22 focused Debug/Release tests and a 640x360 smoke pass. | Bitmap text and inventory/crafting/shop/dialogue surfaces still use inconsistent density, hierarchy and spacing. There is no screenshot regression matrix or full click-path smoke. |
| Combat feedback | `partial` | Guard block/parry/break and projectile launch now enter bounded visual/audio queues exactly once. Bounce/pierce/expire/destroy adapters are typed, tested and steady-state 0 B without client-side gameplay authority. | Attach all terminal projectile paths, capture a visible combat matrix and add production audio. |
| Water and reflection presentation | `partial` | `LiquidPresentationPlanner` prepares bounded run-coalesced body, depth, surface and left/right shore commands in LateUpdate. Forest/Amber/Marsh/Crystal palettes share the existing reflection contract and Draw only consumes prepared commands. Focused tests and 1080p runtime budgets pass at 0 B. | Deterministic spawn captures did not contain a visible water body, so shoreline/depth appearance still needs a dedicated water-heavy 1080p/1440p proof before this can be called visually verified. |
| Building and environment interaction | `partial` | Mining and placement are transactional and data driven. | Furniture anchors, background walls, liquids, richer placement previews and rollback IDs remain visibly incomplete. |
| Audio | `partial` | Mixer, soundscape selection, crossfade, voice limits and missing-asset telemetry are active. | No production sound files are shipped, so the polished visual scenes remain silent. |

## Asset Audit Findings

The 2026-07-19 strict main-manifest audit covers 190 definitions and reports:

- 0 missing files;
- 0 dimension mismatches;
- 0 hard-issue assets and 0 hard issues;
- 156 pass and 34 review assets;
- 104 palette-review findings, 32 weak-dark-silhouette findings and 2 low-canvas-occupancy findings.

Supplemental strict audits cover V6 background planes 12/12, Wave 06 character layers 5/5, terrain materials 6/6 and loose foliage 3/3 with 0 hard issues. Biome backgrounds and generated material packs intentionally use focused palettes beyond the small legacy global sprite palette; they are judged with their briefs, previews, runtime readability and performance evidence rather than being quantized back to that coarse palette.

Final V6 Release smokes load 235 resources and 1,308 frames with 0 invalid resources and a 34,744,320 B decoded-source estimate. Forest/Amber/Marsh 1920x1080 and Amber 3440x1440 pass after the final foliage regeneration. `Render.Background` averages 0.089-0.113 ms at 1080p and 0.134 ms at 3440x1440, all at 0 B. Liquid preparation averages 0.253-0.384 ms at 1080p and remains 0 B.

## Ordered Polish Plan

Completed in this slice: V6 background depth stacks and native scaling, Wave 06 player activation, Terrain Polish V1, biome Tree V2 materials/foliage/falling leaves, buffered/coyote/variable-height character movement, centralized enemy/item body physics and prepared biome liquid presentation.

1. **Water-heavy visual proof:** capture authored fixtures with visible shorelines at 1080p/1440p, then tune only from those comparisons.
2. **Physics shapes and continuous pairs:** add one-way platforms, slopes and fast body-body TOI before expanding into joints or liquid forces.
3. **Background residency and traversal:** capture all four V6 biomes during jumping/mining at 720p/1440p, then bound residency to active/adjacent biome planes.
4. **UI scale and hierarchy:** finish migration of inventory, crafting, character, shop and dialogue surfaces onto shared layout primitives; add deterministic click-path and accessibility-scale smokes.
5. **Combat and entity proof:** attach all projectile terminal adapters and capture melee/ranged/magic, guard/parry/break, hit/death and biome encounter states.
6. **Environment mechanics:** finish furniture, walls and liquid placement with preview, validation and recovery-safe persistence.
7. **Production audio and soft debt:** add licensed/project-owned biome loops and compact gameplay cues, then resolve the 34 review assets in runtime-priority batches.

## Exit Gates For Every Polish Slice

- Runtime reference and ownership are explicit; no showcase-only asset is called finished.
- New raster assets have brief, exact dimensions, manifest metadata, preview, audit and provenance.
- No disk I/O, decode, generation or unbounded allocation enters Draw or another known hot path.
- Debug and Release focused tests pass, followed by the full suite when the shared tree is stable.
- Visible work exits with a real client capture at representative resolution; performance-sensitive work includes before/after p95/p99 and memory evidence.
