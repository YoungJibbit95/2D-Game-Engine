using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Physics;
using Game.Core.World.TileEntities;
using System.Numerics;
using Game.Core.Randomness;

namespace Game.Core.Saving;

public sealed class GameLoadCoordinator
{
    private const string MetadataFileName = "metadata.json";

    private readonly WorldSaveService _worlds;
    private readonly PlayerSaveService _players;
    private readonly EntitySaveService _runtimeEntities;
    private readonly TileEntitySaveService _tileEntities;
    private readonly FarmPlotSaveService _farmPlots;
    private readonly SimulationSaveService _simulation = new();
    private readonly RandomStateSaveService _randomState = new();
    private readonly WorldEventStateSaveService _worldEvents = new();
    private readonly TileCollisionResolver _collisionResolver;
    private readonly Func<DateTimeOffset> _clock;

    public GameLoadCoordinator()
        : this(
            new WorldSaveService(WorldChunkStorageMode.RegionFiles),
            new PlayerSaveService(),
            new EntitySaveService(),
            new TileEntitySaveService(),
            new FarmPlotSaveService(),
            new TileCollisionResolver())
    {
    }

    public GameLoadCoordinator(
        WorldSaveService worlds,
        PlayerSaveService players,
        EntitySaveService runtimeEntities,
        TileEntitySaveService tileEntities,
        TileCollisionResolver collisionResolver,
        Func<DateTimeOffset>? clock = null)
        : this(worlds, players, runtimeEntities, tileEntities, farmPlots: null, collisionResolver, clock)
    {
    }

