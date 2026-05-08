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
}
