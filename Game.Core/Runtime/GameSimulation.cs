using Game.Core.Actions;
using Game.Core.Biomes;
using Game.Core.Combat;
using Game.Core.Data;
using Game.Core.Diagnostics;
using Game.Core.Diagnostics.Replay;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Interaction;
using Game.Core.Inventory;
using Game.Core.Lighting;
using Game.Core.Loot;
using Game.Core.Physics;
using Game.Core.Projectiles;
using Game.Core.Randomness;
using Game.Core.Effects;
using Game.Core.Spawning;
using Game.Core.Time;
using Game.Core.World;
using Game.Core.World.Liquids;
using Game.Core.World.Simulation;
using Game.Core.WorldEvents;
using System.Numerics;
using InventoryModel = Game.Core.Inventory.Inventory;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Runtime;

public sealed class GameSimulation : IDisposable
{
    private static readonly FarmDailyTickResult NoFarming = new(0, 0, 0, 0);
    private static readonly ImmutableSnapshotList<GameSimulationPhase> TickPhases = new(
    [
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
    ]);

    private readonly CombatSystem _combat;
    private readonly CombatDamageResolver _combatDamageResolver;
    private readonly EntityAttackSystem _entityAttacks;
    private readonly EntityDeathLifecycle _entityDeaths;
    private readonly ItemPickupSystem _pickup;
    private readonly SpawnScheduler _spawnScheduler;
    private readonly SpawnSchedulerOptions _spawnOptions;
    private SpawnSchedulerOptions _runtimeSpawnOptions;
    private readonly SpawnActivitySource[] _spawnActivitySources = new SpawnActivitySource[1];
    private readonly PlayerRespawnSystem _respawn;
    private readonly WorldSimulationScheduler _worldSimulation;
    private readonly WorldSimulationOptions _worldSimulationOptions;
    private readonly LiquidSimulationSystem _liquids;
    private readonly WorldSimulationEventBridge _worldSimulationEvents;
    private readonly ChunkMetadataService _chunkMetadata;
    private readonly FarmingSystem _farming;
    private readonly PlayerItemUseSystem _itemUse;
    private readonly LightingSystem _lighting;
    private readonly EquipmentStatCalculator _equipmentStats;
    private readonly LivingWorldRuntime _livingWorld;
    private readonly Random _farmingRandom;
    private readonly DeterministicRandomStream _deathKeys;
    private readonly SimulationPhaseTelemetry _phaseTelemetry = new();
    private GameSimulationOptions _options;
    private bool _disposed;
    private byte _lastSunlight;
    private long _tickNumber;
    private ReplayCaptureSession? _replayCapture;
    private bool _observedWorldEventActive;
    private string? _observedWorldEventId;