    public GameLoadCoordinator(
        WorldSaveService worlds,
        PlayerSaveService players,
        EntitySaveService runtimeEntities,
        TileEntitySaveService tileEntities,
        FarmPlotSaveService? farmPlots,
        TileCollisionResolver collisionResolver,
        Func<DateTimeOffset>? clock = null)
    {
        _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _runtimeEntities = runtimeEntities ?? throw new ArgumentNullException(nameof(runtimeEntities));
        _tileEntities = tileEntities ?? throw new ArgumentNullException(nameof(tileEntities));
        _farmPlots = farmPlots ?? new FarmPlotSaveService();
        _collisionResolver = collisionResolver ?? throw new ArgumentNullException(nameof(collisionResolver));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public bool CanLoad(string saveDirectory, GameLoadCoordinatorOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
        {
            return false;
        }

        options ??= new GameLoadCoordinatorOptions();
        return File.Exists(Path.Combine(saveDirectory, MetadataFileName)) &&
               File.Exists(Path.Combine(saveDirectory, options.PlayerFileName));
    }

    public GameLoadResult Load(
        string saveDirectory,
        GameContentDatabase content,
        GameLoadCoordinatorOptions? options = null,
        GameEventBus? events = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        ArgumentNullException.ThrowIfNull(content);
        options ??= new GameLoadCoordinatorOptions();

        var world = _worlds.Load(saveDirectory);
        var playerPath = Path.Combine(saveDirectory, options.PlayerFileName);
        var playerData = _players.Load(playerPath);
        var inventory = _players.ToPlayerInventory(playerData, content.Items);
        var player = RestorePlayer(playerData);
        var playerWarnings = new List<PlayerLoadWarning>();
        var equipmentLoadout = _players.ToEquipmentLoadout(playerData, content.Items, playerWarnings);
        _players.RestoreStatusEffects(playerData, player.StatusEffects, content.StatusEffects, playerWarnings);
        var characterAppearance = _players.ToCharacterAppearance(playerData);

        var entityManager = new EntityManager();
        var runtimeEntitiesLoaded = false;
        if (options.LoadRuntimeEntities)
        {
            var entitiesPath = Path.Combine(saveDirectory, options.EntitiesFileName);
            runtimeEntitiesLoaded = File.Exists(entitiesPath);
            foreach (var entity in _runtimeEntities.Load(entitiesPath, content))
            {
                entityManager.Add(entity);
            }
        }

        var tileEntityManager = new TileEntityManager();
        var tileEntitiesLoaded = false;
        if (options.LoadTileEntities)
        {
            var tileEntitiesPath = Path.Combine(saveDirectory, options.TileEntitiesFileName);
            tileEntitiesLoaded = File.Exists(tileEntitiesPath);
            tileEntityManager = _tileEntities.Load(tileEntitiesPath, content.Items);
        }

        var farmPlotManager = new FarmPlotManager();
        var farmPlotsLoaded = false;
        if (options.LoadFarmPlots)
        {
            var farmPlotsPath = Path.Combine(saveDirectory, options.FarmPlotsFileName);
            farmPlotsLoaded = File.Exists(farmPlotsPath);
            farmPlotManager = _farmPlots.Load(farmPlotsPath, content.Crops);
        }

        var worldTime = new Game.Core.Time.WorldTime();
        var simulationStateLoaded = false;
        if (options.LoadSimulationState)
        {
            var simulationPath = Path.Combine(saveDirectory, options.SimulationStateFileName);
            simulationStateLoaded = File.Exists(simulationPath);
            if (simulationStateLoaded)
            {
                worldTime = _simulation.Load(simulationPath);
            }
        }

        var randomStatePath = Path.Combine(saveDirectory, options.RandomStateFileName);
        var randomStateLoad = options.LoadRandomState
            ? _randomState.LoadOrCreate(randomStatePath, unchecked((ulong)(uint)world.Metadata.Seed))
            : new RandomStateLoadResult
            {
                Registry = new SessionRandomRegistry(unchecked((ulong)(uint)world.Metadata.Seed)),
                Source = RandomStateLoadSource.LegacyFallback
            };

        var worldEventStateLoad = options.LoadWorldEventState
            ? _worldEvents.LoadOrDefault(Path.Combine(saveDirectory, options.WorldEventStateFileName))
            : new WorldEventStateLoadResult
            {
                Source = WorldEventStateLoadSource.LegacyFallback
            };

        var result = new GameLoadResult(
            saveDirectory,
            _clock(),
            world,
            player,
            inventory,
            entityManager,
            tileEntityManager,
            PlayerLoaded: true,
            RuntimeEntitiesLoaded: runtimeEntitiesLoaded,
            TileEntitiesLoaded: tileEntitiesLoaded,
            FarmPlotsLoaded: farmPlotsLoaded,
            RuntimeEntityCount: entityManager.Entities.Count,
            TileEntityCount: tileEntityManager.Entities.Count,
            FarmPlots: farmPlotManager,
            FarmPlotCount: farmPlotManager.Plots.Count)
        {
            EquipmentLoadout = equipmentLoadout,
            CharacterAppearance = characterAppearance,
            PlayerWarnings = playerWarnings.ToArray(),
            WorldTime = worldTime,
            SimulationStateLoaded = simulationStateLoaded,
            RandomStreams = randomStateLoad.Registry,
            RandomStateLoaded = randomStateLoad.Source is not RandomStateLoadSource.LegacyFallback,
            RandomStateSource = randomStateLoad.Source,
            RandomStateWarning = randomStateLoad.Warning,
            WorldEventState = worldEventStateLoad.State,
            WorldEventStateLoaded = worldEventStateLoad.Source is not WorldEventStateLoadSource.LegacyFallback,
            WorldEventStateSource = worldEventStateLoad.Source,
            WorldEventStateWarning = worldEventStateLoad.Warning
        };

        events?.Publish(new GameLoadedEvent(result));
        return result;
    }

    private PlayerEntity RestorePlayer(PlayerSaveData data)
    {
        return new PlayerEntity(
            new Vector2(data.PositionX, data.PositionY),
            _collisionResolver,
            data.MaxHealth,
            data.Health,
            currentMana: data.Mana);
    }
}
