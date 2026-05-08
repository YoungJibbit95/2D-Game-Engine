using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using System.Numerics;
using Xunit;

namespace Game.Tests.EventTests;

public sealed class SystemEventIntegrationTests
{
    [Fact]
    public void PickupSystem_PublishesItemPickedUpEvent()
    {
        var bus = new GameEventBus();
        ItemPickedUpEvent? received = null;
        bus.Subscribe<ItemPickedUpEvent>(gameEvent => received = gameEvent);
        var entities = new EntityManager(spatialCellSize: 16);
        var drop = new DroppedItemEntity(new ItemStack("gel", 1), Vector2.Zero, new TileCollisionResolver());
        entities.Add(drop);
        var inventory = new Inventory(1, CreateItems());

        new ItemPickupSystem().PickupItems(entities, inventory, drop.Bounds, bus);

        Assert.NotNull(received);
        Assert.Equal(drop.Id, received.EntityId);
        Assert.Equal(new ItemStack("gel", 1), received.Stack);
    }

    [Fact]
    public void CombatSystem_PublishesProjectileHitAndEntityDiedEvents()
    {
        var bus = new GameEventBus();
        var hitCount = 0;
        var deathCount = 0;
        bus.Subscribe<ProjectileHitEvent>(_ => hitCount++);
        bus.Subscribe<EntityDiedEvent>(_ => deathCount++);
        var entities = new EntityManager(spatialCellSize: 16);
        entities.Add(new EntityFactory(new TileCollisionResolver()).CreateEnemy(new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 5,
            LootTableId = "slime_basic"
        }, Vector2.Zero));
        entities.Add(new ProjectileEntity("arrow", Vector2.Zero, Vector2.Zero, 5, 0, 0, 5));

        new CombatSystem(new LootRoller(new Random(1)), new TileCollisionResolver())
            .ResolveProjectileHits(entities, CreateLootTables(), bus);

        Assert.Equal(1, hitCount);
        Assert.Equal(1, deathCount);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "gel",
                DisplayName = "Gel",
                Type = ItemType.Material,
                TexturePath = "items/gel",
                MaxStack = 999
            }
        });
    }

    private static LootTableRegistry CreateLootTables()
    {
        return LootTableRegistry.Create(new[]
        {
            new LootTableDefinition
            {
                Id = "slime_basic",
                Entries = Array.Empty<LootEntryDefinition>()
            }
        });
    }
}