    public GameSimulation(
        GameContentDatabase content,
        GameWorld world,
        BiomeMap biomeMap,
        PlayerEntity player,
        PlayerInventory playerInventory,
        EntityManager? entities = null,
        WorldTime? time = null,
        GameEventBus? events = null,
        CombatSystem? combat = null,
        ItemPickupSystem? pickup = null,
        SpawnScheduler? spawnScheduler = null,
        SpawnSchedulerOptions? spawnOptions = null,
        PlayerRespawnSystem? respawn = null,
        WorldSimulationScheduler? worldSimulation = null,
        WorldSimulationOptions? worldSimulationOptions = null,
        LiquidSimulationSystem? liquids = null,
        ChunkMetadataService? chunkMetadata = null,
        FarmPlotManager? farmPlots = null,
        FarmingSystem? farming = null,
        PlayerItemUseSystem? itemUse = null,
        EquipmentLoadout? equipmentLoadout = null,
        EquipmentStatCalculator? equipmentStats = null,
        GameSimulationOptions? options = null,
        LivingWorldRuntime? livingWorld = null,
        SessionRandomRegistry? randomStreams = null,
        GuardRuntimeState? playerGuard = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Biomes = biomeMap ?? throw new ArgumentNullException(nameof(biomeMap));
        Player = player ?? throw new ArgumentNullException(nameof(player));
        PlayerInventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
        Entities = entities ?? new EntityManager();
        Time = time ?? new WorldTime();
        Events = events ?? new GameEventBus();
        FarmPlots = farmPlots ?? new FarmPlotManager();
        EquipmentLoadout = equipmentLoadout ?? new EquipmentLoadout();
        RandomStreams = randomStreams ?? new SessionRandomRegistry(unchecked((ulong)(uint)world.Metadata.Seed));
        PlayerGuard = playerGuard ?? new GuardRuntimeState(new GuardDefinition());
        var collisionResolver = new TileCollisionResolver();
        _combat = combat ?? new CombatSystem(
            new LootRoller(RandomStreams.CreateSystemRandomAdapter("combat.loot")),
            collisionResolver);
        _combatDamageResolver = new CombatDamageResolver(
            RandomStreams.GetStream("combat.critical"),
            RandomStreams.GetStream("combat.status"));
        _entityAttacks = new EntityAttackSystem();
        _entityDeaths = new EntityDeathLifecycle(
            new LootRoller(RandomStreams.GetStream("combat.death-loot")),
            collisionResolver);
        _deathKeys = RandomStreams.GetStream("combat.death-keys");
        _pickup = pickup ?? new ItemPickupSystem();
        _spawnScheduler = spawnScheduler ?? new SpawnScheduler(
            RandomStreams.GetStream("spawning.candidates"),
            RandomStreams.GetStream("spawning.rules"));
        _spawnOptions = spawnOptions ?? SpawnSchedulerOptions.Default;
        _respawn = respawn ?? new PlayerRespawnSystem();
        _worldSimulation = worldSimulation ?? new WorldSimulationScheduler();
        _worldSimulationOptions = worldSimulationOptions ?? WorldSimulationOptions.Default;
        _liquids = liquids ?? new LiquidSimulationSystem();
        _chunkMetadata = chunkMetadata ?? new ChunkMetadataService();
        _farming = farming ?? new FarmingSystem();
        _itemUse = itemUse ?? new PlayerItemUseSystem(
            melee: new MeleeAttackSystem(
                new LootRoller(RandomStreams.CreateSystemRandomAdapter("combat.melee-loot")),
                collisionResolver),
            collisionResolver: collisionResolver,
            statusEffects: new StatusEffectApplier(
                RandomStreams.CreateSystemRandomAdapter("combat.status-effects")));
        _lighting = new LightingSystem();
        _equipmentStats = equipmentStats ?? new EquipmentStatCalculator();
        _farmingRandom = RandomStreams.CreateSystemRandomAdapter("farming.harvest");
        _livingWorld = livingWorld ?? LivingWorldRuntime.CreateDefault(
            world.Metadata.Seed,
            world.HeightTiles,
            Math.Max(6, world.HeightTiles / 3),
            content.Biomes,
            content.WorldEvents);
        _options = options ?? GameSimulationOptions.Default;
        _runtimeSpawnOptions = ResolveSpawnOptions(_spawnOptions, _options);
        _phaseTelemetry.SetEnabled(_options.EnablePhaseTelemetry);
        _lastSunlight = LightingSystem.ResolveSunlight(Time.NormalizedTimeOfDay);
        ValidateOptions(_options);
        _worldSimulationEvents = WorldSimulationEventBridge.Attach(Events, _worldSimulation);
        var initialLivingWorld = CaptureLivingWorld();
        ObserveWorldEvent(initialLivingWorld);
        LatestSnapshot = CaptureSnapshot(
            0,
            CombatResolutionResult.None,
            EntityLifecycleResolution.None,
            0,
            SpawnSchedulerResult.None,
            initialLivingWorld);
    }

    public GameSimulation(
        GameContentDatabase content,
        GameWorld world,
        BiomeMap biomeMap,
        PlayerEntity player,
        InventoryModel playerInventory,
        EntityManager? entities = null,
        WorldTime? time = null,
        GameEventBus? events = null,
        CombatSystem? combat = null,
        ItemPickupSystem? pickup = null,
        SpawnScheduler? spawnScheduler = null,
        SpawnSchedulerOptions? spawnOptions = null,
        PlayerRespawnSystem? respawn = null,
        WorldSimulationScheduler? worldSimulation = null,
        WorldSimulationOptions? worldSimulationOptions = null,
        LiquidSimulationSystem? liquids = null,
        ChunkMetadataService? chunkMetadata = null,
        FarmPlotManager? farmPlots = null,
        FarmingSystem? farming = null,
        PlayerItemUseSystem? itemUse = null,
        EquipmentLoadout? equipmentLoadout = null,
        EquipmentStatCalculator? equipmentStats = null,
        GameSimulationOptions? options = null,
        LivingWorldRuntime? livingWorld = null,
        SessionRandomRegistry? randomStreams = null,
        GuardRuntimeState? playerGuard = null)
        : this(
            content,
            world,
            biomeMap,
            player,
            CreateCompatiblePlayerInventory(playerInventory, content),
            entities,
            time,
            events,
            combat,
            pickup,
            spawnScheduler,
            spawnOptions,
            respawn,
            worldSimulation,
            worldSimulationOptions,
            liquids,
            chunkMetadata,
            farmPlots,
            farming,
            itemUse,
            equipmentLoadout,
            equipmentStats,
            options,
            livingWorld,
            randomStreams,
            playerGuard)
    {
    }

