using Game.Core.Events;

namespace Game.Core.Saving;

public sealed class GameSaveCoordinator
{
    private readonly PlayerSaveService _players;
    private readonly EntitySaveService _entities;
    private readonly TileEntitySaveService _tileEntities;
    private readonly Func<DateTimeOffset> _clock;
    private float _autosaveAccumulator;

    public GameSaveCoordinator()
        : this(new PlayerSaveService(), new EntitySaveService(), new TileEntitySaveService())
    {
    }

    public GameSaveCoordinator(
        PlayerSaveService players,
        EntitySaveService entities,
        TileEntitySaveService tileEntities,
        Func<DateTimeOffset>? clock = null)
    {
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _tileEntities = tileEntities ?? throw new ArgumentNullException(nameof(tileEntities));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public GameSaveResult Save(
        GameSaveRequest request,
        string saveDirectory,
        GameSaveCoordinatorOptions? options = null,
        GameSaveReason reason = GameSaveReason.Manual,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        options ??= new GameSaveCoordinatorOptions();

        Directory.CreateDirectory(saveDirectory);
        var worldChunksConsidered = options.WorldSaveMode == WorldSaveMode.DirtyChunksOnly
            ? request.World.Chunks.Values.Count(chunk => chunk.IsDirty)
            : request.World.Chunks.Count;

        new WorldSaveService(options.ChunkStorageMode).Save(request.World, saveDirectory, options.WorldSaveMode);

        var playerPath = Path.Combine(saveDirectory, options.PlayerFileName);
        var playerData = _players.CreateSaveData(
            request.Player,
            request.Inventory,
            options.PlayerId,
            options.PlayerDisplayName,
            options.PlayerMana);
        _players.Save(playerData, playerPath);

        var entitiesPath = Path.Combine(saveDirectory, options.EntitiesFileName);
        _entities.Save(request.Entities.Entities, entitiesPath);
        var runtimeEntitiesSaved = request.Entities.Entities.Count(entity => entity.IsActive);

        var tileEntitiesSaved = 0;
        var tileEntitiesWereSaved = request.TileEntities is not null;
        if (request.TileEntities is not null)
        {
            tileEntitiesSaved = request.TileEntities.Entities.Count;
            _tileEntities.Save(request.TileEntities, Path.Combine(saveDirectory, options.TileEntitiesFileName));
        }

        var result = new GameSaveResult(
            saveDirectory,
            reason,
            _clock(),
            options.WorldSaveMode,
            options.ChunkStorageMode,
            worldChunksConsidered,
            runtimeEntitiesSaved,
            tileEntitiesSaved,
            PlayerSaved: true,
            WorldSaved: true,
            EntitiesSaved: true,
            TileEntitiesSaved: tileEntitiesWereSaved);

        events?.Publish(new GameSavedEvent(result));
        return result;
    }

    public GameSaveResult? TickAutosave(
        float deltaSeconds,
        float intervalSeconds,
        GameSaveRequest request,
        string saveDirectory,
        GameSaveCoordinatorOptions? options = null,
        GameEventBus? events = null)
    {
        if (deltaSeconds <= 0 || intervalSeconds <= 0)
        {
            return null;
        }

        _autosaveAccumulator += deltaSeconds;
        if (_autosaveAccumulator < intervalSeconds)
        {
            return null;
        }

        _autosaveAccumulator = MathF.Max(0, _autosaveAccumulator - intervalSeconds);
        return Save(request, saveDirectory, options, GameSaveReason.Autosave, events);
    }

    public void ResetAutosaveTimer()
    {
        _autosaveAccumulator = 0;
    }
}
