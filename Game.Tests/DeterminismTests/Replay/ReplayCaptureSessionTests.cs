using System.Numerics;
using Game.Core.Diagnostics.Replay;
using Game.Core.Entities;
using Game.Core.Runtime;
using Game.Core.World;
using Xunit;

namespace Game.Tests.DeterminismTests.Replay;

public sealed class ReplayCaptureSessionTests
{
    [Fact]
    public void Record_CapturesDeltaInputAndPeriodicCheckpoint()
    {
        var capture = new ReplayCaptureSession(new ReplayCaptureOptions
        {
            FrameCapacity = 4,
            CheckpointIntervalTicks = 2
        });
        var command = new PlayerCommand(0.5f, true, false, Vector2.UnitX);
        var use = new PlayerItemUseRequest(true, new TilePos(3, -2), new Vector2(48, -32));

        capture.Record(1, command, use, 1f / 75f, null);
        capture.Record(2, command, use, 1f / 75f, 0xCAFEUL);

        var snapshot = capture.CaptureSnapshot();
        Assert.Equal(2, snapshot.Frames.Count);
        Assert.Equal(1f / 75f, snapshot.Frames[0].DeltaSeconds);
        Assert.Null(snapshot.Frames[0].CheckpointStateHash);
        Assert.Equal(0xCAFEUL, snapshot.Frames[1].CheckpointStateHash);
        Assert.True(capture.ShouldCaptureCheckpoint(2));
        Assert.False(capture.ShouldCaptureCheckpoint(3));
    }

    [Fact]
    public void Compare_ReportsDeltaTimeAsInputDivergence()
    {
        var expected = ReplayTestData.Frame(1) with { DeltaSeconds = 1f / 60f };
        var actual = expected with { DeltaSeconds = 1f / 120f };

        var result = ReplayComparer.Compare(
            ReplayTestData.Snapshot(expected),
            ReplayTestData.Snapshot(actual));

        Assert.Equal(ReplayDivergenceReason.InputMismatch, result.Reason);
        Assert.Equal(1, result.DivergenceTick);
    }

    [Fact]
    public void Options_RejectUnboundedCheckpointIntervals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayCaptureSession(
            new ReplayCaptureOptions { CheckpointIntervalTicks = 60 * 60 * 10 + 1 }));
    }
}
