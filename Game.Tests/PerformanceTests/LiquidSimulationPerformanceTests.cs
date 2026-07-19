using System.Diagnostics;
using Game.Core.World;
using Game.Core.World.Liquids;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class LiquidSimulationPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public LiquidSimulationPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ActiveCellStep_HasBoundedDistributionAndNoSteadyAllocation()
    {
        const int width = 512;
        const int height = 64;
        const int activeCells = 128;
        const int samples = 1_000;
        var world = new World(width, height, WorldMetadata.CreateDefault(seed: 19));
        for (var x = 0; x < activeCells; x++)
        {
            var tileX = x * 3 + 1;
            world.SetTile(tileX, 8, TileInstance.Liquid(255));
            world.SetTile(tileX, 9, KnownTileIds.Stone);
            world.SetTile(tileX, 7, KnownTileIds.Stone);
            world.SetTile(tileX - 1, 8, KnownTileIds.Stone);
            world.SetTile(tileX + 1, 8, KnownTileIds.Stone);
        }

        var system = new LiquidSimulationSystem();
        var workspace = new LiquidSimulationWorkspace(initialCapacity: activeCells * 2);
        var options = LiquidSimulationOptions.Default with
        {
            MaxCellsPerStep = activeCells,
            MaxTransferOperationsPerStep = activeCells * 3
        };

        RunSamples(world, system, workspace, options, activeCells, 64, output: null);

        const int scanSamples = 64;
        var fullRegionScan = new double[scanSamples];
        var fullRegion = new RectI(0, 0, width, height);
        _ = CountActiveLiquidsByFullRegionScan(world, fullRegion);
        var fullScanChecksum = 0;
        for (var sample = 0; sample < scanSamples; sample++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            fullScanChecksum += CountActiveLiquidsByFullRegionScan(world, fullRegion);
            fullRegionScan[sample] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        }

        Array.Sort(fullRegionScan);

        var elapsed = new double[samples];
        var processedCells = 0;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < samples; sample++)
        {
            ActivateProbeCells(workspace, activeCells);
            var startedAt = Stopwatch.GetTimestamp();
            var result = system.Step(world, workspace, options);
            elapsed[sample] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            processedCells += result.ProcessedCells;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Array.Sort(elapsed);
        var measurement =
            $"active liquid {activeCells} cells: p50={Percentile(elapsed, 0.50):F4} ms, " +
            $"p95={Percentile(elapsed, 0.95):F4} ms, " +
            $"p99={Percentile(elapsed, 0.99):F4} ms, " +
            $"allocation={allocated / (double)samples:F1} B/step; " +
            $"legacy full-region scan probe p50={Percentile(fullRegionScan, 0.50):F4} ms, " +
            $"p99={Percentile(fullRegionScan, 0.99):F4} ms";
        _output.WriteLine(measurement);

        Assert.Equal(activeCells * samples, processedCells);
        Assert.Equal(activeCells * scanSamples, fullScanChecksum);
        Assert.True(allocated == 0, measurement);
        Assert.True(Percentile(elapsed, 0.99) <= 2, measurement);
    }

    [Fact]
    public void CompatibilityRegionSeeding_HasAHardPerStepScanBudget()
    {
        const int width = 512;
        const int height = 256;
        const int scanBudget = 1_024;
        var world = new World(width, height, WorldMetadata.CreateDefault(seed: 20));
        for (var chunkY = 0; chunkY < height / Game.Core.GameConstants.ChunkSize; chunkY++)
        {
            for (var chunkX = 0; chunkX < width / Game.Core.GameConstants.ChunkSize; chunkX++)
            {
                world.GetOrCreateChunk(new ChunkPos(chunkX, chunkY));
            }
        }

        var options = LiquidSimulationOptions.Default with
        {
            MaxSeedTileChecksPerStep = scanBudget
        };
        var result = new LiquidSimulationSystem().Step(
            world,
            new RectI(0, 0, width, height),
            options);

        Assert.Equal(scanBudget, result.SeedTilesChecked);
        Assert.Equal(1, result.PendingSeedRegions);
        Assert.True(result.SeedBudgetExhausted);
        Assert.Equal(0, result.ProcessedCells);
    }

    private static void RunSamples(
        World world,
        LiquidSimulationSystem system,
        LiquidSimulationWorkspace workspace,
        LiquidSimulationOptions options,
        int activeCells,
        int samples,
        double[]? output)
    {
        for (var sample = 0; sample < samples; sample++)
        {
            ActivateProbeCells(workspace, activeCells);
            var startedAt = Stopwatch.GetTimestamp();
            system.Step(world, workspace, options);
            if (output is not null)
            {
                output[sample] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            }
        }
    }

    private static void ActivateProbeCells(LiquidSimulationWorkspace workspace, int count)
    {
        for (var x = 0; x < count; x++)
        {
            workspace.Activate(new TilePos(x * 3 + 1, 8));
        }
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static int CountActiveLiquidsByFullRegionScan(World world, RectI region)
    {
        var count = 0;
        for (var y = region.Bottom - 1; y >= region.Top; y--)
        {
            for (var x = region.Left; x < region.Right; x++)
            {
                count += world.GetTile(x, y).HasLiquid ? 1 : 0;
            }
        }

        return count;
    }
}
