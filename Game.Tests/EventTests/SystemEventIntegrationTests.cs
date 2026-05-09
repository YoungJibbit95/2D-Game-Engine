using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Core.World.Simulation;
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

    [Fact]
    public void BuildingSystem_PublishesTilePlacedEvent()
    {
        var bus = new GameEventBus();
        TilePlacedEvent? received = null;
        bus.Subscribe<TilePlacedEvent>(gameEvent => received = gameEvent);
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
        var inventory = new Inventory(2, CreateBuildItems());
        inventory.AddItem(new ItemStack("dirt_block", 1));

        var placed = new BuildingSystem().PlaceTile(
            world,
            inventory,
            CreateBuildItems(),
            CreateBuildTiles(),
            new TilePos(2, 2),
            "dirt_block",
            Vector2.Zero,
            128,
            new RectI(1000, 1000, 1, 1),
            bus);

        Assert.True(placed);
        Assert.NotNull(received);
        Assert.Equal(new TilePos(2, 2), received.Position);
        Assert.Equal(KnownTileIds.Dirt, received.TileId);
        Assert.Equal("dirt_block", received.ItemId);
    }

    [Fact]
    public void WorldSimulationEventBridge_MarksRegionsFromTileEvents()
    {
        var bus = new GameEventBus();
        var scheduler = new WorldSimulationScheduler();
        using var bridge = WorldSimulationEventBridge.Attach(bus, scheduler, tilePadding: 1);

        bus.Publish(new TileMinedEvent(new TilePos(4, 4), KnownTileIds.Dirt, ItemStack.Empty));
        bus.Publish(new TilePlacedEvent(new TilePos(6, 4), KnownTileIds.Stone, "stone_block"));

        Assert.True(scheduler.PendingLiquidRegionCount > 0);
        Assert.True(scheduler.PendingRenderRegionCount > 0);
        Assert.True(scheduler.PendingLightRegionCount > 0);
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

    private static ItemRegistry CreateBuildItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt_block",
                MaxStack = 999,
                PlacesTileId = "dirt"
            }
        });
    }

    private static TileRegistry CreateBuildTiles()
    {
        return TileRegistry.Create(new[]
        {
            new TileDefinition
            {
                NumericId = KnownTileIds.Dirt,
                Id = "dirt",
                DisplayName = "Dirt",
                TexturePath = "tiles/dirt",
                Solid = true,
                BlocksLight = true,
                Hardness = 1
            }
        });
    }
}
