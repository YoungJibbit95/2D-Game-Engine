using Game.Core.Commands;
using Game.Core.DeveloperTools;
using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.Physics;
using Game.Core.Time;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.CommandTests;

public sealed class AdvancedCommandDispatcherTests
{
    [Fact]
    public void DefaultRegistry_ContainsCompleteDeveloperCommandSurface()
    {
        var registry = CommandRegistry.CreateDefault();
        var expected = new[]
        {
            "give", "remove", "clear", "spawn", "despawn", "tp", "position", "time",
            "godmode", "noclip", "fly", "speed", "chunk", "spawnrate", "debug",
            "performance", "event", "help"
        };

        Assert.All(expected, name => Assert.True(registry.TryGet(name, out _), $"Missing /{name}."));
    }

    [Fact]
    public void InventoryCommands_GiveRemoveAndClearLegacyInventory()
    {
        var content = CommandTestContent.Create();
        var inventory = new Inventory(4, content.Items);
        var context = new CommandContext { Content = content, PlayerInventory = inventory };
        var dispatcher = CreateDispatcher();

        var give = dispatcher.Execute("/give gel 12", context);
        var remove = dispatcher.Execute("/remove gel 5", context);
        var clear = dispatcher.Execute("/clear inventory", context);

        Assert.True(give.IsSuccess);
        Assert.True(remove.IsSuccess);
        Assert.True(clear.IsSuccess);
        Assert.Equal("inventory_cleared", clear.Code);
        Assert.Equal(0, inventory.CountItem("gel"));
        Assert.Contains("7 item(s)", clear.Message);
    }

