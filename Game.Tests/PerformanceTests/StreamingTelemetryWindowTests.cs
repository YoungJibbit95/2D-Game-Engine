using Game.Core.Diagnostics;
using Game.Core.World.Streaming;
using Xunit;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class StreamingTelemetryWindowTests
{
    [Fact]
    public void Add_IsAllocationFreeAndAggregatePreservesBackpressureAndRecoveryDeltas()
    {
        var window = new StreamingTelemetryWindow(64);
        var telemetry = CreateTelemetry(sequence: 0);
        for (var index = 0; index < 128; index++)
        {
            window.Add(index, telemetry);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100_000; index++)
        {
            window.Add(index, telemetry);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(0, allocated);

        window.Clear();
        for (var sequence = 10; sequence <= 14; sequence++)
        {
            window.Add(sequence, CreateTelemetry(sequence));
        }

        var aggregate = window.CaptureAggregate();
        Assert.Equal(5, aggregate.SampleCount);
        Assert.Equal(10, aggregate.FirstSequence);
        Assert.Equal(14, aggregate.LastSequence);
        Assert.Equal(4, aggregate.LoadOperations);
        Assert.Equal(8, aggregate.GenerateOperations);
        Assert.Equal(4, aggregate.CancellationRequests);
        Assert.Equal(4, aggregate.StaleResultsRejected);
        Assert.Equal(4, aggregate.FailedJobs);
        Assert.Equal(4, aggregate.MaxApplyQueueLength);
        Assert.Equal(1d, aggregate.BackpressureRatio);
    }

    [Fact]
    public void RingBuffer_ReportsOnlyLatestBoundedWindow()
    {
        var window = new StreamingTelemetryWindow(4);
        for (var sequence = 0; sequence < 10; sequence++)
        {
            window.Add(sequence, CreateTelemetry(sequence));
        }

        var aggregate = window.CaptureAggregate();
        Assert.Equal(4, aggregate.SampleCount);
        Assert.Equal(6, aggregate.FirstSequence);
        Assert.Equal(9, aggregate.LastSequence);
        Assert.Equal(3, aggregate.LoadOperations);
        Assert.Equal(6, aggregate.GenerateOperations);
    }

    private static ChunkStreamingTelemetry CreateTelemetry(long sequence)
    {
        var depth = (int)(sequence % 5);
        return new ChunkStreamingTelemetry(
            PendingLoadJobs: depth,
            PendingSaveJobs: depth / 2,
            ApplyQueueLength: depth,
            DeferredLoadRequests: depth + 1,
            DeferredUnloadRequests: depth / 2,
            QueuedDecodedBytes: depth * 4096L,
            LoadedDecodedBytes: sequence * 1024,
            GeneratedDecodedBytes: sequence * 2048,
            AppliedDecodedBytes: sequence * 1024,
            SavedDecodedBytes: sequence * 512,
            LoadOperations: sequence,
            GenerateOperations: sequence * 2,
            ApplyOperations: sequence,
            SaveOperations: sequence / 2,
            UnloadOperations: sequence / 3,
            CancellationRequests: sequence,
            CancelledJobs: sequence / 2,
            StaleResultsRejected: sequence,
            FailedJobs: sequence,
            LoadTime: TimeSpan.FromMilliseconds(sequence),
            GenerateTime: TimeSpan.FromMilliseconds(sequence * 2),
            ApplyTime: TimeSpan.FromMilliseconds(sequence),
            SaveTime: TimeSpan.FromMilliseconds(sequence / 2d));
    }
}
