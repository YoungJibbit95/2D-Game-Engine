using Game.Core;
using Game.Core.Diagnostics.Performance;
using Game.Core.Saving;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using Game.Core.World.Streaming.Diagnostics;
using Xunit;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class BidirectionalCameraTraversalPerformanceTests
{
    [Fact]
    public void ColdAndWarmTraversal_AcrossNegativeAndPositiveX_ProducesBoundedDistributions()
    {
        var profile = WorldGenerationProfile.Small;
        var generator = new InfiniteWorldChunkGenerator();
        var world = generator.CreateWorld(profile, 73013, "Long Session Streaming");
        using var jobs = new ChunkStreamingJobRunner(
            generator,
            new WorldSaveService(WorldChunkStorageMode.RegionFiles));
        var streaming = new ChunkStreamingService(jobs);
        var streamingOptions = new ChunkStreamingOptions
        {
            LoadMarginChunks = 1,
            UnloadMarginChunks = 70,
            MaxChunkOperationsPerUpdate = 8,
            MaxConcurrentLoadJobs = 4,
            MaxConcurrentSaveJobs = 1,
            MaxApplyQueueLength = 16,
            MaxApplyTimePerUpdate = TimeSpan.FromMilliseconds(4),
            MaxApplyDecodedBytesPerUpdate = 512 * 1024
        };
        var traversalOptions = new BidirectionalCameraTraversalOptions
        {
            MinimumCenterTileX = -1024,
            MaximumCenterTileX = 1024,
            StepTiles = GameConstants.ChunkSize,
            VisibleWidthTiles = 96,
            VisibleHeightTiles = 48,
            MaxSettleUpdatesPerPosition = 4096,
            ColdBudgetMilliseconds = 250,
            WarmBudgetMilliseconds = 1000d / 60d
        };

        var result = new BidirectionalCameraTraversalHarness(
            world,
            profile,
            streaming,
            streamingOptions,
            traversalOptions).Run();

        Assert.Equal(65, result.CameraPositionsPerPass);
        Assert.Equal(65, result.Cold.TotalSampleCount);
        Assert.Equal(65, result.Warm.TotalSampleCount);
        Assert.Equal(65, result.Cold.RetainedSampleCount);
        Assert.Equal(65, result.Warm.RetainedSampleCount);
        Assert.Equal(LongSessionDistributionLabels.StreamingColdBidirectionalSettleMilliseconds, result.Cold.Label);
        Assert.Equal(LongSessionDistributionLabels.StreamingWarmBidirectionalSettleMilliseconds, result.Warm.Label);
        Assert.True(result.TraversedNegativeX);
        Assert.True(result.TraversedPositiveX);
        Assert.True(result.ColdGenerateOperations > 0);
        Assert.Equal(0, result.WarmGenerateOperations);
        Assert.True(result.ColdApplyOperations > 0);
        Assert.Equal(0, result.WarmApplyOperations);
        Assert.Equal(0, result.FailedJobs);
        Assert.True(result.MaxResidentChunks > 0);
        Assert.True(result.ColdUpdateCount >= result.CameraPositionsPerPass);
        Assert.Equal(result.CameraPositionsPerPass, result.WarmUpdateCount);
        Assert.True(result.Cold.P50 <= result.Cold.P95);
        Assert.True(result.Cold.P95 <= result.Cold.P99);
        Assert.True(result.Cold.P99 <= result.Cold.Maximum);
        Assert.True(result.Warm.P50 <= result.Warm.P95);
        Assert.True(result.Warm.P95 <= result.Warm.P99);
        Assert.True(result.Warm.P99 <= result.Warm.Maximum);
        Assert.True(result.Cold.P99 <= result.Cold.Budget, $"cold p99={result.Cold.P99:0.###} ms");
        Assert.True(result.Warm.P99 <= result.Warm.Budget, $"warm p99={result.Warm.P99:0.###} ms");
        Assert.True(
            result.Warm.P95 <= result.Cold.P95,
            $"cold p95={result.Cold.P95:0.###} ms, warm p95={result.Warm.P95:0.###} ms");

        LongSessionDistributionArtifactExporter.ExportIfRequested(
            "streaming.camera-bidirectional",
            result.Cold,
            result.Warm);
    }
}