    [Fact]
    public void RemoveCommand_IncludesFavoriteStacksForDeveloperAction()
    {
        var content = CommandTestContent.Create();
        var inventory = new PlayerInventory(content.Items);
        inventory.AddItem(new ItemStack("gel", 3));
        inventory.Hotbar.SetFavorite(0, favorite: true);

        var result = CreateDispatcher().Execute("/remove gel 3", new CommandContext
        {
            Content = content,
            PlayerLoadoutInventory = inventory
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(0, inventory.CountItem("gel"));
    }

    [Fact]
    public void DespawnCommand_RemovesByInstanceThenByDefinition()
    {
        var content = CommandTestContent.Create();
        var entities = new EntityManager();
        var factory = new EntityFactory(new TileCollisionResolver());
        entities.Add(factory.CreateEnemy(content.Entities.GetById("slime"), Vector2.Zero));
        entities.Add(factory.CreateEnemy(content.Entities.GetById("slime"), Vector2.One));
        var context = new CommandContext { EntityManager = entities };

        var first = CreateDispatcher().Execute("/despawn #1", context);
        var second = CreateDispatcher().Execute("/despawn slime 1", context);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Empty(entities.Entities);
    }

    [Fact]
    public void TeleportAndPositionCommands_ExposeRequestAndCurrentState()
    {
        var dispatcher = CreateDispatcher();

        var teleport = dispatcher.Execute("/tp -12.5 48", new CommandContext());
        var position = dispatcher.Execute("/pos", new CommandContext { PlayerPosition = new Vector2(4, 8) });

        Assert.Equal(CommandResultKind.Request, teleport.Kind);
        Assert.Equal(new Vector2(-12.5f, 48), Assert.IsType<TeleportPlayerIntent>(teleport.Intent).Position);
        Assert.True(position.IsSuccess);
        Assert.Contains("4, 8", position.Message);
    }

    [Theory]
    [InlineData("/godmode on", DeveloperMovementMode.GodMode, DeveloperToggle.On)]
    [InlineData("/noclip off", DeveloperMovementMode.NoClip, DeveloperToggle.Off)]
    [InlineData("/fly", DeveloperMovementMode.Fly, DeveloperToggle.Toggle)]
    public void MovementModeCommands_ReturnTypedIntents(
        string input,
        DeveloperMovementMode expectedMode,
        DeveloperToggle expectedToggle)
    {
        var result = CreateDispatcher().Execute(input, new CommandContext());

        var intent = Assert.IsType<SetDeveloperMovementModeIntent>(result.Intent);
        Assert.Equal(expectedMode, intent.Mode);
        Assert.Equal(expectedToggle, intent.Value);
    }

    [Fact]
    public void SpeedAndSpawnRateCommands_ValidateBoundsAndReturnTypedIntents()
    {
        var dispatcher = CreateDispatcher();

        var speed = dispatcher.Execute("/speed 2.5", new CommandContext());
        var spawnRate = dispatcher.Execute("/spawnrate reset", new CommandContext());
        var invalid = dispatcher.Execute("/speed 100", new CommandContext());

        Assert.Equal(2.5f, Assert.IsType<SetDeveloperSpeedIntent>(speed.Intent).Multiplier);
        Assert.True(Assert.IsType<SetSpawnRateIntent>(spawnRate.Intent).Reset);
        Assert.False(invalid.IsSuccess);
        Assert.Equal("invalid_argument", invalid.Code);
    }

    [Fact]
    public void ChunkInfo_ReturnsMetadataAndReloadUsesPlayerChunkIntent()
    {
        var world = new World(
            64,
            64,
            WorldMetadata.CreateDefault(seed: 17),
            isHorizontallyInfinite: true);
        var chunk = world.GetOrCreateChunk(new ChunkPos(-1, 0));
        chunk.UpdateMetadata(new ChunkMetadata(3, 4, 5, 99));
        var context = new CommandContext
        {
            World = world,
            PlayerPosition = new Vector2(-1, 0)
        };
        var dispatcher = CreateDispatcher();

        var info = dispatcher.Execute("/chunk info -1 0", context);
        var reload = dispatcher.Execute("/chunk reload", context);

        Assert.True(info.IsSuccess);
        Assert.Contains("liquids=3", info.Message);
        Assert.Equal(new ChunkPos(-1, 0), Assert.IsType<ReloadChunkIntent>(reload.Intent).Position);
    }

    [Fact]
    public void DebugPerformanceAndEventCommands_ReturnSpecificRequests()
    {
        var dispatcher = CreateDispatcher();

        var debug = dispatcher.Execute("/debug streaming on", new CommandContext());
        var performance = dispatcher.Execute("/perf capture", new CommandContext());
        var eventResult = dispatcher.Execute("/event watch EntityDiedEvent", new CommandContext());

        var debugIntent = Assert.IsType<SetDebugViewIntent>(debug.Intent);
        Assert.Equal(DebugView.Streaming, debugIntent.View);
        Assert.Equal(DeveloperToggle.On, debugIntent.Value);
        Assert.Equal(PerformanceRequestKind.Capture, Assert.IsType<PerformanceRequestIntent>(performance.Intent).Kind);
        var eventIntent = Assert.IsType<EventDiagnosticsRequestIntent>(eventResult.Intent);
        Assert.Equal(EventDiagnosticsRequestKind.Watch, eventIntent.Kind);
        Assert.Equal("EntityDiedEvent", eventIntent.EventName);
    }

    [Fact]
    public void TypedValidation_PreventsExecutionAndReturnsUsage()
    {
        var worldTime = new WorldTime();

        var result = CreateDispatcher().Execute("/time invalid", new CommandContext { WorldTime = worldTime });

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_argument", result.Code);
        Assert.Equal(0, worldTime.TimeOfDaySeconds);
    }

    [Fact]
    public void ConditionalArguments_RejectIncompleteOrIrrelevantValues()
    {
        var content = CommandTestContent.Create();
        var entities = new EntityManager();
        var factory = new EntityFactory(new TileCollisionResolver());
        var dispatcher = CreateDispatcher();

        var spawn = dispatcher.Execute("/spawn slime 10", new CommandContext
        {
            Content = content,
            EntityManager = entities,
            EntityFactory = factory,
            PlayerPosition = Vector2.Zero
        });
        var time = dispatcher.Execute("/time day 0.2", new CommandContext { WorldTime = new WorldTime() });
        var debug = dispatcher.Execute("/debug world on", new CommandContext());

        Assert.False(spawn.IsSuccess);
        Assert.Empty(entities.Entities);
        Assert.False(time.IsSuccess);
        Assert.False(debug.IsSuccess);
    }

    private static CommandDispatcher CreateDispatcher()
    {
        return new CommandDispatcher(CommandRegistry.CreateDefault());
    }
}
