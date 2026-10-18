# PR-0A / YJSE-P0-001: cross-platform assets and release CI

Date: 2026-10-18. Branch: `codex/yjse-p0-001-asset-ci`.
Base: `5ceb115420cc91ae2cdd36d0b63e4c8c476e05ee` (`origin/master`).
Integrator: Codex lead. Asset and CI investigators: two real read-only subagents; independent integrated-diff review uses the asset investigator. Only the lead writes this worktree.
Authorization: feature branch, commits, push and PR explicitly permitted by the user; no merge or force-push.

## Problem and scope

The original checkout is on `main` / `164795ae74b9928fa1c608adeadfe6f1396aeea1`, eight README commits behind remote master, with 96 tracked modifications and 185 untracked files. PR-0A uses a separate clean worktree at `F:\Coding\YJsE-PR-0A`; none of that unrelated engine/art work is included or discarded.

All 268 authored sprites were checked against actual enumerated file names with ordinal case comparison: 62 paths in seven manifests use `Assets/` although Git and disk contain `assets/`. Matching generation briefs and seven generator scripts reproduce the mistake; twelve website sources and two CI paths share it. No PNG is actually absent. Packs: Wave05, Wave06Character, BackgroundDepthV6, LooseFoliageV1/V2, TerrainPolishV1 and TreeTrunksV3.

