using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace Game.Benchmarks;

internal static class BenchmarkDotNetHost
{
    private const string ShortFlag = "--bdn-short";
    private const string DryFlag = "--bdn-dry";

    public static bool TryRun(string[] args, out int exitCode)
    {
        var isShort = args.Contains(ShortFlag, StringComparer.OrdinalIgnoreCase);
        var isDry = args.Contains(DryFlag, StringComparer.OrdinalIgnoreCase);
        if (!isShort && !isDry)
        {
            exitCode = 0;
            return false;
        }

        var forwarded = args
            .Where(argument => !string.Equals(argument, ShortFlag, StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(argument, DryFlag, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var job = (isDry ? Job.Dry : Job.ShortRun)
            .WithRuntime(CoreRuntime.Core80)
            .WithId(isDry ? "YjsE-Dry" : "YjsE-Short");
        var artifacts = Path.Combine(FindRepositoryRoot(), "artifacts", "benchmarkdotnet");
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(job)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddExporter(JsonExporter.Full)
            .WithArtifactsPath(artifacts)
            .WithOptions(ConfigOptions.JoinSummary);

        var summaries = BenchmarkSwitcher
            .FromTypes(
            [
                typeof(InfiniteWorldGenerationBenchmarks),
                typeof(ChunkStreamingBenchmarks),
                typeof(LightingBenchmarks),
                typeof(SpawnSchedulerBenchmarks),
                typeof(EntityAiBenchmarks),
                typeof(SimulationTelemetryBenchmarks),
                typeof(FrameSnapshotQueryBenchmarks)
            ])
            .Run(forwarded, config);
        exitCode = summaries.Any(summary => summary.Reports.Any(report => !report.Success)) ? 1 : 0;
        return true;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Program.FindGameDataRoot());
        return directory.Parent?.FullName ?? Directory.GetCurrentDirectory();
    }
}
