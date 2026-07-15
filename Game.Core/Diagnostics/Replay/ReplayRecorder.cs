namespace Game.Core.Diagnostics.Replay;

public sealed class ReplayRecorder
{
    private readonly ReplayInputFrame[] _frames;
    private int _head;
    private int _count;
    private long _droppedFrameCount;

    public ReplayRecorder(int capacity = ReplayLimits.DefaultFrameCapacity)
    {
        if (capacity is <= 0 or > ReplayLimits.MaximumFrameCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                $"Replay capacity must be between 1 and {ReplayLimits.MaximumFrameCapacity}.");
        }

        _frames = new ReplayInputFrame[capacity];
    }

    public int Capacity => _frames.Length;

    public int Count => _count;

    public long DroppedFrameCount => _droppedFrameCount;

    public void Record(in ReplayInputFrame frame)
    {
        ReplayInputFrame.Validate(frame);
        if (_count > 0)
        {
            var newest = _frames[PhysicalIndex(_count - 1)];
            if (frame.Tick <= newest.Tick || frame.Sequence <= newest.Sequence)
            {
                throw new ArgumentException(
                    "Replay frames must have strictly increasing ticks and sequences.",
                    nameof(frame));
            }
        }

        if (_count < _frames.Length)
        {
            _frames[PhysicalIndex(_count)] = frame;
            _count++;
            return;
        }

        _frames[_head] = frame;
        _head = (_head + 1) % _frames.Length;
        _droppedFrameCount++;
    }

    public bool TryGetFrame(int chronologicalIndex, out ReplayInputFrame frame)
    {
        if ((uint)chronologicalIndex >= (uint)_count)
        {
            frame = default;
            return false;
        }

        frame = _frames[PhysicalIndex(chronologicalIndex)];
        return true;
    }

    public ReplayRecordingSnapshot CaptureSnapshot()
    {
        var frames = new ReplayInputFrame[_count];
        for (var index = 0; index < _count; index++)
        {
            frames[index] = _frames[PhysicalIndex(index)];
        }

        return new ReplayRecordingSnapshot
        {
            Capacity = Capacity,
            DroppedFrameCount = _droppedFrameCount,
            Frames = frames
        };
    }

    public void Restore(ReplayRecordingSnapshot snapshot)
    {
        ReplayRecordingSnapshot.Validate(snapshot);
        if (snapshot.Capacity != Capacity)
        {
            throw new InvalidDataException(
                $"Replay snapshot capacity {snapshot.Capacity} does not match recorder capacity {Capacity}.");
        }

        Array.Clear(_frames);
        _head = 0;
        _count = snapshot.Frames.Count;
        _droppedFrameCount = snapshot.DroppedFrameCount;
        for (var index = 0; index < _count; index++)
        {
            _frames[index] = snapshot.Frames[index];
        }
    }

    public static ReplayRecorder FromSnapshot(ReplayRecordingSnapshot snapshot)
    {
        ReplayRecordingSnapshot.Validate(snapshot);
        var recorder = new ReplayRecorder(snapshot.Capacity);
        recorder.Restore(snapshot);
        return recorder;
    }

    private int PhysicalIndex(int chronologicalIndex)
    {
        return (_head + chronologicalIndex) % _frames.Length;
    }
}
