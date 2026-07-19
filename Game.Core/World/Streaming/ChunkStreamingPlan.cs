namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingPlan(
    ChunkWindowSet RequiredChunks,
    IReadOnlySet<ChunkPos> ChunksToLoad,
    IReadOnlySet<ChunkPos> ChunksToUnload);
