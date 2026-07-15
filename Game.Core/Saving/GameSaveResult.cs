namespace Game.Core.Saving;

public sealed record GameSaveResult(
    string SaveDirectory,
    GameSaveReason Reason,
    DateTimeOffset SavedAtUtc,
    WorldSaveMode WorldSaveMode,
    WorldChunkStorageMode ChunkStorageMode,
    int WorldChunksConsidered,
    int RuntimeEntitiesSaved,
    int TileEntityCount,
    bool PlayerSaved,
    bool WorldSaved,
    bool EntitiesSaved,
    bool TileEntitiesSaved,
    int FarmPlotCount = 0,
    bool FarmPlotsSaved = false)
{
    public bool SimulationStateSaved { get; init; }

    public bool RandomStateSaved { get; init; }

    public bool WorldEventStateSaved { get; init; }

    public static GameSaveResult NotSaved(string saveDirectory, GameSaveReason reason, DateTimeOffset now)
    {
        return new GameSaveResult(
            saveDirectory,
            reason,
            now,
            WorldSaveMode.DirtyChunksOnly,
            WorldChunkStorageMode.LooseFiles,
            0,
            0,
            0,
            PlayerSaved: false,
            WorldSaved: false,
            EntitiesSaved: false,
            TileEntitiesSaved: false,
            FarmPlotCount: 0,
            FarmPlotsSaved: false);
    }
}
