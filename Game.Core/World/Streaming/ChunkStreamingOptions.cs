namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingOptions
{
    public int LoadMarginChunks { get; init; } = 1;

    public int UnloadMarginChunks { get; init; } = 3;

    public bool KeepDirtyChunksLoaded { get; init; } = true;
}
