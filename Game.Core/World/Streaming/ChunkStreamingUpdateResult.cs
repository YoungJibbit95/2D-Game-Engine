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

    public int ApplyOperationsProcessed { get; init; }

    public int CancellationRequests { get; init; }

    public int CancelledJobs { get; init; }

    public int StaleResultsRejected { get; init; }

    public int FailedJobs { get; init; }

    public int RetryableFailures { get; init; }

    public int PermanentFailures { get; init; }

    public int RetryScheduled { get; init; }

    public int RetryExhausted { get; init; }

    public int DeferredApplyItemsByTime { get; init; }

    public int DeferredApplyItemsByBytes { get; init; }

    public long AppliedDecodedBytes { get; init; }

    public int OversizeApplyOperations { get; init; }

    public ChunkStreamingTelemetry Telemetry { get; init; } = ChunkStreamingTelemetry.Empty;

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
