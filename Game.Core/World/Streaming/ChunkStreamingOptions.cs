namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingOptions
{
    public int LoadMarginChunks { get; init; } = 1;

    public int UnloadMarginChunks { get; init; } = 3;

    public bool KeepDirtyChunksLoaded { get; init; } = true;

    public int MaxChunkOperationsPerUpdate { get; init; } = 32;

    public int MaxConcurrentLoadJobs { get; init; } = 4;

    public int MaxConcurrentSaveJobs { get; init; } = 1;

    public int MaxApplyQueueLength { get; init; } = 64;

    public TimeSpan MaxApplyTimePerUpdate { get; init; } = TimeSpan.FromMilliseconds(4);

    public long MaxApplyDecodedBytesPerUpdate { get; init; } = 512 * 1024;

    public ChunkStreamingRetryPolicy RetryPolicy { get; init; } = new();
}
