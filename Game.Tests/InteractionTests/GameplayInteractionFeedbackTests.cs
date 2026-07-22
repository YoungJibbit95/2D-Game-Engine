using Game.Core.Events;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.InteractionTests;

public sealed class GameplayInteractionFeedbackTests
{
    [Fact]
    public void MiningSystem_ReturnsContinuousProgressAndThrottlesProgressEvents()
    {
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);
        var events = new GameEventBus();
        var startedEvents = 0;
        var progressEvents = 0;
        var completedEvents = 0;
        events.Subscribe<MiningStartedEvent>(_ => startedEvents++);
        events.Subscribe<MiningProgressEvent>(_ => progressEvents++);
        events.Subscribe<MiningCompletedEvent>(_ => completedEvents++);
        var mining = new MiningSystem();
        MiningResult first = default;

        for (var index = 0; index < 5; index++)
        {
            var result = mining.Update(
                world,
                CreateTiles(),
                new TilePos(2, 2),
                new Vector2(40, 40),
                96,
                10,
                0.02f,
                events);
            if (index == 0)
            {
                first = result;
            }
        }

        Assert.True(first.Started);
        Assert.Equal(new TilePos(2, 2), first.TilePosition);
        Assert.Equal(KnownTileIds.Dirt, first.TargetTileId);
        Assert.InRange(first.Progress, 0.070f, 0.071f);
        Assert.Equal(1, startedEvents);
        Assert.Equal(5, progressEvents);

        var completed = mining.Update(
            world,
            CreateTiles(),
            new TilePos(2, 2),
            new Vector2(40, 40),
            96,
            10,
            2f,
            events);

        Assert.True(completed.Completed);
        Assert.Equal(MiningActionStatus.Completed, completed.Status);
        Assert.Equal(1f, completed.Progress);
        Assert.Equal(1, completedEvents);
    }

    [Fact]
    public void MiningSystem_BlockedReasonAndEventAreDeduplicatedUntilStateChanges()
    {
        var world = CreateWorld();
        world.SetTile(7, 7, KnownTileIds.Dirt);
        var events = new GameEventBus();
        var blockedEvents = 0;
        events.Subscribe<MiningBlockedEvent>(_ => blockedEvents++);
        var mining = new MiningSystem();
        var target = new TilePos(7, 7);

        var first = mining.Update(world, CreateTiles(), target, Vector2.Zero, 16, 10, 0.1f, events);
        var repeated = mining.Update(world, CreateTiles(), target, Vector2.Zero, 16, 10, 0.1f, events);

        Assert.True(first.Blocked);
        Assert.Equal(GameplayActionFailureReason.OutOfReach, first.FailureReason);
        Assert.Equal(first, repeated);
        Assert.Equal(1, blockedEvents);

        var valid = mining.Update(world, CreateTiles(), target, new Vector2(120, 120), 96, 10, 0.1f, events);
        var blockedAgain = mining.Update(world, CreateTiles(), target, Vector2.Zero, 16, 10, 0.1f, events);

        Assert.True(valid.InProgress);
        Assert.Equal(GameplayActionFailureReason.OutOfReach, blockedAgain.FailureReason);
        Assert.Equal(2, blockedEvents);
    }

    [Fact]
    public void MiningSystem_ReportsInsufficientToolPower()
    {
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);

        var result = new MiningSystem().Update(
            world,
            CreateTiles(requiredPower: 20),
            new TilePos(2, 2),
            new Vector2(40, 40),
            96,
            10,
            1f);

        Assert.True(result.Blocked);
        Assert.Equal(GameplayActionFailureReason.InsufficientToolPower, result.FailureReason);
    }

    [Fact]
    public void BuildingSystem_DetailResultDistinguishesOccupiedAndOutOfReach()
    {
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);
        var system = new BuildingSystem();
        var items = CreateItems();
        var tiles = CreateTiles();

        var occupied = system.EvaluatePlacement(
            world,
            items,
            tiles,
            new TilePos(2, 2),
            new ItemStack("dirt_block", 1),
            new Vector2(40, 40),
            96,
            new RectI(1000, 1000, 1, 1));
        var outOfReach = system.EvaluatePlacement(
            world,
            items,
            tiles,
            new TilePos(7, 7),
            new ItemStack("dirt_block", 1),
            Vector2.Zero,
            16,
            new RectI(1000, 1000, 1, 1));

        Assert.Equal(GameplayActionFailureReason.Occupied, occupied.FailureReason);
        Assert.Equal(GameplayActionFailureReason.OutOfReach, outOfReach.FailureReason);
    }

    private static World CreateWorld()
    {
        return new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt_block",
                MaxStack = 99,
                PlacesTileId = "dirt"
            }
        });
    }

    private static TileRegistry CreateTiles(int requiredPower = 0)
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
                Hardness = 2,
                MiningPowerRequired = requiredPower,
                DropItemId = "dirt_block"
            }
        });
    }
}
