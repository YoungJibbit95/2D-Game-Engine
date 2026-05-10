namespace Game.Core.World;

public sealed record TileEditBatchResult(
    int RequestedEdits,
    int ChangedTiles,
    RectI ChangedBounds,
    IReadOnlyList<TilePos> ChangedPositions,
    IReadOnlyCollection<ChunkPos> DirtyChunks)
{
    public static TileEditBatchResult Empty { get; } = new(
        0,
        0,
        new RectI(0, 0, 0, 0),
        Array.Empty<TilePos>(),
        Array.Empty<ChunkPos>());

    public bool HasChanges => ChangedTiles > 0;
}
