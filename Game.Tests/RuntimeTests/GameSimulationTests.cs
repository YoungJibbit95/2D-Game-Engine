using Game.Core.Actions;
using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Crafting;
using Game.Core.Data;
using Game.Core.Diagnostics.Replay;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Equipment;
using Game.Core.Effects;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Runtime;
using Game.Core.Spawning;
using Game.Core.Tiles;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Simulation;
using Game.Core.WorldEvents;
using System.Numerics;
using Xunit;

namespace Game.Tests.RuntimeTests;

public sealed class GameSimulationTests
{
    [Fact]
    public void Tick_AdvancesCoreSystemsTogether()
    {
        var content = CreateContent();
        var world = new World(32, 16, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 8, KnownTileIds.Dirt);
        }

        var player = new PlayerEntity(new Vector2(64, 96), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        var entities = new EntityManager(spatialCellSize: 16);
        entities.Add(new DroppedItemEntity(new ItemStack("gel", 2), player.Body.Position, new TileCollisionResolver()));
        entities.Add(new EntityFactory(new TileCollisionResolver()).CreateEnemy(CreateSlimeDefinition(), player.Body.Position));

        var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            entities,
            combat: new CombatSystem(new LootRoller(new Random(1)), new TileCollisionResolver()),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.Equal(1, result.ContactDamage.ContactHits);
        Assert.Equal(90, player.Health);
        Assert.Equal(1, result.PickedUpItems);
        Assert.Equal(2, inventory.CountItem("gel"));
    }

