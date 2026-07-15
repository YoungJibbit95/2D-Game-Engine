using Game.Core.Diagnostics.Replay;
using Xunit;

namespace Game.Tests.DeterminismTests.Replay;

public sealed class ReplayComparerTests
{
    [Fact]
    public void Compare_ExactMatchReportsLastCheckpoint()
    {
        var recording = ReplayTestData.Snapshot(
            ReplayTestData.Frame(10, 100, sequence: 30),
            ReplayTestData.Frame(11, sequence: 31),
            ReplayTestData.Frame(12, 120, sequence: 32));

        var result = ReplayComparer.Compare(recording, recording with { Frames = recording.Frames.ToArray() });

        Assert.True(result.IsMatch);
        Assert.Equal(ReplayDivergenceReason.None, result.Reason);
        Assert.Equal(new ReplayCheckpointMatch(12, 32, 120), result.LastMatchingCheckpoint);
    }

    [Fact]
    public void Compare_HashMismatchReportsFirstDivergenceAndPriorCheckpointContext()
    {
        var expected = ReplayTestData.Snapshot(
            ReplayTestData.Frame(0, 10),
            ReplayTestData.Frame(1),
            ReplayTestData.Frame(2, 20),
            ReplayTestData.Frame(3, 30));
        var actual = ReplayTestData.Snapshot(
            ReplayTestData.Frame(0, 10),
            ReplayTestData.Frame(1),
            ReplayTestData.Frame(2, 999),
            ReplayTestData.Frame(3, 30));

        var result = ReplayComparer.Compare(expected, actual);

        Assert.False(result.IsMatch);
        Assert.Equal(ReplayDivergenceReason.HashMismatch, result.Reason);
        Assert.Equal(2, result.DivergenceTick);
        Assert.Equal(20UL, result.ExpectedHash);
        Assert.Equal(999UL, result.ActualHash);
        Assert.Equal(new ReplayCheckpointMatch(0, 0, 10), result.LastMatchingCheckpoint);
        Assert.Equal(expected.Frames[2], result.ExpectedInput);
        Assert.Equal(actual.Frames[2], result.ActualInput);
    }

    [Fact]
    public void Compare_ReportsMissingAndExtraFramesAtFirstSequenceGap()
    {
        var complete = ReplayTestData.Snapshot(
            ReplayTestData.Frame(0),
            ReplayTestData.Frame(1),
            ReplayTestData.Frame(2));
        var missingMiddle = ReplayTestData.Snapshot(
            ReplayTestData.Frame(0),
            ReplayTestData.Frame(2));

        var missing = ReplayComparer.Compare(complete, missingMiddle);
        var extra = ReplayComparer.Compare(missingMiddle, complete);

        Assert.Equal(ReplayDivergenceReason.MissingFrame, missing.Reason);
        Assert.Equal(1, missing.DivergenceTick);
        Assert.Equal(ReplayDivergenceReason.ExtraFrame, extra.Reason);
        Assert.Equal(1, extra.DivergenceTick);
    }

    [Fact]
    public void Compare_ReportsInputAndCheckpointPresenceDifferences()
    {
        var expectedFrame = ReplayTestData.Frame(4, 44, moveAxis: 0.5f);
        var inputMismatch = ReplayComparer.Compare(
            ReplayTestData.Snapshot(expectedFrame),
            ReplayTestData.Snapshot(ReplayTestData.Frame(4, 44, moveAxis: -0.5f)));
        var missingCheckpoint = ReplayComparer.Compare(
            ReplayTestData.Snapshot(expectedFrame),
            ReplayTestData.Snapshot(expectedFrame with { CheckpointStateHash = null }));
        var extraCheckpoint = ReplayComparer.Compare(
            ReplayTestData.Snapshot(expectedFrame with { CheckpointStateHash = null }),
            ReplayTestData.Snapshot(expectedFrame));

        Assert.Equal(ReplayDivergenceReason.InputMismatch, inputMismatch.Reason);
        Assert.Equal(ReplayDivergenceReason.MissingCheckpoint, missingCheckpoint.Reason);
        Assert.Equal(ReplayDivergenceReason.ExtraCheckpoint, extraCheckpoint.Reason);
    }

    [Fact]
    public void Compare_ReportsOrderAndVersionBeforeLaterHashDifferences()
    {
        var expected = ReplayTestData.Snapshot(
            ReplayTestData.Frame(0, 10),
            ReplayTestData.Frame(1, 20));
        var unordered = expected with
        {
            Frames = [ReplayTestData.Frame(0, 10), ReplayTestData.Frame(0, 999, sequence: 1)]
        };
        var futureRecording = expected with
        {
            FormatVersion = ReplayRecordingSnapshot.CurrentFormatVersion + 1
        };
        var futureFrame = expected with
        {
            Frames =
            [
                expected.Frames[0] with
                {
                    FormatVersion = ReplayInputFrame.CurrentFormatVersion + 1
                },
                expected.Frames[1]
            ]
        };

        Assert.Equal(ReplayDivergenceReason.OrderMismatch, ReplayComparer.Compare(expected, unordered).Reason);
        Assert.Equal(ReplayDivergenceReason.VersionMismatch, ReplayComparer.Compare(expected, futureRecording).Reason);
        Assert.Equal(ReplayDivergenceReason.VersionMismatch, ReplayComparer.Compare(expected, futureFrame).Reason);
    }

    [Fact]
    public void Compare_TrailingFrameIsReportedWithoutReadingPastEitherRecording()
    {
        var one = ReplayTestData.Snapshot(ReplayTestData.Frame(0, 10));
        var two = ReplayTestData.Snapshot(ReplayTestData.Frame(0, 10), ReplayTestData.Frame(1, 20));

        Assert.Equal(ReplayDivergenceReason.MissingFrame, ReplayComparer.Compare(two, one).Reason);
        Assert.Equal(ReplayDivergenceReason.ExtraFrame, ReplayComparer.Compare(one, two).Reason);
    }
}
