namespace Game.Core.Diagnostics.Replay;

public enum ReplayDivergenceReason
{
    None,
    MissingFrame,
    ExtraFrame,
    OrderMismatch,
    InputMismatch,
    MissingCheckpoint,
    ExtraCheckpoint,
    HashMismatch,
    VersionMismatch
}

public readonly record struct ReplayCheckpointMatch(long Tick, long Sequence, ulong StateHash);

public sealed record ReplayComparisonResult
{
    public bool IsMatch => Reason == ReplayDivergenceReason.None;

    public ReplayDivergenceReason Reason { get; init; }

    public long? DivergenceTick { get; init; }

    public ulong? ExpectedHash { get; init; }

    public ulong? ActualHash { get; init; }

    public ReplayCheckpointMatch? LastMatchingCheckpoint { get; init; }

    public ReplayInputFrame? ExpectedInput { get; init; }

    public ReplayInputFrame? ActualInput { get; init; }

    public static ReplayComparisonResult ExactMatch(ReplayCheckpointMatch? lastMatchingCheckpoint)
    {
        return new ReplayComparisonResult
        {
            LastMatchingCheckpoint = lastMatchingCheckpoint
        };
    }
}

public static class ReplayComparer
{
    public static ReplayComparisonResult Compare(
        ReplayRecordingSnapshot expected,
        ReplayRecordingSnapshot actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.FormatVersion != ReplayRecordingSnapshot.CurrentFormatVersion ||
            actual.FormatVersion != ReplayRecordingSnapshot.CurrentFormatVersion)
        {
            return Diverged(ReplayDivergenceReason.VersionMismatch, null, null, null);
        }

        var expectedFrames = expected.Frames ?? Array.Empty<ReplayInputFrame>();
        var actualFrames = actual.Frames ?? Array.Empty<ReplayInputFrame>();
        ReplayCheckpointMatch? lastCheckpoint = null;
        long previousExpectedTick = -1;
        long previousExpectedSequence = -1;
        long previousActualTick = -1;
        long previousActualSequence = -1;
        var commonCount = Math.Min(expectedFrames.Count, actualFrames.Count);

        for (var index = 0; index < commonCount; index++)
        {
            var expectedFrame = expectedFrames[index];
            var actualFrame = actualFrames[index];
            if (!IsOrdered(expectedFrame, previousExpectedTick, previousExpectedSequence) ||
                !IsOrdered(actualFrame, previousActualTick, previousActualSequence))
            {
                return Diverged(
                    ReplayDivergenceReason.OrderMismatch,
                    expectedFrame,
                    actualFrame,
                    lastCheckpoint);
            }

            previousExpectedTick = expectedFrame.Tick;
            previousExpectedSequence = expectedFrame.Sequence;
            previousActualTick = actualFrame.Tick;
            previousActualSequence = actualFrame.Sequence;

            var ordering = ComparePosition(expectedFrame, actualFrame);
            if (ordering < 0)
            {
                return Diverged(
                    ReplayDivergenceReason.MissingFrame,
                    expectedFrame,
                    actualFrame,
                    lastCheckpoint);
            }

            if (ordering > 0)
            {
                return Diverged(
                    ReplayDivergenceReason.ExtraFrame,
                    expectedFrame,
                    actualFrame,
                    lastCheckpoint);
            }

            if (expectedFrame.FormatVersion != ReplayInputFrame.CurrentFormatVersion ||
                actualFrame.FormatVersion != ReplayInputFrame.CurrentFormatVersion)
            {
                return Diverged(
                    ReplayDivergenceReason.VersionMismatch,
                    expectedFrame,
                    actualFrame,
                    lastCheckpoint);
            }

            if (expectedFrame.DeltaSeconds != actualFrame.DeltaSeconds ||
                expectedFrame.Command != actualFrame.Command ||
                expectedFrame.ItemUseRequest != actualFrame.ItemUseRequest)
            {
                return Diverged(
                    ReplayDivergenceReason.InputMismatch,
                    expectedFrame,
                    actualFrame,
                    lastCheckpoint);
            }

            if (expectedFrame.CheckpointStateHash.HasValue != actualFrame.CheckpointStateHash.HasValue)
            {
                return Diverged(
                    expectedFrame.CheckpointStateHash.HasValue
                        ? ReplayDivergenceReason.MissingCheckpoint
                        : ReplayDivergenceReason.ExtraCheckpoint,
                    expectedFrame,
                    actualFrame,
                    lastCheckpoint);
            }

            if (expectedFrame.CheckpointStateHash != actualFrame.CheckpointStateHash)
            {
                return Diverged(
                    ReplayDivergenceReason.HashMismatch,
                    expectedFrame,
                    actualFrame,
                    lastCheckpoint);
            }

            if (expectedFrame.CheckpointStateHash is { } stateHash)
            {
                lastCheckpoint = new ReplayCheckpointMatch(
                    expectedFrame.Tick,
                    expectedFrame.Sequence,
                    stateHash);
            }
        }

        if (expectedFrames.Count > commonCount)
        {
            return Diverged(
                ReplayDivergenceReason.MissingFrame,
                expectedFrames[commonCount],
                null,
                lastCheckpoint);
        }

        if (actualFrames.Count > commonCount)
        {
            return Diverged(
                ReplayDivergenceReason.ExtraFrame,
                null,
                actualFrames[commonCount],
                lastCheckpoint);
        }

        return ReplayComparisonResult.ExactMatch(lastCheckpoint);
    }

    private static bool IsOrdered(in ReplayInputFrame frame, long previousTick, long previousSequence)
    {
        return frame.Tick >= 0 && frame.Sequence >= 0 &&
               frame.Tick > previousTick && frame.Sequence > previousSequence;
    }

    private static int ComparePosition(in ReplayInputFrame expected, in ReplayInputFrame actual)
    {
        var tickComparison = expected.Tick.CompareTo(actual.Tick);
        return tickComparison != 0 ? tickComparison : expected.Sequence.CompareTo(actual.Sequence);
    }

    private static ReplayComparisonResult Diverged(
        ReplayDivergenceReason reason,
        ReplayInputFrame? expected,
        ReplayInputFrame? actual,
        ReplayCheckpointMatch? lastCheckpoint)
    {
        return new ReplayComparisonResult
        {
            Reason = reason,
            DivergenceTick = ResolveDivergenceTick(reason, expected, actual),
            ExpectedHash = expected?.CheckpointStateHash,
            ActualHash = actual?.CheckpointStateHash,
            LastMatchingCheckpoint = lastCheckpoint,
            ExpectedInput = expected,
            ActualInput = actual
        };
    }

    private static long? ResolveDivergenceTick(
        ReplayDivergenceReason reason,
        ReplayInputFrame? expected,
        ReplayInputFrame? actual)
    {
        return reason switch
        {
            ReplayDivergenceReason.MissingFrame or ReplayDivergenceReason.MissingCheckpoint =>
                expected?.Tick ?? actual?.Tick,
            ReplayDivergenceReason.ExtraFrame or ReplayDivergenceReason.ExtraCheckpoint =>
                actual?.Tick ?? expected?.Tick,
            _ when expected.HasValue && actual.HasValue =>
                Math.Min(expected.Value.Tick, actual.Value.Tick),
            _ => expected?.Tick ?? actual?.Tick
        };
    }
}
