# PR-0D native measurement index

Read [the decision and interpretation](../../PR_0D_LIGHTING_PREPARATION_REUSE.md) before comparing numbers. Reuse is opt-in; these observations do not establish a general Prepare/frame/FPS improvement. Original references remain immutable.

## Files and provenance

Each resolution has `per_process.csv` (81 individual processes) and `comparison.json` (environment, actual DLL versions/SHA256, fixture/source identities, descriptive ranges and 18 exact cross-role comparisons). CSV hashes bind each raw capture, PNG and report. BEFORE cache counters are blank/unavailable. All three repetitions are retained, including unfavorable results and allocation/timing outliers.

Versioned min/max JSON leaves are compacted onto single lines for review; parsed data is checked equal to the accepted exporter output. CSV content is unchanged.

[`native_controls.json`](native_controls.json) binds the separately rebuilt policy/test-freeze controls: default OFF, four optimized original Golden pairs/references, actual reset/source expiry and two additional exact comparisons (default/ON day and ON/OFF lifecycle). It records actual loaded DLL identities and successful raw-capture hashes, without replacing calibration timings. Ordinary smoke sidecars have no benchmark metadata; only Golden sidecars attest the mode.

All roots below are relative to `F:\Coding\YJsE-PR-0D\artifacts`. JSON preserves the runner root arguments (workspace-relative here); hashes bind the local raw captures/reports. Accepted captures use 1,800 frames, 300 warmup, three repeats, Release, requested software cap 0 / Immediate presentation. Driver/OS pacing, power and scheduling are uncontrolled; GPU time is unknown.

| Resolution | Role | Accepted diagnostic run root |
| --- | --- | --- |
| 1080p | BEFORE | `before-1080p-clean/61de68e135ba4c5c9c45c286340653da` |
| 1080p | OFF | `off-1080p/d30808d2eee9459f9db85423c9f5c569` |
| 1080p | ON | `on-1080p/3b3051f089404770b1666930fb35861d` |
| 1440p | BEFORE | `before-1440p/1cfc7bd914c34cf8a5f8db9aa81e4b93` |
| 1440p | OFF | `off-1440p/e0201f2a693e416b8ce46aeda024b120` |
| 1440p | ON | `on-1440p/acdd263116514874b6094f933a79ae30` |

BEFORE loads separately published clean base `de83468aa7e5d19f43634ae1df6935299f9d83d2` from `F:\Coding\YJsE-PR-0D-before-publish`. OFF and ON load identical published calibration DLLs from `F:\Coding\YJsE-PR-0D-publish`, frozen at `1cdded8d248e28ac172183eb58d63636d63047d0`. The only later production-runtime change is the opt-in startup default; measured explicit-mode preparation/cache math is unchanged. Rebuilt policy/test controls use `4fd2d6c88bf2b927ac9f755ef52fa8ec18effbbf` / `F:\Coding\YJsE-PR-0D-final-publish` and are correctness controls, not substituted calibration rows.

Accepted timing order was OFF-1080, ON-1080, BEFORE-1440, OFF-1440, ON-1440, correctness controls, then complete clean BEFORE-1080. It was not randomized/interleaved. This PR ran no builds/tests alongside these timing captures; the host was not exclusive. Earlier BEFORE-1080 root `before-1080p/70c81fe8ea1e41819557784235a38357` is excluded wholesale because short tooltests overlapped its period. That replacement decision preceded analysis; no accepted outlier was deleted/retried for a better result.

## Reproduction and retention

Use a separate worktree at the base SHA to restore/build/publish BEFORE. Publish the candidate once and use that exact bundle for both explicit OFF/ON modes. Follow the commands in the dossier for both 1920x1080 and 2560x1440; substitute the GUID root printed by each diagnostic runner. Then run `tools/lighting_reuse_compare.py` with the three roots. It rejects mixed Release/config/OS/CPU/driver/settings/state/binary identities, paced requests, absent/invalid reports and nonexact images before exporting data.

Full raw traces, reports, screenshots, amplified diffs and previews remain in ignored local artifacts. `artifacts/comparison-1080p` and `artifacts/comparison-1440p` hold the complete accepted exporter outputs. Their versioned JSON `artifact_directory` entries refer to those output roots; diff images are not present beside these copied CSV/JSON files. Regeneration is required on another machine; identical hashes are only expected in the same controlled environment.

Hosted runs retain independent raw captures, reports and repeat/ON-OFF/portable diff artifacts. Their current-head links are in [PR #9](https://github.com/YoungJibbit95/2D-Game-Engine/pull/9). They verify Windows/Ubuntu Debug/Release correctness with Mesa, not native NVIDIA performance. Download with `gh run download <run-id> --pattern 'lighting-diagnostics-*' --pattern 'golden-scenes-*' --pattern 'test-results-*' --dir <output>`.

`prepare_*`, `mask_*` and `upload_cpu_*` timing columns are separate inclusive CPU-wall scopes, with invocation and amortized statistics. Do not sum nested scopes or interpret `upload_cpu_*` as GPU execution. `frame_interval_*` measures observed presentation intervals. Cache bytes are bounded CPU payload/numeric-copy estimates, not VRAM or measured bus traffic; OFF also reserves the payload. The dossier records counter denominators, actual allocations and limitations.
