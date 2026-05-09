using Game.Core.Events;

namespace Game.Core.World.Simulation;

public sealed class WorldSimulationEventBridge : IDisposable
{
    private readonly List<IDisposable> _subscriptions = new();
    private bool _disposed;

    private WorldSimulationEventBridge()
    {
    }

    public static WorldSimulationEventBridge Attach(
        GameEventBus events,
        WorldSimulationScheduler scheduler,
        int tilePadding = 2)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(scheduler);

        var bridge = new WorldSimulationEventBridge();
        bridge._subscriptions.Add(events.Subscribe<TileMinedEvent>(gameEvent =>
            scheduler.MarkTileChanged(gameEvent.Position, tilePadding)));
        bridge._subscriptions.Add(events.Subscribe<TilePlacedEvent>(gameEvent =>
            scheduler.MarkTileChanged(gameEvent.Position, tilePadding)));
        return bridge;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        _disposed = true;
    }
}