Historical failed engine CI, independently inspected: [run 31577193431](https://github.com/YoungJibbit95/2D-Game-Engine/actions/runs/31577193431). Ubuntu Release fails strict texture preload; Ubuntu Debug fails Wave05 audit; website fails source-file validation. Windows Release fails before frame 1 because the hosted system OpenGL lacks framebuffer-object extensions. These are baseline failures, not results of this PR.

Current clean locked restore also fails `NU1902` for MessagePack 3.1.8. The minimal patch update to 3.1.9 and all four lockfiles retains warnings-as-errors. [Upstream advisory and patched version](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h).

## Delivered behavior

- Correct only path references, including their generator sources; sprite IDs, PNG bytes, content schemas, saves and simulation are unchanged. Historical generation/audit records are retained rather than regenerated as fabricated evidence.
- `SpriteAssetAuditService` indexes actual file names using ordinal comparison even on Windows, honors each asset's own SourceRoot and compares generation-brief paths exactly. Existing shared-source aliases remain valid.
- Repository regression requires every sprite file to be present and treats missing files as errors. Negative fixtures cover directory/file case, brief case, source-root escapes and mod/alias ownership.
- Website source copies require case-exact directory/file names and matching manifest case, in addition to existing byte equality. A Node test proves rejection on Windows too.
- Published-client runner launches Main Menu, World Select and deterministic Forest/day/clear gameplay from the publish directory, overrides external content-root settings, creates a fresh output directory and checks exit status, PNG, passing JSON and captured gameplay. Both the existing client watchdog and an outer native-initialization deadline remain active. Every state's stdout/stderr is retained, including failures.
- CI keeps Windows/Ubuntu Debug/Release, all existing tests, performance gates and strict asset checks. Windows graphics uses official signed MSYS2 Mesa packages in runner temporary storage; only the smoke process receives SDL's library override and dependency PATH. The pinned [setup action](https://github.com/msys2/setup-msys2/tree/ec48f7c5447b3140e2b088413ae3a55687bccb6e) checks its base installer; Pacman verifies distribution package signatures. Package versions and DLL hashes are logged. No Mesa DLL enters the game bundle or system driver directories. [Mesa software renderer](https://docs.mesa3d.org/drivers/llvmpipe.html), [MSYS2 package management](https://www.msys2.org/docs/package-management/).

No renderer, lighting, worldgen, gameplay, UI redesign or save migration is part of this PR. Larger path-only file count follows from correcting every producer and consumer of the same defect.

## Verification

Local host: Windows, .NET SDK 8.0.420, Node 26.3.1, Python 3.12.10, Pillow 12.3.0, NVIDIA RTX 3070 Ti driver 32.0.16.1692. CI uses its declared Node 24/Python 3.13 and Mesa software renderers. Local results do not establish hosted results.

Commands below run in the isolated worktree; detailed logs/TRX/JSON/images are under ignored `artifacts/`. The publish bundle is physically outside the source tree at `F:\Coding\YJsE-PR-0A-publish`.

| Gate | Status | Command / evidence |
| --- | --- | --- |
| Baseline path repro | fail (expected) | `artifacts/baseline-path-errors.json`: 62 wrong-case paths, no missing PNG |
| Baseline restore | fail (expected) | `dotnet restore YjsE.sln --nologo --locked-mode`; `artifacts/baseline-restore.log`: NU1902 |
| Regression before fix | fail (expected) | `dotnet test ... --filter FullyQualifiedName~Game.Tests.AssetTests.SpriteAssetTests`; five failing cases in `red-path-regression.trx` |
| Locked restore | pass | `dotnet restore YjsE.sln --nologo --locked-mode`; `artifacts/restore.log` |
| Style | pass | `dotnet format style YjsE.sln --verify-no-changes --no-restore --severity warn --verbosity minimal`; `artifacts/format.log` |
| Debug build / functional tests | pass | `dotnet build YjsE.sln -c Debug --no-restore --nologo --warnaserror`; workflow functional filter; 1,725/1,725; `artifacts/debug-build.log`, `test-results/debug-functional.trx` |
| Release build / functional tests | pass | Same commands with Release; 1,725/1,725; `artifacts/release-build.log`, `test-results/release-functional.trx` |
| Strict main / Wave05 audits | pass | Existing `audit_sprites.py --fail-on-hard-issues` commands, corrected `--scan-root assets/Wave05`; `artifacts/asset-audit*.json`: zero hard issues |
| Deterministic previews | pass | Existing UI `--check-existing`; `python Game.Data/assets/Wave05/tools/build_wave05_preview.py` then `git diff --exit-code` on PNG/summary: unchanged |
| Website | pass | `npm ci`, `npm run validate`; Node path fixture, Svelte check/build, 26 docs, 30 byte-exact source copies; `artifacts/website.log` |
| Published GPU smokes | pass | `dotnet publish Game.Client/Game.Client.csproj -c Release --no-restore --output F:\Coding\YJsE-PR-0A-publish`; `tools/run_client_smokes.ps1`: three real native-GPU states, 263 resources / 1,518 frames / 0 invalid |
| Missing-asset negative smoke | pass | Temporarily removed published `marsh_frog.png`; all three states rejected it with exit 1, despite older successful captures; restored the file in `finally`. `artifacts/client-smoke-negative.log` |
| Isolated Release performance | pass | `tools/run_isolated_performance_tests.ps1 -Configuration Release`; all 21 classes / 39 tests passed at unchanged budgets; `artifacts/isolated-performance.log` |
| Quick benchmark harness | pass | `dotnet run --project Game.Benchmarks -c Release --no-build -- --quick --output artifacts/performance-quick.json`; matching 240-tick replay hashes |
| Hosted Windows / Ubuntu | not run | New PR workflow must pass; historical or local runs do not satisfy this gate |
| Golden scene corpus / gameplay playthrough | not run | Separate PR-0B; these smoke captures are not a completed golden corpus or a 15–30 minute gameplay acceptance |

Visual evidence: 640x360, 30 frames with 10 warmup frames, 60 FPS cap. Forest/day/clear capture inspected: real player, world tiles, layered background and HUD visible. The synthetic asset sample panel is excluded by existing scene-pixel validation. GPU timing and performance improvement: unknown; no rendering optimization is claimed.

## Review, risk and rollback

Independent read-only integrated-diff review: pass after correcting the review finding for null/empty/whitespace SourceRoots and adding corresponding regression cases. No open findings. Reviewer did not execute tests; test evidence above was executed by the lead. Hosted run links will be recorded after execution. Until required hosted gates pass, this is a draft candidate.

Mesa is a rolling distribution: action/installer are pinned but the signed package dependency closure is resolved at run time and recorded, not frozen as a distribution snapshot. A package/graphics failure must fail CI; there is no placeholder, skip or success fallback. Existing palette/silhouette review debt is unaffected. Save/load and negative-X behavior are covered by the unchanged full functional suite; renderer-neutral Core boundaries remain intact.

Rollback: revert the PR commits. No content-ID, image, save-format or runtime renderer migration needs reversal. The original dirty checkout remains available unchanged.

Next independent PR: **PR-0B / YJSE-P0-002**, reproducible Golden Scene captures, visual regression and benchmark fixtures after PR-0A's hosted acceptance. Do not begin V2 renderer/lighting/worldgen rewrites automatically.
