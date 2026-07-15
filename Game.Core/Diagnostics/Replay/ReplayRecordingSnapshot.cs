namespace Game.Core.Diagnostics.Replay;

public sealed record ReplayRecordingSnapshot
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public int Capacity { get; init; } = ReplayLimits.DefaultFrameCapacity;

    public long DroppedFrameCount { get; init; }

    public IReadOnlyList<ReplayInputFrame> Frames { get; init; } = Array.Empty<ReplayInputFrame>();

    public static void Validate(ReplayRecordingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Replay recording version {snapshot.FormatVersion} is unsupported.");
        }

        if (snapshot.Capacity is <= 0 or > ReplayLimits.MaximumFrameCapacity)
        {
            throw new InvalidDataException(
                $"Replay capacity must be between 1 and {ReplayLimits.MaximumFrameCapacity}.");
        }

        if (snapshot.DroppedFrameCount < 0 || snapshot.Frames is null || snapshot.Frames.Count > snapshot.Capacity)
        {
            throw new InvalidDataException("Replay recording metadata is invalid.");
        }

        long previousTick = -1;
        long previousSequence = -1;
        for (var index = 0; index < snapshot.Frames.Count; index++)
        {
            var frame = snapshot.Frames[index];
            ReplayInputFrame.Validate(frame);
            if (index > 0 && (frame.Tick <= previousTick || frame.Sequence <= previousSequence))
            {
                throw new InvalidDataException(
                    "Replay frames must have strictly increasing ticks and sequences.");
            }

            previousTick = frame.Tick;
            previousSequence = frame.Sequence;
        }
    }
}
