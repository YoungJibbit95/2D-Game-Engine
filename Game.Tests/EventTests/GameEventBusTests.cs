using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.World;
using Xunit;

namespace Game.Tests.EventTests;

public sealed class GameEventBusTests
{
    [Fact]
    public void Publish_DeliversEventToSubscriber()
    {
        var bus = new GameEventBus();
        TileMinedEvent? received = null;
        bus.Subscribe<TileMinedEvent>(gameEvent => received = gameEvent);

        bus.Publish(new TileMinedEvent(new TilePos(1, 2), KnownTileIds.Dirt, new ItemStack("dirt_block", 1)));

        Assert.NotNull(received);
        Assert.Equal(new TilePos(1, 2), received.Position);
    }

    [Fact]
    public void Subscription_DisposeStopsReceivingEvents()
    {
        var bus = new GameEventBus();
        var count = 0;
        var subscription = bus.Subscribe<ItemPickedUpEvent>(_ => count++);

        subscription.Dispose();
        bus.Publish(new ItemPickedUpEvent(1, new ItemStack("gel", 1)));

        Assert.Equal(0, count);
    }

    [Fact]
    public void SubscribeAll_ReceivesDifferentEventTypes()
    {
        var bus = new GameEventBus();
        var received = new List<IGameEvent>();

        bus.SubscribeAll(received.Add);
        bus.Publish(new TileMinedEvent(new TilePos(1, 2), KnownTileIds.Dirt, new ItemStack("dirt_block", 1)));
        bus.Publish(new ChunkGeneratedEvent(new ChunkPos(-1, 0)));

        Assert.IsType<TileMinedEvent>(received[0]);
        Assert.IsType<ChunkGeneratedEvent>(received[1]);
    }

    [Fact]
    public void SubscribeAll_DisposedSubscriptionStopsReceivingEvents()
    {
        var bus = new GameEventBus();
        var count = 0;
        var subscription = bus.SubscribeAll(_ => count++);

        subscription.Dispose();
        bus.Publish(new ChunkGeneratedEvent(new ChunkPos(0, 0)));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Publish_UsesStableSnapshotWhenSubscriptionChangesDuringDispatch()
    {
        var bus = new GameEventBus();
        var calls = new List<string>();
        IDisposable? second = null;
        bus.Subscribe<ChunkGeneratedEvent>(_ =>
        {
            calls.Add("first");
            second?.Dispose();
            bus.Subscribe<ChunkGeneratedEvent>(_ => calls.Add("late"));
        });
        second = bus.Subscribe<ChunkGeneratedEvent>(_ => calls.Add("second"));

        bus.Publish(new ChunkGeneratedEvent(new ChunkPos(0, 0)));
        Assert.Equal(["first", "second"], calls);

        calls.Clear();
        bus.Publish(new ChunkGeneratedEvent(new ChunkPos(1, 0)));
        Assert.Equal(["first", "late"], calls);
    }

    [Fact]
    public void Publish_SteadyStateDoesNotAllocate()
    {
        var bus = new GameEventBus();
        var received = 0;
        bus.Subscribe<ChunkGeneratedEvent>(_ => received++);
        var gameEvent = new ChunkGeneratedEvent(new ChunkPos(-4, 2));
        for (var index = 0; index < 32; index++)
        {
            bus.Publish(gameEvent);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            bus.Publish(gameEvent);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(1_032, received);
    }

    [Fact]
    public void Publish_ValueEventSteadyStateDoesNotBox()
    {
        var bus = new GameEventBus();
        var received = 0;
        bus.Subscribe<ValueGameEvent>(gameEvent => received += gameEvent.Amount);
        var gameEvent = new ValueGameEvent(1);
        for (var index = 0; index < 32; index++)
        {
            bus.Publish(gameEvent);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            bus.Publish(gameEvent);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1_032, received);
    }

    [Fact]
    public void GameEventJournal_RecordsEventsWithCapacityAndDrain()
    {
        var bus = new GameEventBus();
        var now = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        using var journal = new GameEventJournal(bus, capacity: 2, clock: () => now);

        bus.Publish(new ChunkGeneratedEvent(new ChunkPos(0, 0)));
        bus.Publish(new ChunkGeneratedEvent(new ChunkPos(1, 0)));
        bus.Publish(new ChunkGeneratedEvent(new ChunkPos(2, 0)));

        Assert.Equal(2, journal.Count);
        Assert.Equal(new ChunkPos(1, 0), Assert.IsType<ChunkGeneratedEvent>(journal.Events[0].Event).Position);
        Assert.Equal(new ChunkPos(2, 0), Assert.IsType<ChunkGeneratedEvent>(journal.Events[1].Event).Position);
        Assert.Equal(1, journal.Events[0].Sequence);
        Assert.Equal(now, journal.Events[0].RecordedAtUtc);

        var drained = journal.Drain();

        Assert.Equal(2, drained.Count);
        Assert.Equal(0, journal.Count);
    }

    private readonly record struct ValueGameEvent(int Amount) : IGameEvent;
}
