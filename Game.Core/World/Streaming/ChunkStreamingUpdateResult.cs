namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingUpdateResult(
    ChunkStreamingPlan Plan,
    int LoadedChunks,
    int GeneratedChunks,
    int SavedChunksBeforeUnload,
    int UnloadedChunks,
    int SkippedDirtyUnloads)
{
    public static ChunkStreamingUpdateResult Empty { get; } = new(
        new ChunkStreamingPlan(
            new HashSet<ChunkPos>(),
            new HashSet<ChunkPos>(),
            new HashSet<ChunkPos>()),
        0,
        0,
        0,
        0,
        0);
}
