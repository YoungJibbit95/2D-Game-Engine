namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingPlan(
    IReadOnlySet<ChunkPos> RequiredChunks,
    IReadOnlySet<ChunkPos> ChunksToLoad,
    IReadOnlySet<ChunkPos> ChunksToUnload);
