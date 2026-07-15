using Game.Core.Diagnostics.Replay;
using Xunit;

namespace Game.Tests.DeterminismTests.Replay;

public sealed class ReplayRecorderTests
{
    [Fact]
    public void Record_UsesBoundedChronologicalRingAndCountsDroppedFrames()
    {
        var recorder = new ReplayRecorder(3);
        for (var tick = 0; tick < 7; tick++)
        {
            var frame = ReplayTestData.Frame(tick);
            recorder.Record(frame);
        }

        var snapshot = recorder.CaptureSnapshot();

        Assert.Equal(3, recorder.Count);
        Assert.Equal(4, recorder.DroppedFrameCount);
        Assert.Equal([4L, 5L, 6L], snapshot.Frames.Select(frame => frame.Tick));
        Assert.True(recorder.TryGetFrame(0, out var oldest));
        Assert.Equal(4, oldest.Tick);
        Assert.False(recorder.TryGetFrame(3, out _));
    }

    [Fact]
    public void Record_RejectsNonMonotoneTickOrSequence()
    {
        var recorder = new ReplayRecorder(4);
        recorder.Record(ReplayTestData.Frame(10, sequence: 20));

        Assert.Throws<ArgumentException>(
            () => recorder.Record(ReplayTestData.Frame(10, sequence: 21)));
        Assert.Throws<ArgumentException>(
            () => recorder.Record(ReplayTestData.Frame(11, sequence: 20)));
    }

    [Fact]
    public void SnapshotJsonRestore_ContinuesIdenticallyToUninterruptedRecorder()
    {
        var uninterrupted = new ReplayRecorder(4);
        var resumable = new ReplayRecorder(4);
        for (var tick = 0; tick < 4; tick++)
        {
            var frame = ReplayTestData.Frame(tick, (ulong)(tick * 17), useItem: tick == 2);
            uninterrupted.Record(frame);
            resumable.Record(frame);
        }

        var serialized = ReplayJsonSerializer.Serialize(resumable.CaptureSnapshot());
        var resumed = ReplayRecorder.FromSnapshot(ReplayJsonSerializer.Deserialize(serialized));
        for (var tick = 4; tick < 11; tick++)
        {
            var frame = ReplayTestData.Frame(tick, (ulong)(tick * 17), useItem: tick == 8);
            uninterrupted.Record(frame);
            resumed.Record(frame);
        }

        Assert.Equal(uninterrupted.DroppedFrameCount, resumed.DroppedFrameCount);
        Assert.Equal(
            uninterrupted.CaptureSnapshot().Frames,
            resumed.CaptureSnapshot().Frames);
    }

    [Fact]
    public void Record_SteadyStateDoesNotAllocate()
    {
        var recorder = new ReplayRecorder(32);
        for (var tick = 0; tick < 64; tick++)
        {
            recorder.Record(ReplayTestData.Frame(tick));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 64; tick < 1_064; tick++)
        {
            recorder.Record(ReplayTestData.Frame(tick));
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void CapacityAndRestore_AreDefensivelyBounded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayRecorder(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReplayRecorder(ReplayLimits.MaximumFrameCapacity + 1));

        var recorder = new ReplayRecorder(4);
        var differentCapacity = new ReplayRecordingSnapshot
        {
            Capacity = 3,
            Frames = [ReplayTestData.Frame(0)]
        };

        Assert.Throws<InvalidDataException>(() => recorder.Restore(differentCapacity));
    }
}
