namespace Game.Core.Events;

public sealed class GameEventBus
{
    private readonly Dictionary<Type, Delegate[]> _handlers = new();
    private Action<IGameEvent>[] _anyHandlers = Array.Empty<Action<IGameEvent>>();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var handlers = _handlers.GetValueOrDefault(eventType, Array.Empty<Delegate>());
        var updated = new Delegate[handlers.Length + 1];
        Array.Copy(handlers, updated, handlers.Length);
        updated[^1] = handler;
        _handlers[eventType] = updated;
        return new Subscription(() => Remove(eventType, handler));
    }

    public IDisposable SubscribeAll(Action<IGameEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var updated = new Action<IGameEvent>[_anyHandlers.Length + 1];
        Array.Copy(_anyHandlers, updated, _anyHandlers.Length);
        updated[^1] = handler;
        _anyHandlers = updated;
        return new Subscription(() => RemoveAny(handler));
    }

    public void Publish<TEvent>(TEvent gameEvent)
        where TEvent : IGameEvent
    {
        if (gameEvent is null)
        {
            throw new ArgumentNullException(nameof(gameEvent));
        }

        var anyHandlers = _anyHandlers;
        for (var index = 0; index < anyHandlers.Length; index++)
        {
            anyHandlers[index](gameEvent);
        }

        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            return;
        }

        for (var index = 0; index < handlers.Length; index++)
        {
            ((Action<TEvent>)handlers[index])(gameEvent);
        }
    }

    private void Remove(Type eventType, Delegate handler)
    {
        if (!_handlers.TryGetValue(eventType, out var handlers))
        {
            return;
        }

        var index = Array.IndexOf(handlers, handler);
        if (index < 0)
        {
            return;
        }

        if (handlers.Length == 1)
        {
            _handlers.Remove(eventType);
            return;
        }

        var updated = new Delegate[handlers.Length - 1];
        Array.Copy(handlers, 0, updated, 0, index);
        Array.Copy(handlers, index + 1, updated, index, handlers.Length - index - 1);
        _handlers[eventType] = updated;
    }

    private void RemoveAny(Action<IGameEvent> handler)
    {
        var handlers = _anyHandlers;
        var index = Array.IndexOf(handlers, handler);
        if (index < 0)
        {
            return;
        }

        if (handlers.Length == 1)
        {
            _anyHandlers = Array.Empty<Action<IGameEvent>>();
            return;
        }

        var updated = new Action<IGameEvent>[handlers.Length - 1];
        Array.Copy(handlers, 0, updated, 0, index);
        Array.Copy(handlers, index + 1, updated, index, handlers.Length - index - 1);
        _anyHandlers = updated;
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
