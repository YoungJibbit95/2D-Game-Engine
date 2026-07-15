using Game.Core.Diagnostics;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using Xunit;

namespace Game.Tests.PerformanceTests;

public sealed class ChunkStreamingPerformanceSoakTests
{
    [Fact]
    public void BidirectionalCameraTrace_KeepsQueueBoundedAndProducesAggregatePressureSeries()
    {
        var profile = WorldGenerationProfile.Small;
        var generator = new InfiniteWorldChunkGenerator(seed => new DeterministicNoise(seed));
        var world = generator.CreateWorld(profile, 90210, "Streaming Soak");
        var runner = new ImmediateGenerationJobRunner(generator);
        var streaming = new ChunkStreamingService(runner);
        var options = new ChunkStreamingOptions
        {
            LoadMarginChunks = 1,
            UnloadMarginChunks = 2,
            MaxChunkOperationsPerUpdate = 3,
            MaxConcurrentLoadJobs = 2,
            MaxConcurrentSaveJobs = 1,
            MaxApplyQueueLength = 6
        };
        var series = new StreamingTelemetryWindow(256);
        long sequence = 0;
        var centers = new[]
        {
            -4096, -2048, -512, -64, 0, 64, 512, 2048, 4096,
            2048, 512, 64, 0, -64, -512, -2048, -4096
        };
        var sawNegativeChunk = false;
        var sawPositiveChunk = false;

        for (var traceIndex = 0; traceIndex < centers.Length; traceIndex++)
        {
            var visible = new RectI(centers[traceIndex] - 48, profile.SurfaceBaseY - 20, 96, 48);
            for (var update = 0; update < 8; update++)
            {
                var result = streaming.Update(world, profile, visible, options: options);
                series.Add(sequence++, result.Telemetry);
                Assert.InRange(result.Telemetry.ApplyQueueLength, 0, options.MaxApplyQueueLength);
                Assert.InRange(result.Telemetry.PendingLoadJobs, 0, options.MaxConcurrentLoadJobs);
                sawNegativeChunk |= world.Chunks.Keys.Any(position => position.X < 0);
                sawPositiveChunk |= world.Chunks.Keys.Any(position => position.X > 0);
            }
        }

        var aggregate = series.CaptureAggregate();
        Assert.Equal(sequence, aggregate.SampleCount);
        Assert.True(aggregate.GenerateOperations > 0);
        Assert.True(aggregate.ApplyOperations > 0);
        Assert.Equal(0, aggregate.FailedJobs);
        Assert.InRange(aggregate.MaxApplyQueueLength, 0, options.MaxApplyQueueLength);
        Assert.InRange(aggregate.MaxPendingLoadJobs, 0, options.MaxConcurrentLoadJobs);
        Assert.True(aggregate.BackpressureSampleCount > 0);
        Assert.True(sawNegativeChunk, "The camera trace never materialized a negative-X chunk.");
        Assert.True(sawPositiveChunk, "The camera trace never materialized a positive-X chunk.");
    }

    private sealed class ImmediateGenerationJobRunner : IChunkStreamingJobRunner
    {
        private readonly InfiniteWorldChunkGenerator _generator;

        public ImmediateGenerationJobRunner(InfiniteWorldChunkGenerator generator)
        {
            _generator = generator;
        }

        public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
            ChunkStreamingLoadJobRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = DateTime.UtcNow;
            var chunk = _generator.GenerateChunk(
                request.GenerationProfile,
                request.WorldMetadata.Seed,
                request.Position);
            return Task.FromResult(new ChunkStreamingLoadJobResult(
                request,
                ChunkStreamingChunkSnapshot.Capture(chunk),
                LoadedFromSave: false,
                TimeSpan.Zero,
                DateTime.UtcNow - startedAt));
        }

        public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
            ChunkStreamingSaveJobRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChunkStreamingSaveJobResult(request, Succeeded: true, TimeSpan.Zero));
        }
    }

    private sealed class DeterministicNoise : INoiseService
    {
        private readonly int _seed;

        public DeterministicNoise(int seed)
        {
            _seed = seed;
        }

        public float GetNoise(float x, float y)
        {
            var value = MathF.Sin(x * 2.117f + y * 0.731f + _seed * 0.001f);
            return Math.Clamp(value, -1f, 1f);
        }
    }
}
