namespace Game.Core.World;

public sealed record ChunkMetadata(
    int ActiveLiquidTiles,
    int ActiveLightTiles,
    int TileEntityCount,
    long LastSavedTick)
{
    public static ChunkMetadata Empty { get; } = new(0, 0, 0, 0);

    public bool HasActiveSimulation => ActiveLiquidTiles > 0 || ActiveLightTiles > 0 || TileEntityCount > 0;
}