    public GameContentDatabase Content { get; }

    public GameWorld World { get; }

    public BiomeMap Biomes { get; }

    public PlayerEntity Player { get; }

    public PlayerInventory PlayerInventory { get; }

    public EntityManager Entities { get; }

    public WorldTime Time { get; }

    public GameEventBus Events { get; }

    public FarmPlotManager FarmPlots { get; }

    public EquipmentLoadout EquipmentLoadout { get; }

    public LivingWorldRuntime LivingWorld => _livingWorld;

    public SessionRandomRegistry RandomStreams { get; }

    public GuardRuntimeState PlayerGuard { get; }

    public GameSimulationOptions Options => _options;

    public SimulationPhaseTelemetry PhaseTelemetry => _phaseTelemetry;

    public long TickNumber => _tickNumber;

    public bool IsReplayCaptureActive => _replayCapture is not null;

    public FarmSeason CurrentSeason => ResolveSeason(Time.Day);

    public GameFrameSnapshot LatestSnapshot { get; private set; }

    public void ConfigureOptions(GameSimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        _options = options;
        _runtimeSpawnOptions = ResolveSpawnOptions(_spawnOptions, options);
        _phaseTelemetry.SetEnabled(options.EnablePhaseTelemetry);
    }

    public void StartReplayCapture(ReplayCaptureOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _replayCapture = new ReplayCaptureSession(options);
    }

