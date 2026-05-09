using Game.Core.Biomes;
using Game.Core.Commands;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.Time;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.CommandTests;

public sealed class CommandDispatcherTests
{
    [Fact]
    public void Execute_GiveCommand_AddsItemAndPublishesEvent()
    {
        var content = CreateContent();
        var inventory = new Inventory(3, content.Items);
        var bus = new GameEventBus();
        CommandExecutedEvent? received = null;
        bus.Subscribe<CommandExecutedEvent>(gameEvent => received = gameEvent);

        var result = CreateDispatcher().Execute("/give gel 12", new CommandContext
        {
            Content = content,
            PlayerInventory = inventory,
            Events = bus
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(12, inventory.CountItem("gel"));
        Assert.NotNull(received);
        Assert.Equal("give", received.CommandName);
        Assert.True(received.Success);
    }

    [Fact]
    public void Execute_GiveAlias_UsesRegisteredAlias()
    {
        var content = CreateContent();
        var inventory = new Inventory(3, content.Items);

        var result = CreateDispatcher().Execute("/item gel 2", new CommandContext
        {
            Content = content,
            PlayerInventory = inventory
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, inventory.CountItem("gel"));
    }

    [Fact]
    public void Execute_GiveCommand_CanUsePlayerInventoryModel()
    {
        var content = CreateContent();
        var inventory = new PlayerInventory(content.Items);

        var result = CreateDispatcher().Execute("/give gel 4", new CommandContext
        {
            Content = content,
            PlayerLoadoutInventory = inventory
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(4, inventory.CountItem("gel"));
    }

    [Fact]
    public void Execute_TimeCommand_ChangesWorldTime()
    {
        var worldTime = new WorldTime(dayLengthSeconds: 100);

        var nightResult = CreateDispatcher().Execute("/time night", new CommandContext
        {
            WorldTime = worldTime
        });
        var dayResult = CreateDispatcher().Execute("/time set 0.5", new CommandContext
        {
            WorldTime = worldTime
        });

        Assert.True(nightResult.IsSuccess);
        Assert.True(dayResult.IsSuccess);
        Assert.Equal(0.5, worldTime.NormalizedTimeOfDay, precision: 3);
        Assert.False(worldTime.IsNight);
    }

    [Fact]
    public void Execute_SpawnCommand_AddsEnemyEntity()
    {
        var content = CreateContent();
        var entities = new EntityManager(spatialCellSize: 16);
        var factory = new EntityFactory(new TileCollisionResolver());

        var result = CreateDispatcher().Execute("/spawn slime 32 48", new CommandContext
        {
            Content = content,
            EntityManager = entities,
            EntityFactory = factory
        });

        Assert.True(result.IsSuccess);
        var enemy = Assert.IsType<EnemyEntity>(Assert.Single(entities.Entities));
        Assert.Equal("slime", enemy.DefinitionId);
        Assert.Equal(new Vector2(32, 48), enemy.Body.Position);
    }

    [Fact]
    public void Execute_UnknownCommand_ReturnsFailureAndPublishesEvent()
    {
        var bus = new GameEventBus();
        CommandExecutedEvent? received = null;
        bus.Subscribe<CommandExecutedEvent>(gameEvent => received = gameEvent);

        var result = CreateDispatcher().Execute("/does_not_exist", new CommandContext
        {
            Events = bus
        });

        Assert.False(result.IsSuccess);
        Assert.NotNull(received);
        Assert.Equal("does_not_exist", received.CommandName);
        Assert.False(received.Success);
    }

    [Fact]
    public void Execute_DebugWorld_ReturnsSnapshotSummary()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 44) with { Name = "Test World" });
        world.SetTile(1, 1, KnownTileIds.Dirt);
        var entities = new EntityManager(spatialCellSize: 16);

        var result = CreateDispatcher().Execute("/debug world", new CommandContext
        {
            World = world,
            EntityManager = entities,
            WorldTime = new WorldTime()
        });

        Assert.True(result.IsSuccess);
        Assert.Contains("Test World", result.Message);
        Assert.Contains("seed=44", result.Message);
        Assert.Contains("chunks=1", result.Message);
    }

    private static CommandDispatcher CreateDispatcher()
    {
        return new CommandDispatcher(CommandRegistry.CreateDefault());
    }

    private static GameContentDatabase CreateContent()
    {
        return new GameContentDatabase(
            TileRegistry.Create(Array.Empty<TileDefinition>()),
            ItemRegistry.Create(new[]
            {
                new ItemDefinition
                {
                    Id = "gel",
                    DisplayName = "Gel",
                    Type = ItemType.Material,
                    TexturePath = "items/gel",
                    MaxStack = 999
                }
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(Array.Empty<BiomeDefinition>()),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(new[]
            {
                new EntityDefinition
                {
                    Id = "slime",
                    DisplayName = "Slime",
                    TexturePath = "entities/slime",
                    MaxHealth = 5,
                    Width = 16,
                    Height = 16,
                    AiBehavior = "slime"
                }
            }),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
    }
}
