# YjsE Repository Working Agreement

## Source Of Truth

Use this order when implementation and documentation disagree:

1. Current code.
2. Successful builds, tests, benchmarks, and runtime smokes from the current checkout.
3. `Docs/Development/CAPABILITY_MATRIX.md`.
4. `Docs/Development/ARCHITECTURE_DECISIONS.md`.
5. `Docs/Development/WORK_LEDGER.md`.
6. Other engine documentation and checklists.

Capability status values are limited to `verified`, `implemented-unverified`, `partial`, `planned`, `blocked`, and `deprecated`.

## Project Boundaries

- `Game.Core` owns renderer-neutral simulation, gameplay rules, content contracts, persistence, runtime services, and UI models. It must not reference MonoGame types.
- `Game.Client` owns the MonoGame host, device input/audio adapters, rendering, client states, and concrete widgets. It must not become an authoritative gameplay simulation.
- `Game.Data` is replaceable sample/dev validation content. Concrete games can supply their own `yjse.game.json` and content root.
- `Game.Tests` validates core behavior and content/runtime contracts.
- Preserve the Terraria-like sideview sandbox as the primary reference path. Keep Farming, Topdown, Dialogue, and Shop modules compiling and tested without starting unrelated genre expansions.

## Session Resume Contract

For a normal continuation:

1. Read this file and `Docs/Development/WORK_LEDGER.md`.
2. Inspect branch, HEAD, and working-tree changes before editing.
3. Fix existing build/test failures first.
4. Continue the ledger's `Exact next action` within the active epic and exit gate.
5. Implement the largest coherent slice that can be validated safely.
6. Run relevant Debug/Release tests, runtime smokes, audits, and measurements.
7. Update the capability matrix and work ledger, including a new exact resume action.

Do not commit or push unless the user explicitly asks. Never discard unrelated or pre-existing working-tree changes.

## Engineering Gates

- No synchronous disk I/O, chunk decode, or world generation in `Draw`.
- Do not create duplicate GPU textures for frames of one PNG; frames use source rectangles.
- Do not duplicate core gameplay rules in client overlays or maintain a second live simulation beside `GameSimulation`.
- Avoid per-frame LINQ, string-key construction, and unbounded allocation in known hot paths.
- Save-format changes require migration and compatibility tests; direct region overwrite requires a recovery-safe plan.
- New sprites require manifest metadata, generation brief/specification, exact dimensions, runtime reference, audit coverage, preview, and provenance.
- Measure before and after performance-sensitive changes.
