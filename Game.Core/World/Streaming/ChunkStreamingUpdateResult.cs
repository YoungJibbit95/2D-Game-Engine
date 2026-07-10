namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingUpdateResult(
    ChunkStreamingPlan Plan,
    int LoadedChunks,
    int GeneratedChunks,
    int SavedChunksBeforeUnload,
    int UnloadedChunks,
    int SkippedDirtyUnloads,
    IReadOnlyList<ChunkPos> LoadedChunkPositions,
    IReadOnlyList<ChunkPos> GeneratedChunkPositions,
    IReadOnlyList<ChunkPos> SavedChunkPositions,
    IReadOnlyList<ChunkPos> UnloadedChunkPositions,
    IReadOnlyList<ChunkPos> SkippedDirtyUnloadPositions,
    int OperationsProcessed,
    int DeferredLoadChunks,
    int DeferredUnloadChunks)
{
    public bool BudgetExhausted => DeferredLoadChunks > 0 || DeferredUnloadChunks > 0;

    public static ChunkStreamingUpdateResult Empty { get; } = new(
        new ChunkStreamingPlan(
            new HashSet<ChunkPos>(),
            new HashSet<ChunkPos>(),
            new HashSet<ChunkPos>()),
        0,
        0,
        0,
        0,
        0,
        Array.Empty<ChunkPos>(),
        Array.Empty<ChunkPos>(),
        Array.Empty<ChunkPos>(),
        Array.Empty<ChunkPos>(),
        Array.Empty<ChunkPos>(),
        0,
        0,
        0);
}
