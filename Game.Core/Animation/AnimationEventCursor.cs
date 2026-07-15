namespace Game.Core.Animation;

public readonly record struct AnimationEventOccurrence(
    long Sequence,
    string ClipId,
    string EventId,
    int TimelineTick,
    string? Payload);

public sealed class AnimationEventCursor
{
    private readonly List<AnimationEventOccurrence> _pending = new();
    private long _nextSequence;

    public int PendingCount => _pending.Count;

    public void CopyPendingTo(ICollection<AnimationEventOccurrence> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        for (var index = 0; index < _pending.Count; index++)
        {
            destination.Add(_pending[index]);
        }
    }

    public AnimationEventOccurrence[] ConsumeAll()
    {
        if (_pending.Count == 0)
        {
            return Array.Empty<AnimationEventOccurrence>();
        }

        var result = _pending.ToArray();
        _pending.Clear();
        return result;
    }

    public void Clear()
    {
        _pending.Clear();
    }

    internal void Append(string clipId, AnimationEventMarker marker)
    {
        _pending.Add(new AnimationEventOccurrence(
            _nextSequence++,
            clipId,
            marker.Id,
            marker.Tick,
            marker.Payload));
    }
}