    [Fact]
    public void Tick_AdvancesScheduledWorldSimulation()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(5, 5, TileInstance.Liquid(255));
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            worldSimulationOptions: new WorldSimulationOptions(LiquidStepIntervalSeconds: 0));

        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.True(result.WorldSimulation.Liquids.MovedLiquid > 0);
        Assert.False(world.GetTile(5, 5).HasLiquid);
        Assert.True(world.GetTile(5, 6).HasLiquid);
        Assert.True(world.GetOrCreateChunk(new ChunkPos(0, 0)).Metadata.ActiveLiquidTiles > 0);
    }

    [Fact]
    public void Tick_ProcessesWorldSimulationRegionsMarkedByEvents()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(5, 5, TileInstance.Liquid(255));
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        var events = new GameEventBus();
        var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            events: events,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            worldSimulationOptions: new WorldSimulationOptions(LiquidStepIntervalSeconds: 0, SeedExistingLiquids: false));

        events.Publish(new TileMinedEvent(new TilePos(5, 5), KnownTileIds.Dirt, ItemStack.Empty));
        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.True(result.WorldSimulation.Liquids.MovedLiquid > 0);
        Assert.True(result.WorldSimulation.LiquidRegionsProcessed > 0);
    }

    [Fact]
    public void Tick_ReportsStableOrderedPhases()
    {
        using var simulation = CreateSimulation();

        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.True(result.DidTick);
        Assert.Equal(1, result.TickNumber);
        Assert.Equal(
            new[]
            {
                GameSimulationPhase.PlayerCommand,
                GameSimulationPhase.WorldTimeAndFarming,
                GameSimulationPhase.LivingWorld,
                GameSimulationPhase.Player,
                GameSimulationPhase.PlayerItemUse,
                GameSimulationPhase.Entities,
                GameSimulationPhase.EntityAttacks,
                GameSimulationPhase.Combat,
                GameSimulationPhase.EntityDeaths,
                GameSimulationPhase.PickupMagnetism,
                GameSimulationPhase.Pickups,
                GameSimulationPhase.Spawning,
                GameSimulationPhase.Respawn,
                GameSimulationPhase.WorldSimulation,
                GameSimulationPhase.Lighting,
                GameSimulationPhase.FrameSnapshot
            },
            result.ObservedPhases);
        Assert.Same(result.Snapshot, simulation.LatestSnapshot);
    }

    [Fact]
    public void Constructor_RebasesRestoredWorldEventsToPersistedWorldClock()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var livingWorld = LivingWorldRuntime.CreateDefault(
            world.Metadata.Seed,
            world.HeightTiles,
            surfaceBaseY: 6,
            biomes: content.Biomes,
            worldEvents: content.WorldEvents);
        livingWorld.RestoreWorldEvents(new WorldEventRuntimeSnapshot
        {
            LastAdvancedTick = 900,
            RegionIndex = 0,
            BiomeId = "forest",
            Status = WorldEventRuntimeStatus.Inactive
        });

        using var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            livingWorld: livingWorld);

        Assert.Equal(0, simulation.TickNumber);
        Assert.Equal(0, simulation.LatestSnapshot.TickNumber);
        Assert.Equal(0, simulation.LivingWorld.WorldEventSnapshot!.LastAdvancedTick);
        Assert.Equal(1, simulation.Tick(PlayerCommand.None, 0.016f).TickNumber);
    }

    [Fact]
    public void PhaseTelemetry_DisabledPathDoesNotCollectSamples()
    {
        using var simulation = CreateSimulation();

        simulation.Tick(PlayerCommand.None, 0.016f);
        var telemetry = simulation.PhaseTelemetry.CaptureSnapshot();

        Assert.False(telemetry.IsEnabled);
        Assert.All(telemetry.Measurements, measurement => Assert.Equal(0, measurement.Samples));
    }

    [Fact]
    public void PhaseTelemetry_CollectsEveryAuthoritativePhaseAndCanReset()
    {
        using var simulation = CreateSimulation();
        simulation.ConfigureOptions(simulation.Options with { EnablePhaseTelemetry = true });

        simulation.Tick(PlayerCommand.None, 0.016f);
        var telemetry = simulation.PhaseTelemetry.CaptureSnapshot();

        Assert.True(telemetry.IsEnabled);
        Assert.Equal(Enum.GetValues<GameSimulationPhase>().Length, telemetry.Measurements.Count);
        Assert.All(telemetry.Measurements, measurement =>
        {
            Assert.Equal(1, measurement.Samples);
            Assert.True(measurement.LastElapsedTicks >= 0);
            Assert.True(measurement.LastAllocatedBytes >= 0);
            Assert.True(telemetry.TryGet(measurement.Phase, out var resolved));
            Assert.Equal(measurement, resolved);
        });

        simulation.PhaseTelemetry.Reset();
        Assert.All(
            simulation.PhaseTelemetry.CaptureSnapshot().Measurements,
            measurement => Assert.Equal(0, measurement.Samples));
    }

    [Fact]
    public void Tick_UpdatesPlayerGuardFromRendererNeutralCommandAndSnapshot()
    {
        using var simulation = CreateSimulation();

        var guarded = simulation.Tick(
            new PlayerCommand(0, false, WantsGuard: true, GuardFacing: Vector2.UnitX),
            0.05f);

        Assert.True(simulation.PlayerGuard.IsGuarding);
        Assert.True(guarded.Snapshot.Player.IsGuarding);
        Assert.False(guarded.Snapshot.Player.IsGuardBroken);
        Assert.Equal(
            simulation.PlayerGuard.Definition.MaxStamina,
            guarded.Snapshot.Player.MaxGuardStamina);

        var released = simulation.Tick(PlayerCommand.None, 0.05f);
        Assert.False(simulation.PlayerGuard.IsGuarding);
        Assert.False(released.Snapshot.Player.IsGuarding);
    }

    [Fact]
    public void Tick_RoutesEnemyContactThroughAuthoritativeGuardResolution()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var entities = new EntityManager();
        entities.Add(new EnemyEntity(
            "guard_test_enemy",
            new Vector2(27, 32),
            new Vector2(16, 16),
            new HealthComponent(10),
            NullAiBehavior.Instance,
            new TileCollisionResolver(),
            contactDamage: 20,
            contactKnockback: 0));
        var guard = new GuardRuntimeState(new GuardDefinition { ParryWindowSeconds = 0 });
        using var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            playerGuard: guard);

        var result = simulation.Tick(
            new PlayerCommand(0, false, WantsGuard: true, GuardFacing: -Vector2.UnitX),
            0.05f);

        Assert.True(result.ContactDamage.Blocked);
        Assert.Equal(5, result.ContactDamage.DamageApplied);
        Assert.Equal(15, result.ContactDamage.DamagePrevented);
        Assert.Equal(95, player.Health);
        Assert.True(result.Snapshot.Player.IsGuarding);
        Assert.True(result.Snapshot.Player.GuardStamina < result.Snapshot.Player.MaxGuardStamina);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.1f)]
    public void Tick_NonPositiveDeltaDoesNotAdvanceOrObservePhases(float deltaSeconds)
    {
        using var simulation = CreateSimulation();
        var initialSnapshot = simulation.LatestSnapshot;
        var initialPosition = simulation.Player.Body.Position;

        var result = simulation.Tick(new PlayerCommand(1, true), deltaSeconds);

        Assert.False(result.DidTick);
        Assert.Equal(0, result.TickNumber);
        Assert.Empty(result.ObservedPhases);
        Assert.Same(initialSnapshot, result.Snapshot);
        Assert.Equal(initialPosition, simulation.Player.Body.Position);
        Assert.Equal(1, simulation.Time.Day);
        Assert.Equal(0, simulation.Time.TimeOfDaySeconds);
    }

    [Fact]
    public void Tick_SnapshotDoesNotRetainMutableRuntimeState()
    {
        var content = CreateContent();
        var world = new World(32, 16, WorldMetadata.CreateDefault(seed: 1));
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 3));
        var entities = new EntityManager();
        var droppedItem = new DroppedItemEntity(new ItemStack("gel", 2), new Vector2(400, 32), new TileCollisionResolver());
        entities.Add(droppedItem);
        using var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            player,
            inventory,
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        var snapshot = simulation.Tick(PlayerCommand.None, 0.016f).Snapshot;
        var playerPosition = snapshot.Player.Position;
        var entityPosition = snapshot.Entities[0].Position;
        var worldTime = snapshot.WorldTime.TimeOfDaySeconds;

        player.Body.Position = new Vector2(900, 900);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 99));
        droppedItem.SetStack(new ItemStack("gel", 1));
        simulation.Time.Update(10);

        Assert.Equal(playerPosition, snapshot.Player.Position);
        Assert.Equal(new ItemStack("gel", 3), snapshot.Player.Hotbar[0].Stack);
        Assert.Equal(entityPosition, snapshot.Entities[0].Position);
        Assert.Equal(new ItemStack("gel", 2), snapshot.Entities[0].ItemStack);
        Assert.Equal(worldTime, snapshot.WorldTime.TimeOfDaySeconds);
        Assert.IsType<ImmutableSnapshotList<EntityFrameSnapshot>>(snapshot.Entities);
    }

    [Fact]
    public void Tick_SnapshotStorageSharesOnlyUnchangedImmutableSegments()
    {
        var content = CreateContent();
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 3));
        var plots = new FarmPlotManager();
        var plot = plots.GetOrCreatePlot(new TilePos(2, 2));
        plot.IsTilled = true;
        var entities = new EntityManager();
        var entity = new SnapshotProbeEntity(new Vector2(48, 32));
        entities.Add(entity);
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            inventory,
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            farmPlots: plots);

        var first = simulation.LatestSnapshot;
        var unchanged = simulation.Tick(PlayerCommand.None, 1f / 60f).Snapshot;

        Assert.Same(first.Player.Hotbar, unchanged.Player.Hotbar);
        Assert.Same(first.Entities, unchanged.Entities);
        Assert.Same(first.FarmPlots, unchanged.FarmPlots);

        var firstPosition = first.Entities[0].Position;
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 7));
        plot.IsWatered = true;
        entity.MoveTo(new Vector2(80, 32));
        var changed = simulation.Tick(PlayerCommand.None, 1f / 60f).Snapshot;

        Assert.NotSame(first.Player.Hotbar, changed.Player.Hotbar);
        Assert.NotSame(first.Entities, changed.Entities);
        Assert.NotSame(first.FarmPlots, changed.FarmPlots);
        Assert.Equal(new ItemStack("gel", 3), first.Player.Hotbar[0].Stack);
        Assert.False(first.FarmPlots[0].IsWatered);
        Assert.Equal(firstPosition, first.Entities[0].Position);
        Assert.Equal(new ItemStack("gel", 7), changed.Player.Hotbar[0].Stack);
        Assert.True(changed.FarmPlots[0].IsWatered);
        Assert.Equal(new Vector2(80, 32), changed.Entities[0].Position);
    }

    [Fact]
    public void Tick_AdvancesFarmPlotsAtEveryDayBoundary()
    {
        var content = CreateContent();
        var plots = new FarmPlotManager();
        var plot = plots.GetOrCreatePlot(new TilePos(2, 2));
        plot.IsTilled = true;
        plot.IsWatered = true;
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            time: new WorldTime(dayLengthSeconds: 0.01),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            farmPlots: plots);

        var result = simulation.Tick(PlayerCommand.None, 0.021f);

        Assert.Equal(2, result.DaysAdvanced);
        Assert.Equal(1, result.Farming.ClearedWateredPlots);
        Assert.False(plot.IsWatered);
        Assert.Equal(3, result.Snapshot.WorldTime.Day);
        Assert.Equal(1, result.Snapshot.Hud.FarmPlots);
        Assert.Single(result.Snapshot.FarmPlots);
        Assert.True(result.Snapshot.FarmPlots[0].IsTilled);
    }

    [Fact]
    public void Tick_ProcessesSelectedItemUseWithSimulationFarmState()
    {
        var content = CreateContent();
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("watering_can", 1));
        var plots = new FarmPlotManager();
        plots.GetOrCreatePlot(new TilePos(2, 2)).IsTilled = true;
        using var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            inventory,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            farmPlots: plots);

        var result = simulation.Tick(
            PlayerCommand.None,
            0.016f,
            new PlayerItemUseRequest(true, new TilePos(2, 2), new Vector2(40, 40)));

        Assert.Equal(PlayerItemUseKind.Water, result.ItemUse.Kind);
        Assert.True(result.ItemUse.Success);
        Assert.True(plots.TryGetPlot(new TilePos(2, 2), out var plot));
        Assert.True(plot.IsWatered);
    }

    [Fact]
    public void Tick_RoutesSuccessfulPlayerActionToWorldEventExactlyOnce()
    {
        var eventDefinition = new WorldEventDefinition
        {
            Id = "harvest_moon",
            ChancePerWindow = 0f,
            PlayerActionTriggers = [WorldEventPlayerActionKind.Farm],
            PlayerActionTriggerChance = 1f,
            MinDurationTicks = 60,
            MaxDurationTicks = 60
        };
        var content = CreateContent() with
        {
            WorldEvents = WorldEventDefinitionRegistry.Create([eventDefinition])
        };
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var inventory = new PlayerInventory(content.Items);
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("watering_can", 1));
        var plots = new FarmPlotManager();
        plots.GetOrCreatePlot(new TilePos(2, 2)).IsTilled = true;
        var events = new GameEventBus();
        var evaluations = new List<WorldEventPlayerActionEvaluatedEvent>();
        var activations = new List<WorldEventActivatedEvent>();
        events.Subscribe<WorldEventPlayerActionEvaluatedEvent>(evaluations.Add);
        events.Subscribe<WorldEventActivatedEvent>(activations.Add);
        var livingWorld = LivingWorldRuntime.CreateDefault(
            world.Metadata.Seed,
            world.HeightTiles,
            surfaceBaseY: 6,
            biomes: content.Biomes,
            worldEvents: content.WorldEvents);
        using var simulation = new GameSimulation(
            content,
            world,
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            inventory,
            events: events,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            farmPlots: plots,
            livingWorld: livingWorld);
        var request = new PlayerItemUseRequest(true, new TilePos(2, 2), new Vector2(40, 40));

        var succeeded = simulation.Tick(PlayerCommand.None, 0.016f, request);
        var blocked = simulation.Tick(PlayerCommand.None, 0.016f, request);

        Assert.True(succeeded.ItemUse.Success);
        Assert.True(blocked.ItemUse.Blocked);
        var evaluated = Assert.Single(evaluations);
        Assert.Equal(1, evaluated.Sequence);
        Assert.True(evaluated.Activated);
        Assert.Equal("harvest_moon", evaluated.EventId);
        var activated = Assert.Single(activations);
        Assert.Equal(WorldEventActivationSource.PlayerAction, activated.Source);
        Assert.Equal(WorldEventPlayerActionKind.Farm, activated.TriggerAction);
        Assert.Equal(1, simulation.LivingWorld.LastProcessedPlayerActionSequence);
    }

    [Fact]
    public void Tick_AcceleratesDroppedItemsWithConfiguredMagnetBeforePickup()
    {
        var content = CreateContent();
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var entities = new EntityManager(spatialCellSize: 16);
        var droppedItem = new DroppedItemEntity(new ItemStack("gel", 1), new Vector2(100, 32), new TileCollisionResolver());
        entities.Add(droppedItem);
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            player,
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            options: new GameSimulationOptions
            {
                PickupRadiusPixels = 1,
                ItemMagnetRadiusPixels = 92,
                ItemMagnetStrength = 480
            });

        var result = simulation.Tick(PlayerCommand.None, 1f / 60f);

        Assert.Equal(0, result.PickedUpItems);
        Assert.True(droppedItem.Body.Velocity.X < 0);
        Assert.Equal(droppedItem.Body.Velocity, result.Snapshot.Entities[0].Velocity);
        Assert.Equal("gel", result.Snapshot.Entities[0].ContentId);
        Assert.Equal(new ItemStack("gel", 1), result.Snapshot.Entities[0].ItemStack);
    }

    [Fact]
    public void ConfigureOptions_AppliesRuntimeAutoPickupSettingOnNextTick()
    {
        var content = CreateContent();
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var entities = new EntityManager(spatialCellSize: 16);
        entities.Add(new DroppedItemEntity(new ItemStack("gel", 1), player.Body.Position, new TileCollisionResolver()));
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            player,
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            options: GameSimulationOptions.Default with { AutoPickupItems = false });

        var disabled = simulation.Tick(PlayerCommand.None, 0.001f);
        simulation.ConfigureOptions(simulation.Options with { AutoPickupItems = true });
        var enabled = simulation.Tick(PlayerCommand.None, 0.001f);

        Assert.Equal(0, disabled.PickedUpItems);
        Assert.Equal(1, enabled.PickedUpItems);
        Assert.Equal(1, simulation.PlayerInventory.CountItem("gel"));
    }

    [Fact]
    public void ConfigureOptions_RejectsUnboundedLightingWorkBudget()
    {
        var content = CreateContent();
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            new EntityManager(),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.ConfigureOptions(
            simulation.Options with { MaxLightingChunksPerTick = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => simulation.ConfigureOptions(
            simulation.Options with { MaxLightingChunksPerTick = 9 }));
    }

    [Fact]
    public void Tick_RenderSnapshotExcludesInactiveEntities()
    {
        var content = CreateContent();
        var entities = new EntityManager();
        var dropped = new DroppedItemEntity(new ItemStack("gel", 1), new Vector2(320, 32), new TileCollisionResolver())
        {
            IsActive = false
        };
        entities.Add(dropped);
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        var snapshot = simulation.Tick(PlayerCommand.None, 0.016f).Snapshot;

        Assert.Empty(snapshot.Entities);
        Assert.Equal(0, snapshot.Hud.ActiveEntities);
        Assert.Equal(0, snapshot.Hud.DroppedItems);
    }

    [Fact]
    public void Tick_AppliesEquipmentAndStatusEffectStatsBeforeSnapshot()
    {
        var content = CreateContent();
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(new ItemStack("vital_helmet", 1), content.Items, EquipmentSlotType.Head).Success);
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        player.StatusEffects.Apply(new StatusEffectDefinition
        {
            Id = "fortified",
            DisplayName = "Fortified",
            DurationSeconds = 5,
            MaxHealthBonus = 7
        });
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            player,
            new PlayerInventory(content.Items),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 },
            equipmentLoadout: loadout);

        var snapshot = simulation.Tick(PlayerCommand.None, 0.016f).Snapshot;

        Assert.Same(loadout, simulation.EquipmentLoadout);
        Assert.Equal(122, snapshot.Player.MaxHealth);
        Assert.Equal(30, snapshot.Player.MaxMana);
        Assert.Equal(122, snapshot.Player.Stats.MaxHealth);
        Assert.Equal(30, snapshot.Player.Stats.MaxMana);
        Assert.Equal(122, snapshot.Hud.MaxHealth);
        Assert.Equal(30, snapshot.Hud.MaxMana);
    }

    [Fact]
    public void Tick_EntityAiCanPerceiveAndAttackAuthoritativePlayer()
    {
        var content = CreateContent();
        var player = new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver());
        var entities = new EntityManager();
        var behavior = new ImmediateAttackBehavior(() => player.Id, damage: 7);
        entities.Add(new EnemyEntity(
            "attacker",
            new Vector2(34, 32),
            new Vector2(16, 16),
            new HealthComponent(10),
            behavior,
            new TileCollisionResolver(),
            contactDamage: 0));
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            player,
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        var result = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.Equal(1, result.EntityAttacks.IntentsConsumed);
        Assert.Equal(1, result.EntityAttacks.HitsApplied);
        Assert.Equal(93, player.Health);
    }

    [Fact]
    public void Tick_EntitySnapshotPreservesFactionAiTargetAndDetailedTelemetry()
    {
        var content = CreateContent();
        var entities = new EntityManager();
        var behavior = new SnapshotTelemetryBehavior();
        var actor = new EnemyEntity(
            "telemetry_probe",
            new Vector2(64, 48),
            new Vector2(16, 18),
            new HealthComponent(25),
            behavior,
            new TileCollisionResolver(),
            contactDamage: 0,
            faction: EntityFaction.Friendly);
        entities.Add(actor);
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(200, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            entities,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        var snapshot = Assert.Single(simulation.Tick(PlayerCommand.None, 1f / 60f).Snapshot.Entities);

        Assert.Equal(EntityFaction.Friendly, snapshot.Faction);
        Assert.Equal(AiState.Chase, snapshot.AiState);
        Assert.Equal(42, snapshot.AiTargetEntityId);
        Assert.Equal(behavior.Telemetry, snapshot.AiTelemetry);
    }

    [Fact]
    public void Tick_EntityDeathsAreFinalizedExactlyOnceByAuthoritativeLifecycle()
    {
        var content = CreateContent();
        var entities = new EntityManager();
        var victim = new EnemyEntity(
            "victim",
            new Vector2(40, 32),
            new Vector2(16, 16),
            new HealthComponent(1),
            NullAiBehavior.Instance,
            new TileCollisionResolver(),
            contactDamage: 0);
        var attackerBehavior = new ImmediateAttackBehavior(() => victim.Id, damage: 3);
        var attacker = new EnemyEntity(
            "friendly_guard",
            new Vector2(36, 32),
            new Vector2(16, 16),
            new HealthComponent(10),
            attackerBehavior,
            new TileCollisionResolver(),
            contactDamage: 0,
            faction: EntityFaction.Friendly);
        entities.Add(attacker);
        entities.Add(victim);
        var events = new GameEventBus();
        var deaths = 0;
        events.Subscribe<EntityDiedEvent>(_ => deaths++);
        using var simulation = new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(200, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            entities,
            events: events,
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });

        var first = simulation.Tick(PlayerCommand.None, 0.016f);
        var second = simulation.Tick(PlayerCommand.None, 0.016f);

        Assert.Equal(1, first.EntityLifecycle.DeathsProcessed);
        Assert.Equal(0, second.EntityLifecycle.DeathsProcessed);
        Assert.Equal(1, deaths);
        Assert.DoesNotContain(second.Snapshot.Entities, entity => entity.Id == victim.Id);
    }

    [Fact]
    public void Dispose_IsIdempotentAndRejectsFurtherTicks()
    {
        var simulation = CreateSimulation();

        simulation.Dispose();
        simulation.Dispose();

        Assert.Throws<ObjectDisposedException>(() => simulation.Tick(PlayerCommand.None, 0.016f));
    }

    [Fact]
    public void ReplayCapture_RecordsAuthoritativeTickInputsAndPeriodicHashes()
    {
        using var simulation = CreateSimulation();
        simulation.StartReplayCapture(new ReplayCaptureOptions
        {
            FrameCapacity = 4,
            CheckpointIntervalTicks = 2
        });
        var command = new PlayerCommand(0.5f, false, false, Vector2.UnitX);

        simulation.Tick(command, 1f / 60f);
        simulation.Tick(PlayerCommand.None, 1f / 60f);
        var snapshot = simulation.StopReplayCapture();

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Frames.Count);
        Assert.Equal(1, snapshot.Frames[0].Tick);
        Assert.Equal(0.5f, snapshot.Frames[0].Command.MoveAxis);
        Assert.Null(snapshot.Frames[0].CheckpointStateHash);
        Assert.NotNull(snapshot.Frames[1].CheckpointStateHash);
        Assert.False(simulation.IsReplayCaptureActive);
        Assert.Null(simulation.CaptureReplayRecording());
    }

    private static GameSimulation CreateSimulation()
    {
        var content = CreateContent();
        return new GameSimulation(
            content,
            new World(16, 16, WorldMetadata.CreateDefault(seed: 1)),
            new BiomeMap("forest"),
            new PlayerEntity(new Vector2(32, 32), new TileCollisionResolver()),
            new PlayerInventory(content.Items),
            spawnOptions: new SpawnSchedulerOptions { MaxTotalActiveEnemies = 0 });
    }

    private static GameContentDatabase CreateContent()
    {
        return new GameContentDatabase(
            TileRegistry.Create(new[]
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
            }),
            ItemRegistry.Create(new[]
            {
                new ItemDefinition
                {
                    Id = "gel",
                    DisplayName = "Gel",
                    Type = ItemType.Material,
                    TexturePath = "items/gel",
                    MaxStack = 999
                },
                new ItemDefinition
                {
                    Id = "watering_can",
                    DisplayName = "Watering Can",
                    Type = ItemType.ToolWateringCan,
                    TexturePath = "items/watering_can",
                    MaxStack = 1,
                    UseTime = 0.25f
                },
                new ItemDefinition
                {
                    Id = "vital_helmet",
                    DisplayName = "Vital Helmet",
                    Type = ItemType.Armor,
                    TexturePath = "items/vital_helmet",
                    MaxStack = 1,
                    EquipmentSlot = EquipmentSlotType.Head,
                    MaxHealthBonus = 15,
                    MaxManaBonus = 10
                }
            }),
            RecipeRegistry.Create(Array.Empty<RecipeDefinition>()),
            LootTableRegistry.Create(Array.Empty<LootTableDefinition>()),
            BiomeRegistry.Create(new[]
            {
                new BiomeDefinition
                {
                    Id = "forest",
                    DisplayName = "Forest",
                    SurfaceTile = "dirt",
                    UndergroundTile = "dirt"
                }
            }),
            ProjectileRegistry.Create(Array.Empty<ProjectileDefinition>()),
            EntityDefinitionRegistry.Create(new[] { CreateSlimeDefinition() }),
            SpawnRuleRegistry.Create(Array.Empty<SpawnRuleDefinition>()));
    }

    private static EntityDefinition CreateSlimeDefinition()
    {
        return new EntityDefinition
        {
            Id = "slime",
            DisplayName = "Slime",
            TexturePath = "entities/slime",
            MaxHealth = 20
        };
    }

    private sealed class ImmediateAttackBehavior : IAiBehavior
    {
        private readonly Func<int> _targetId;
        private readonly int _damage;
        private AiAttackIntent? _pending;

        public ImmediateAttackBehavior(Func<int> targetId, int damage)
        {
            _targetId = targetId;
            _damage = damage;
        }

        public AiState CurrentState => AiState.Attack;

        public int? TargetEntityId => _targetId();

        public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
        {
            _pending = new AiAttackIntent(entity.Id, _targetId(), _damage, 64f, 0f);
        }

        public bool TryConsumeAttackIntent(out AiAttackIntent intent)
        {
            if (_pending is not { } pending)
            {
                intent = default;
                return false;
            }

            intent = pending;
            _pending = null;
            return true;
        }
    }

    private sealed class SnapshotProbeEntity : Entity
    {
        public SnapshotProbeEntity(Vector2 position)
        {
            Position = position;
        }

        public override RectI Bounds => new((int)Position.X, (int)Position.Y, 12, 12);

        public void MoveTo(Vector2 position)
        {
            Position = position;
        }

        public override void Update(World world, float deltaSeconds)
        {
        }
    }

    private sealed class SnapshotTelemetryBehavior : IAiBehavior
    {
        public AiState CurrentState => AiState.Chase;

        public int? TargetEntityId => 42;

        public AiTelemetrySnapshot Telemetry { get; } = new(
            AiState.Chase,
            42,
            new Vector2(11, 12),
            new Vector2(21, 22),
            3.5f,
            true,
            false,
            7,
            101,
            23,
            5);

        public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
        {
        }

        public bool TryConsumeAttackIntent(out AiAttackIntent intent)
        {
            intent = default;
            return false;
        }
    }
}
