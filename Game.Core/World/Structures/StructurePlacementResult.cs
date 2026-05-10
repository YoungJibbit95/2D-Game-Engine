namespace Game.Core.World.Structures;

public readonly record struct StructurePlacementResult(
    bool Placed,
    int TilesWritten,
    RectI ChangedBounds,
    IReadOnlyCollection<ChunkPos> DirtyChunks)
{
    public static StructurePlacementResult Failed { get; } = new(
        false,
        0,
        new RectI(0, 0, 0, 0),
        Array.Empty<ChunkPos>());
}
