# YjsE Performance Harness

The benchmark project has two complementary modes:

- The versioned YjsE harness writes stable aggregate JSON for CI and milestone comparisons.
- BenchmarkDotNet provides statistically sampled micro/meso benchmarks with memory diagnostics.

## Stable harness

```powershell
dotnet run --project Game.Benchmarks -c Release -- --quick --output artifacts/performance-quick-current.json
dotnet run --project Game.Benchmarks -c Release -- --output artifacts/performance-calibration-current.json
```

## BenchmarkDotNet

Run the one-iteration compile/execution smoke first:

```powershell
dotnet run --project Game.Benchmarks -c Release -- --bdn-dry --filter "*SimulationTelemetry*"
```

Run short measured jobs for one category or the complete engine set:

```powershell
dotnet run --project Game.Benchmarks -c Release -- --bdn-short --filter "*InfiniteWorld*"
dotnet run --project Game.Benchmarks -c Release -- --bdn-short --filter "*ChunkStreaming*"
dotnet run --project Game.Benchmarks -c Release -- --bdn-short
```

Results are written below `artifacts/benchmarkdotnet/`. Do not compare Debug and Release,
different runtimes, dirty and clean revisions, or different hardware as if they were one series.

## Scenarios

| Group | Measured contract |
| --- | --- |
| Infinite world | Fresh versus reused generator at negative, origin and positive chunk X |
| Streaming | Cold and prewarmed camera traces crossing negative and positive world X |
| Lighting | Dirty-region recomputation with data-driven torch sources |
| Spawning | Population-cap/despawn scan for 200 entities and two activity sources |
| AI | One update of 200 flock-capable critters |
| Simulation | Representative fixed tick with phase telemetry disabled/enabled |
| Snapshot query | Index and concrete-enumerator scans over 0, 32 and 200 immutable entity values |

Stable harness schema v3 also exports aggregate allocation and timing for every fixed-tick phase.
`performance-baseline.json` preserves the selected values from the existing 2026-07-12 schema-v2
Release calibration artifact. `performance-thresholds.json` contains regression guardrails, not benchmark
claims. A threshold must only be tightened after a checked-in Release report demonstrates headroom
on supported CI.
