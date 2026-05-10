namespace Game.Core.Events;

public sealed class GameEventJournal : IDisposable
{
    private readonly Queue<RecordedGameEvent> _events = new();
    private readonly IDisposable _subscription;
    private readonly Func<DateTimeOffset> _clock;
    private long _nextSequence;
    private bool _disposed;

    public GameEventJournal(GameEventBus bus, int capacity = 256, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(bus);

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Event journal capacity must be greater than zero.");
        }

        Capacity = capacity;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _subscription = bus.SubscribeAll(Record);
    }

    public int Capacity { get; }

    public int Count => _events.Count;

    public IReadOnlyList<RecordedGameEvent> Events => _events.ToArray();

    public IReadOnlyList<RecordedGameEvent> Drain()
    {
        var events = _events.ToArray();
        _events.Clear();
        return events;
    }

    public void Clear()
    {
        _events.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _subscription.Dispose();
        _disposed = true;
    }

    private void Record(IGameEvent gameEvent)
    {
        _events.Enqueue(new RecordedGameEvent(_nextSequence++, _clock(), gameEvent));
        while (_events.Count > Capacity)
        {
            _events.Dequeue();
        }
    }
}
