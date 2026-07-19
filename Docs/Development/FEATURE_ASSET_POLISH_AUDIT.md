# Feature And Asset Polish Audit

Last reviewed: 2026-07-19

This audit is a visual and runtime-oriented companion to the capability matrix. Current code and fresh validation remain authoritative. Capability states use the repository vocabulary: `verified`, `implemented-unverified`, `partial`, `planned`, `blocked`, and `deprecated`.

## Current Visual Baseline

| Area | State | Evidence | Remaining quality gap |
| --- | --- | --- | --- |
| Biome panoramas | `verified` | Forest, Amber Grove, Twilight Marsh and Crystal Depths select additive V5 `1536x384` native-resolution panoramas. Each has a generation brief, exact manifest dimensions, runtime biome reference, wrap preview and provenance. Four 1080p smokes plus Forest 1440p pass with 0 invalid resources. | Bound residency to the active/adjacent biome set; the all-content preload is now a 19,609,600 B decoded estimate. |
| Parallax composition | `verified` | Authored `_vN` panorama layers use deterministic biome/depth/night composition, one seam-safe repeat and full opacity at surface presence. Geometric haze/cloud backdrops no longer show through as rectangular bands. Debug/Release parallax gates pass 29/29 and the final captures are clean. | Complete the 1440p quality/biome matrix and retain the legacy procedural path only for content without authored panoramas. |
| Foreground trees | `partial` | Finite and infinite generation share one deterministic, allocation-free silhouette planner with layered crowns, edge notches, branches and root flare; focused Debug/Release generation contracts pass 16/16. | Biome `treeType` does not yet select distinct foreground trunk/canopy materials or authored foliage decorations. Amber/Marsh therefore clash with their V5 backgrounds. |
| Player character | `partial` | The fixed-tick rig has five independently composited layers, 16 registered frames and authored action states. The checked-in generated source board contains substantially more detail than the active output. | Final runtime frames are only `16x32`; the deterministic remitter loses facial, hair, cloth and armor detail, and the character reads too small at 1080p/1440p. |
| Entity presentation | `partial` | Bounded preparation covers state animation, elite outline, hurt tint, shadow, bob, squash/stretch, wing motion and projectile rotation. | No encounter matrix proves every active actor, attack, hit, death and elite state; several silhouettes remain visually weak in the strict audit. |
| Menus and overlays | `partial` | Shared pointer/gamepad semantics, rounded surfaces, gradients, glow, blur budgets and compact-resolution behavior exist. World Library/Create World now add responsive metadata cards, cached labels, caret/focus behavior and seed validation; 22/22 focused Debug/Release tests and a 640x360 smoke pass. | Bitmap text and inventory/crafting/shop/dialogue surfaces still use inconsistent density, hierarchy and spacing. There is no screenshot regression matrix or full click-path smoke. |
| Combat feedback | `partial` | Guard block/parry/break and projectile launch now enter bounded visual/audio queues exactly once. Bounce/pierce/expire/destroy adapters are typed, tested and steady-state 0 B without client-side gameplay authority. | Attach all terminal projectile paths, capture a visible combat matrix and add production audio. |
| Water and reflection presentation | `partial` | Liquid surfaces and bounded screen-space reflection/radiance composition are active and tested. | Current Amber/Marsh captures expose large flat-color liquid fields with hard rectangular edges; biome tint, surface animation, shoreline foam and depth variation need a dedicated visual pass without adding work to Draw. |
| Building and environment interaction | `partial` | Mining and placement are transactional and data driven. | Furniture anchors, background walls, liquids, richer placement previews and rollback IDs remain visibly incomplete. |
| Audio | `partial` | Mixer, soundscape selection, crossfade, voice limits and missing-asset telemetry are active. | No production sound files are shipped, so the polished visual scenes remain silent. |

## Asset Audit Findings

The 2026-07-19 strict main-manifest audit covers 190 definitions and reports:

- 0 missing files;
- 0 dimension mismatches;
- 0 hard-issue assets and 0 hard issues;
- 156 pass and 34 review assets;
- 104 palette-review findings, 32 weak-dark-silhouette findings and 2 low-canvas-occupancy findings.

The V5 panoramas intentionally use biome-specific 256-color palettes rather than the small global sprite palette. They must therefore be judged with their generation brief, seam preview, readability and runtime performance evidence, not by forcing them back to a coarse global palette.

Final Release traversal evidence loads 209 resources and 1,072 frames with 0 invalid resources. Estimated decoded texture payload is 19,609,600 B; load time is 281-302 ms with about 21.55 MB load allocation. Forest frame pacing averages 6.061 ms at 1080p and 6.076 ms at 1440p; `Render.Background` averages 0.042/0.044 ms and 0 B respectively. The biome 1080p background pass remains 0 B at 0.065-0.086 ms average.

## Ordered Polish Plan

1. **Character Wave 06:** create additive five-layer `24x40` frames for the existing 16-pose order from the generated character source, preserve one shared registration/baseline, keep collision independent from art size, and validate every state at native, 1080p and 1440p scale. Do not upscale the existing `16x32` sheets.
2. **Background residency and traversal:** measure V5 decode/load cost, keep Draw resident-only, and move from preload-every-historical-background toward bounded active/adjacent-biome residency if measurements justify it. Capture Forest, Amber Grove, Twilight Marsh and Crystal Depths at 1080p and 1440p.
3. **Biome foreground identity:** make `treeType` and surface feature sets select data-driven silhouettes/materials/decorations so Amber Grove and Twilight Marsh no longer reuse generic Forest foreground trees.
4. **Water surface identity:** replace flat rectangular liquid fields with biome-tinted depth bands, animated surface highlights, shoreline transitions and restrained reflection distortion prepared outside Draw; validate water-heavy Amber/Marsh scenes at 1080p/1440p.
5. **UI scale and hierarchy:** finish migration of inventory, crafting, character, shop and dialogue surfaces onto shared layout primitives; add deterministic Create World, rename/delete, 1080p and 1440p screenshot/click-path smokes with accessibility scale coverage.
6. **Combat and entity proof:** attach projectile terminal adapters and capture authored melee/ranged/magic, guard/parry/break, projectile spawn/hit/expire and representative biome encounters, while preserving bounded queues and zero duplicate gameplay authority.
7. **Environment mechanics:** finish furniture, walls and liquid placement with preview, validation and recovery-safe persistence; add more authored world props only after those contracts exist.
8. **Production audio and soft debt:** add licensed/project-owned biome loops and compact gameplay cues, then resolve the 34 review assets in runtime-priority batches.

## Exit Gates For Every Polish Slice

- Runtime reference and ownership are explicit; no showcase-only asset is called finished.
- New raster assets have brief, exact dimensions, manifest metadata, preview, audit and provenance.
- No disk I/O, decode, generation or unbounded allocation enters Draw or another known hot path.
- Debug and Release focused tests pass, followed by the full suite when the shared tree is stable.
- Visible work exits with a real client capture at representative resolution; performance-sensitive work includes before/after p95/p99 and memory evidence.
