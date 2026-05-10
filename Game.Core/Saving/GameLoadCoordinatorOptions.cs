namespace Game.Core.Saving;

public sealed record GameLoadCoordinatorOptions
{
    public string PlayerFileName { get; init; } = "player.json";

    public string EntitiesFileName { get; init; } = "entities.json";

    public string TileEntitiesFileName { get; init; } = "tile_entities.json";

    public bool LoadRuntimeEntities { get; init; } = true;

    public bool LoadTileEntities { get; init; } = true;
}
