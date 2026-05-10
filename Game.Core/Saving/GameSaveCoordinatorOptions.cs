namespace Game.Core.Saving;

public sealed record GameSaveCoordinatorOptions
{
    public WorldChunkStorageMode ChunkStorageMode { get; init; } = WorldChunkStorageMode.RegionFiles;

    public WorldSaveMode WorldSaveMode { get; init; } = WorldSaveMode.DirtyChunksOnly;

    public string PlayerFileName { get; init; } = "player.json";

    public string EntitiesFileName { get; init; } = "entities.json";

    public string TileEntitiesFileName { get; init; } = "tile_entities.json";

    public string FarmPlotsFileName { get; init; } = "farm_plots.json";

    public string PlayerId { get; init; } = "player_001";

    public string PlayerDisplayName { get; init; } = "Player";

    public int PlayerMana { get; init; }
}
