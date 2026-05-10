namespace Game.Core.Events;

public sealed class GameEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly List<Action<IGameEvent>> _anyHandlers = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlers))
        {
            handlers = new List<Delegate>();
            _handlers.Add(eventType, handlers);
        }

        handlers.Add(handler);
        return new Subscription(() => handlers.Remove(handler));
    }

    public IDisposable SubscribeAll(Action<IGameEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _anyHandlers.Add(handler);
        return new Subscription(() => _anyHandlers.Remove(handler));
    }

    public void Publish<TEvent>(TEvent gameEvent)
        where TEvent : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        foreach (var handler in _anyHandlers.ToArray())
        {
            handler(gameEvent);
        }

        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            return;
        }

        foreach (var handler in handlers.OfType<Action<TEvent>>().ToArray())
        {
            handler(gameEvent);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        private bool _isDisposed;

        public Subscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _dispose();
            _isDisposed = true;
        }
    }
}
