using Game.Core.Entities;
using Game.Core.Runtime;

namespace Game.Core.Diagnostics.Replay;

public sealed record ReplayCaptureOptions
{
    public static ReplayCaptureOptions Default { get; } = new();

    public int FrameCapacity { get; init; } = ReplayLimits.DefaultFrameCapacity;

    public int CheckpointIntervalTicks { get; init; } = 120;

    internal void Validate()
    {
        if (FrameCapacity is <= 0 or > ReplayLimits.MaximumFrameCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FrameCapacity),
                $"Replay capacity must be between 1 and {ReplayLimits.MaximumFrameCapacity}.");
        }

        if (CheckpointIntervalTicks is < 0 or > 60 * 60 * 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CheckpointIntervalTicks),
                "Replay checkpoint interval must be zero or at most ten hours at 60 ticks per second.");
        }
    }
}

public sealed class ReplayCaptureSession
{
    private readonly ReplayRecorder _recorder;
    private readonly int _checkpointIntervalTicks;
    private long _sequence;

    public ReplayCaptureSession(ReplayCaptureOptions? options = null)
    {
        options ??= ReplayCaptureOptions.Default;
        options.Validate();
        _recorder = new ReplayRecorder(options.FrameCapacity);
        _checkpointIntervalTicks = options.CheckpointIntervalTicks;
    }

    public int Count => _recorder.Count;

    public long DroppedFrameCount => _recorder.DroppedFrameCount;

    public bool ShouldCaptureCheckpoint(long tick)
    {
        return _checkpointIntervalTicks > 0 && tick > 0 && tick % _checkpointIntervalTicks == 0;
    }

    public void Record(
        long tick,
        in PlayerCommand command,
        in PlayerItemUseRequest itemUseRequest,
        float deltaSeconds,
        ulong? checkpointStateHash)
    {
        var frame = ReplayInputFrame.Create(
            tick,
            ++_sequence,
            command,
            itemUseRequest,
            checkpointStateHash,
            deltaSeconds);
        _recorder.Record(frame);
    }

    public ReplayRecordingSnapshot CaptureSnapshot() => _recorder.CaptureSnapshot();
}