    public ReplayRecordingSnapshot? StopReplayCapture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var snapshot = _replayCapture?.CaptureSnapshot();
        _replayCapture = null;
        return snapshot;
    }

    public ReplayRecordingSnapshot? CaptureReplayRecording()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _replayCapture?.CaptureSnapshot();
    }

    public GameSimulationTickResult Tick(PlayerCommand command, float deltaSeconds)
    {
        return Tick(command, deltaSeconds, PlayerItemUseRequest.Inactive);
    }

    public GameSimulationTickResult Tick(
        PlayerCommand command,
        float deltaSeconds,
        PlayerItemUseRequest itemUseRequest)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (deltaSeconds <= 0)
        {
            return CreateNoTickResult();
        }

        using (_phaseTelemetry.Measure(GameSimulationPhase.PlayerCommand))
        {
            Player.SetCommand(Player.HealthComponent.IsDead ? PlayerCommand.None : command);
            if (Player.HealthComponent.IsDead || !command.WantsGuard)
            {
                PlayerGuard.EndGuard();
            }
            else
            {
                PlayerGuard.TryBeginGuard(command.GuardFacing);
                PlayerGuard.TrySetFacing(command.GuardFacing);
            }

            PlayerGuard.Update(deltaSeconds);
        }

        int daysAdvanced;
        FarmDailyTickResult farming;
        using (_phaseTelemetry.Measure(GameSimulationPhase.WorldTimeAndFarming))
        {
            var previousDay = Time.Day;
            Time.Update(deltaSeconds);
            daysAdvanced = Math.Max(0, Time.Day - previousDay);
            farming = AdvanceFarmingDays(previousDay, daysAdvanced);
        }

        LivingWorldFrameSnapshot livingWorld;
        using (_phaseTelemetry.Measure(GameSimulationPhase.LivingWorld))
        {
            livingWorld = CaptureLivingWorld();
            RouteScheduledWorldEventTransition(livingWorld);
        }
        var eventLootContext = LootKillContext.Empty with
        {
            QuantityMultiplier = livingWorld.LootQuantityMultiplier,
            RareChanceMultiplier = livingWorld.RareLootChanceMultiplier
        };

        using (_phaseTelemetry.Measure(GameSimulationPhase.Player))
        {
            var equipmentStats = _equipmentStats.Calculate(PlayerStatBlock.Base, EquipmentLoadout, Content.Items);
            Player.ApplyStats(Player.StatusEffects.ApplyStatModifiers(equipmentStats));
            if (!Player.HealthComponent.IsDead)
            {
                Player.Update(World, deltaSeconds);
            }
        }

        PlayerItemUseResult itemUse;
        using (_phaseTelemetry.Measure(GameSimulationPhase.PlayerItemUse))
        {
            _itemUse.Update(deltaSeconds);
            itemUse = !Player.HealthComponent.IsDead && itemUseRequest.IsActive
                ? _itemUse.UseSelectedItem(
                    World,
                    Content,
                    Player,
                    PlayerInventory,
                    Entities,
                    itemUseRequest.TargetTile,
                    itemUseRequest.TargetWorldPosition,
                    deltaSeconds,
                    Events,
                    FarmPlots,
                    CurrentSeason,
                    Time.Day,
                    _farmingRandom,
                    eventLootContext)
                : PlayerItemUseResult.None;
            RoutePlayerActionWorldEvent(itemUse);
        }

        using (_phaseTelemetry.Measure(GameSimulationPhase.Entities))
        {
            Entities.UpdateAll(World, deltaSeconds, Player, Time.IsNight, _tickNumber);
        }

        EntityAttackResolution entityAttacks;
        using (_phaseTelemetry.Measure(GameSimulationPhase.EntityAttacks))
        {
            entityAttacks = _entityAttacks.ResolvePendingAttacks(Entities, Player);
        }

        CombatResolutionResult combat;
        ContactDamageResult contactDamage;
        using (_phaseTelemetry.Measure(GameSimulationPhase.Combat))
        {
            combat = _combat.ResolveProjectileHits(Entities, Content, Events, eventLootContext);
            contactDamage = Player.HealthComponent.IsDead
                ? ContactDamageResult.None
                : _combat.ResolvePlayerDamage(
                    Player,
                    Entities,
                    Content,
                    PlayerGuard,
                    _combatDamageResolver,
                    events: Events);
        }

        EntityLifecycleResolution entityLifecycle;
        using (_phaseTelemetry.Measure(GameSimulationPhase.EntityDeaths))
        {
            entityLifecycle = ResolveEntityDeaths(livingWorld);
        }

        using (_phaseTelemetry.Measure(GameSimulationPhase.PickupMagnetism))
        {
            if (_options.AutoPickupItems && !Player.HealthComponent.IsDead)
            {
                AttractNearbyDroppedItems(deltaSeconds);
            }
        }

        int pickedUpItems;
        using (_phaseTelemetry.Measure(GameSimulationPhase.Pickups))
        {
            pickedUpItems = Player.HealthComponent.IsDead || !_options.AutoPickupItems
                ? 0
                : _pickup.PickupItems(
                    Entities,
                    PlayerInventory,
                    Player.Bounds.Inflate(_options.PickupRadiusPixels),
                    Events);
        }

        SpawnSchedulerResult spawning;
        using (_phaseTelemetry.Measure(GameSimulationPhase.Spawning))
        {
            var playerTile = CoordinateUtils.WorldToTile(Player.Body.Center.X, Player.Body.Center.Y);
            _spawnActivitySources[0] = SpawnActivitySource.ForPlayer(
                Player.Id,
                playerTile,
                ResolveSpawnVisibleBounds(playerTile, _options),
                new SpawnEnvironment(
                    livingWorld.BiomeId,
                    livingWorld.BiomeLayerId,
                    ResolveWeatherId(livingWorld.Weather),
                    livingWorld.WorldEventId,
                    livingWorld.SpawnDensityMultiplier));
            spawning = _spawnScheduler.Update(
                World,
                Entities,
                Content,
                Biomes,
                Time,
                _spawnActivitySources,
                deltaSeconds,
                _runtimeSpawnOptions);
        }

        PlayerRespawnResult respawn;
        using (_phaseTelemetry.Measure(GameSimulationPhase.Respawn))
        {
            respawn = _respawn.Update(Player, World, deltaSeconds);
        }

        WorldSimulationTickResult worldSimulation;
        using (_phaseTelemetry.Measure(GameSimulationPhase.WorldSimulation))
        {
            worldSimulation = _worldSimulation.Tick(World, deltaSeconds, _liquids, _worldSimulationOptions);
            if (worldSimulation.RenderDirtyRegions.Count > 0)
            {
                _chunkMetadata.RefreshRegions(World, worldSimulation.RenderDirtyRegions);
            }
        }

        using (_phaseTelemetry.Measure(GameSimulationPhase.Lighting))
        {
            var baseSunlight = LightingSystem.ResolveSunlight(Time.NormalizedTimeOfDay);
            var regionalSunlightMultiplier = Math.Clamp(
                1f - livingWorld.CloudCover * 0.45f,
                0.45f,
                1f);
            var sunlight = (byte)Math.Clamp(
                (int)MathF.Round(baseSunlight * regionalSunlightMultiplier),
                byte.MinValue,
                byte.MaxValue);
            if (Math.Abs(sunlight - _lastSunlight) >= 8)
            {
                foreach (var chunk in World.Chunks.Values)
                {
                    chunk.MarkLightDirty();
                }

                _lastSunlight = sunlight;
            }
            _lighting.RecalculateDirty(
                World,
                Content.Tiles,
                sunlight,
                ResolveVisibleTileBounds(
                    CoordinateUtils.WorldToTile(Player.Body.Center.X, Player.Body.Center.Y),
                    _options));
        }

        using (_phaseTelemetry.Measure(GameSimulationPhase.FrameSnapshot))
        {
            _tickNumber++;
            LatestSnapshot = CaptureSnapshot(
                _tickNumber,
                combat,
                entityLifecycle,
                pickedUpItems,
                spawning,
                livingWorld);
        }

        if (_replayCapture is { } replayCapture)
        {
            ulong? checkpointHash = replayCapture.ShouldCaptureCheckpoint(_tickNumber)
                ? SimulationStateHasher.Compute(
                    World,
                    Player,
                    PlayerInventory,
                    Entities,
                    Time,
                    FarmPlots,
                    EquipmentLoadout,
                    RandomStreams)
                : null;
            replayCapture.Record(
                _tickNumber,
                command,
                itemUseRequest,
                deltaSeconds,
                checkpointHash);
        }

        return new GameSimulationTickResult(
            DidTick: true,
            _tickNumber,
            TickPhases,
            entityAttacks,
            combat,
            contactDamage,
            entityLifecycle,
            pickedUpItems,
            spawning,
            respawn,
            worldSimulation,
            daysAdvanced,
            farming,
            itemUse,
            LatestSnapshot);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _worldSimulationEvents.Dispose();
    }

    private GameSimulationTickResult CreateNoTickResult()
    {
        return new GameSimulationTickResult(
            DidTick: false,
            _tickNumber,
            ImmutableSnapshotList<GameSimulationPhase>.Empty,
            EntityAttackResolution.None,
            CombatResolutionResult.None,
            ContactDamageResult.None,
            EntityLifecycleResolution.None,
            0,
            SpawnSchedulerResult.None,
            PlayerRespawnResult.None,
            WorldSimulationTickResult.None,
            0,
            NoFarming,
            PlayerItemUseResult.None,
            LatestSnapshot);
    }

    private FarmDailyTickResult AdvanceFarmingDays(int previousDay, int daysAdvanced)
    {
        if (daysAdvanced == 0)
        {
            return NoFarming;
        }

        var advanced = 0;
        var matured = 0;
        var watered = 0;
        var withered = 0;
        for (var offset = 1; offset <= daysAdvanced; offset++)
        {
            var result = _farming.AdvanceDay(Content.Crops, FarmPlots, ResolveSeason(previousDay + offset));
            advanced += result.AdvancedCrops;
            matured += result.NewlyMatureCrops;
            watered += result.ClearedWateredPlots;
            withered += result.WitheredCrops;
        }

        return new FarmDailyTickResult(advanced, matured, watered, withered);
    }

    private GameFrameSnapshot CaptureSnapshot(
        long tickNumber,
        CombatResolutionResult combat,
        EntityLifecycleResolution entityLifecycle,
        int pickedUpItems,
        SpawnSchedulerResult spawning,
        LivingWorldFrameSnapshot livingWorld)
    {
        var hotbar = new InventorySlotFrameSnapshot[PlayerInventory.Hotbar.Slots.Count];
        var occupiedInventorySlots = 0;
        var totalInventoryItems = 0;
        for (var index = 0; index < PlayerInventory.Hotbar.Slots.Count; index++)
        {
            var slot = PlayerInventory.Hotbar.Slots[index];
            hotbar[index] = new InventorySlotFrameSnapshot(slot.Stack, slot.IsFavorite);
            AccumulateInventorySlot(slot, ref occupiedInventorySlots, ref totalInventoryItems);
        }

        foreach (var slot in PlayerInventory.Main.Slots)
        {
            AccumulateInventorySlot(slot, ref occupiedInventorySlots, ref totalInventoryItems);
        }

        var player = new PlayerFrameSnapshot(
            Player.Body.Position,
            Player.Body.Velocity,
            Player.Bounds,
            Player.Body.OnGround,
            Player.HealthComponent.IsDead,
            Player.Health,
            Player.MaxHealth,
            Player.Mana,
            Player.MaxMana,
            Player.Stats,
            PlayerGuard.IsGuarding,
            PlayerGuard.IsGuardBroken,
            PlayerGuard.Stamina,
            PlayerGuard.Definition.MaxStamina,
            PlayerInventory.SelectedHotbarSlot,
            new ImmutableSnapshotList<InventorySlotFrameSnapshot>(hotbar));

        var entitySnapshots = new List<EntityFrameSnapshot>(Entities.Entities.Count);
        var activeEnemies = 0;
        var droppedItems = 0;
        var projectiles = 0;
        foreach (var entity in Entities.Entities)
        {
            if (!entity.IsActive)
            {
                continue;
            }

            var snapshot = CaptureEntity(entity);
            entitySnapshots.Add(snapshot);
            switch (snapshot.Kind)
            {
                case EntityFrameKind.Enemy:
                    activeEnemies++;
                    break;
                case EntityFrameKind.DroppedItem:
                    droppedItems++;
                    break;
                case EntityFrameKind.Projectile:
                    projectiles++;
                    break;
            }
        }

        var worldTime = new WorldTimeFrameSnapshot(
            Time.Day,
            Time.TimeOfDaySeconds,
            Time.DayLengthSeconds,
            Time.NormalizedTimeOfDay,
            Time.IsNight);
        var farmPlots = new List<FarmPlotFrameSnapshot>(FarmPlots.Plots.Count);
        foreach (var plot in FarmPlots.Plots)
        {
            farmPlots.Add(new FarmPlotFrameSnapshot(
                plot.Position,
                plot.IsTilled,
                plot.IsWatered,
                plot.Crop?.CropId,
                plot.Crop?.PlantedDay ?? 0,
                plot.Crop?.DaysUntilHarvest ?? 0,
                plot.Crop?.HarvestCount ?? 0,
                plot.Crop?.IsMature ?? false));
        }

        var hud = new HudFrameSnapshot(
            Player.Health,
            Player.MaxHealth,
            Player.Mana,
            Player.MaxMana,
            PlayerInventory.SelectedHotbarSlot,
            entitySnapshots.Count,
            activeEnemies,
            droppedItems,
            projectiles,
            occupiedInventorySlots,
            totalInventoryItems,
            FarmPlots.Plots.Count,
            pickedUpItems,
            spawning.Spawned,
            combat.EnemyDeaths + entityLifecycle.DeathsProcessed);

        return new GameFrameSnapshot(
            tickNumber,
            player,
            worldTime,
            livingWorld,
            new ImmutableSnapshotList<EntityFrameSnapshot>(entitySnapshots),
            new ImmutableSnapshotList<FarmPlotFrameSnapshot>(farmPlots),
            hud);
    }

    private LivingWorldFrameSnapshot CaptureLivingWorld()
    {
        var elapsedWorldSeconds = Math.Max(0, Time.Day - 1) * Time.DayLengthSeconds + Time.TimeOfDaySeconds;
        var worldTick = (long)Math.Clamp(
            Math.Floor(elapsedWorldSeconds * 60d),
            0d,
            long.MaxValue);
        return _livingWorld.Capture(
            CoordinateUtils.WorldToTile(Player.Body.Center.X, Player.Body.Center.Y),
            worldTick,
            LightingSystem.ResolveSunlight(Time.NormalizedTimeOfDay) / 255f);
    }

    private void RoutePlayerActionWorldEvent(in PlayerItemUseResult itemUse)
    {
        if (!itemUse.Success || !TryResolveWorldEventAction(itemUse.Kind, out var action))
        {
            return;
        }

        var sequence = checked(_livingWorld.LastProcessedPlayerActionSequence + 1);
        var result = _livingWorld.TriggerPlayerAction(action, sequence);
        Events.Publish(new WorldEventPlayerActionEvaluatedEvent(
            result.Sequence,
            result.Action,
            result.Activated,
            result.EventId));
        if (result.Activated && result.EventId is not null)
        {
            _observedWorldEventActive = true;
            _observedWorldEventId = result.EventId;
            Events.Publish(new WorldEventActivatedEvent(
                result.EventId,
                WorldEventActivationSource.PlayerAction,
                result.Action,
                result.Sequence));
        }
    }

    private static bool TryResolveWorldEventAction(
        PlayerItemUseKind kind,
        out WorldEventPlayerActionKind action)
    {
        switch (kind)
        {
            case PlayerItemUseKind.Mine:
                action = WorldEventPlayerActionKind.Mine;
                return true;
            case PlayerItemUseKind.Build:
                action = WorldEventPlayerActionKind.Build;
                return true;
            case PlayerItemUseKind.Melee:
                action = WorldEventPlayerActionKind.Melee;
                return true;
            case PlayerItemUseKind.Shoot:
                action = WorldEventPlayerActionKind.Shoot;
                return true;
            case PlayerItemUseKind.Cast:
                action = WorldEventPlayerActionKind.Cast;
                return true;
            case PlayerItemUseKind.Consume:
                action = WorldEventPlayerActionKind.Consume;
                return true;
            case PlayerItemUseKind.Till:
            case PlayerItemUseKind.Water:
            case PlayerItemUseKind.Plant:
            case PlayerItemUseKind.Harvest:
                action = WorldEventPlayerActionKind.Farm;
                return true;
            default:
                action = default;
                return false;
        }
    }

    private EntityLifecycleResolution ResolveEntityDeaths(in LivingWorldFrameSnapshot livingWorld)
    {
        var deaths = 0;
        var drops = 0;
        var entityCount = Entities.Entities.Count;
        for (var index = 0; index < entityCount; index++)
        {
            if (Entities.Entities[index] is not EnemyEntity { IsActive: true } enemy || !enemy.Health.IsDead)
            {
                continue;
            }

            var tile = CoordinateUtils.WorldToTile(enemy.Body.Center.X, enemy.Body.Center.Y);
            var killContext = enemy.CreateLootKillContext(isNight: Time.IsNight, victimDepth: tile.Y) with
            {
                QuantityMultiplier = livingWorld.LootQuantityMultiplier,
                RareChanceMultiplier = livingWorld.RareLootChanceMultiplier
            };
            var result = _entityDeaths.ResolveDeath(
                enemy,
                Entities,
                Content.LootTables,
                killContext,
                new LootRollKey(
                    World.Metadata.Seed,
                    unchecked((long)_deathKeys.NextUInt64()),
                    enemy.Id),
                Events);
            if (!result.Processed)
            {
                continue;
            }

            deaths++;
            drops += result.DroppedStacks;
            Events.Publish(new EntityDiedEvent(enemy.Id, enemy.DefinitionId));
        }

        return deaths == 0 ? EntityLifecycleResolution.None : new EntityLifecycleResolution(deaths, drops);
    }

    private void RouteScheduledWorldEventTransition(in LivingWorldFrameSnapshot snapshot)
    {
        if (snapshot.IsWorldEventActive &&
            (!_observedWorldEventActive ||
             !string.Equals(snapshot.WorldEventId, _observedWorldEventId, StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(snapshot.WorldEventId))
        {
            Events.Publish(new WorldEventActivatedEvent(
                snapshot.WorldEventId,
                WorldEventActivationSource.Schedule,
                null,
                0));
        }

        ObserveWorldEvent(snapshot);
    }

    private void ObserveWorldEvent(in LivingWorldFrameSnapshot snapshot)
    {
        _observedWorldEventActive = snapshot.IsWorldEventActive;
        _observedWorldEventId = snapshot.WorldEventId;
    }

    private void AttractNearbyDroppedItems(float deltaSeconds)
    {
        var magnetRadius = _options.ItemMagnetRadiusPixels;
        if (magnetRadius <= 0 || _options.ItemMagnetStrength <= 0)
        {
            return;
        }

        var playerCenter = Player.Body.Center;
        foreach (var entity in Entities.Query(Player.Bounds.Inflate((int)MathF.Ceiling(magnetRadius))))
        {
            if (entity is not DroppedItemEntity { IsActive: true } droppedItem)
            {
                continue;
            }

            var offset = playerCenter - droppedItem.Body.Center;
            var distance = offset.Length();
            if (distance <= 0.001f || distance > magnetRadius)
            {
                continue;
            }

            var direction = offset / distance;
            var strength = (1f - distance / magnetRadius) * _options.ItemMagnetStrength;
            droppedItem.Body.Velocity += direction * strength * deltaSeconds;
        }
    }

    private static EntityFrameSnapshot CaptureEntity(Entity entity)
    {
        return entity switch
        {
            EnemyEntity enemy => new EntityFrameSnapshot(
                enemy.Id,
                EntityFrameKind.Enemy,
                enemy.DefinitionId,
                enemy.Position,
                enemy.Body.Velocity,
                enemy.Bounds,
                enemy.IsActive,
                enemy.Health.Current,
                enemy.Health.Max,
                ItemStack.Empty,
                enemy.Health.InvulnerabilityTimeRemaining > 0,
                DamageType.Generic)
            {
                Faction = enemy.Faction,
                AiState = enemy.AiState,
                AiTargetEntityId = enemy.TargetEntityId,
                AiTelemetry = enemy.AiTelemetry
            },
            DroppedItemEntity droppedItem => new EntityFrameSnapshot(
                droppedItem.Id,
                EntityFrameKind.DroppedItem,
                droppedItem.Stack.ItemId,
                droppedItem.Position,
                droppedItem.Body.Velocity,
                droppedItem.Bounds,
                droppedItem.IsActive,
                0,
                0,
                droppedItem.Stack,
                false,
                DamageType.Generic),
            ProjectileEntity projectile => new EntityFrameSnapshot(
                projectile.Id,
                EntityFrameKind.Projectile,
                projectile.ProjectileId,
                projectile.Position,
                projectile.Velocity,
                projectile.Bounds,
                projectile.IsActive,
                0,
                0,
                ItemStack.Empty,
                false,
                projectile.DamageType),
            _ => new EntityFrameSnapshot(
                entity.Id,
                EntityFrameKind.Entity,
                entity.GetType().Name,
                entity.Position,
                Vector2.Zero,
                entity.Bounds,
                entity.IsActive,
                0,
                0,
                ItemStack.Empty,
                false,
                DamageType.Generic)
        };
    }

    private static void AccumulateInventorySlot(
        InventorySlot slot,
        ref int occupiedInventorySlots,
        ref int totalInventoryItems)
    {
        if (slot.IsEmpty)
        {
            return;
        }

        occupiedInventorySlots++;
        totalInventoryItems += slot.Stack.Count;
    }

    private static PlayerInventory CreateCompatiblePlayerInventory(
        InventoryModel inventory,
        GameContentDatabase content)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(content);
        return inventory.Slots.Count == PlayerInventory.HotbarSlotCount
            ? new PlayerInventory(inventory, new InventoryModel(PlayerInventory.MainSlotCount, content.Items), content.Items)
            : new PlayerInventory(new InventoryModel(PlayerInventory.HotbarSlotCount, content.Items), inventory, content.Items);
    }

    private static void ValidateOptions(GameSimulationOptions options)
    {
        if (options.PickupRadiusPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Pickup radius must not be negative.");
        }

        if (!float.IsFinite(options.ItemMagnetRadiusPixels) || options.ItemMagnetRadiusPixels < 0 ||
            !float.IsFinite(options.ItemMagnetStrength) || options.ItemMagnetStrength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Item magnet values must be finite and non-negative.");
        }

        if (options.MaxActiveEnemies < -1 ||
            !float.IsFinite(options.EnemySpawnRateMultiplier) ||
            options.EnemySpawnRateMultiplier < 0 ||
            options.SpawnMinimumDistanceTiles < 0 ||
            options.SpawnMaximumDistanceTiles < options.SpawnMinimumDistanceTiles ||
            options.SpawnVisibleHalfWidthTiles < 0 ||
            options.SpawnVisibleHalfHeightTiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn options must be finite, non-negative, and ordered.");
        }
    }

    private static SpawnSchedulerOptions ResolveSpawnOptions(
        SpawnSchedulerOptions baseOptions,
        GameSimulationOptions simulationOptions)
    {
        var spawnInterval = simulationOptions.EnemySpawnRateMultiplier <= 0
            ? float.MaxValue
            : baseOptions.SpawnIntervalSeconds / simulationOptions.EnemySpawnRateMultiplier;
        return baseOptions with
        {
            SpawnIntervalSeconds = spawnInterval,
            MinDistanceTiles = simulationOptions.SpawnMinimumDistanceTiles,
            MaxDistanceTiles = simulationOptions.SpawnMaximumDistanceTiles,
            OnScreenHalfWidthTiles = simulationOptions.SpawnOutsideViewportOnly
                ? simulationOptions.SpawnVisibleHalfWidthTiles
                : 0,
            OnScreenHalfHeightTiles = simulationOptions.SpawnOutsideViewportOnly
                ? simulationOptions.SpawnVisibleHalfHeightTiles
                : 0,
            MaxTotalActiveEnemies = simulationOptions.MaxActiveEnemies >= 0
                ? simulationOptions.MaxActiveEnemies
                : baseOptions.MaxTotalActiveEnemies
        };
    }

    private static RectI ResolveSpawnVisibleBounds(TilePos center, GameSimulationOptions options)
    {
        if (!options.SpawnOutsideViewportOnly)
        {
            return default;
        }

        return ResolveVisibleTileBounds(center, options);
    }

    private static RectI ResolveVisibleTileBounds(TilePos center, GameSimulationOptions options)
    {
        var left = Saturate((long)center.X - options.SpawnVisibleHalfWidthTiles);
        var top = Saturate((long)center.Y - options.SpawnVisibleHalfHeightTiles);
        var right = Saturate((long)center.X + options.SpawnVisibleHalfWidthTiles);
        var bottom = Saturate((long)center.Y + options.SpawnVisibleHalfHeightTiles);
        return RectI.FromInclusiveTileBounds(left, top, right, bottom);
    }

    private static string ResolveWeatherId(Game.Core.Weather.WeatherKind weather)
    {
        return weather switch
        {
            Game.Core.Weather.WeatherKind.Rain => "rain",
            Game.Core.Weather.WeatherKind.Storm => "storm",
            Game.Core.Weather.WeatherKind.Fog => "fog",
            _ => "clear"
        };
    }

    private static int Saturate(long value)
    {
        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)value;
    }

    private static FarmSeason ResolveSeason(int day)
    {
        return (((Math.Max(1, day) - 1) / 28) % 4) switch
        {
            0 => FarmSeason.Spring,
            1 => FarmSeason.Summer,
            2 => FarmSeason.Fall,
            _ => FarmSeason.Winter
        };
    }

}
