namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingTelemetry(
    int PendingLoadJobs,
    int PendingSaveJobs,
    int ApplyQueueLength,
    int DeferredLoadRequests,
    int DeferredUnloadRequests,
    long QueuedDecodedBytes,
    long LoadedDecodedBytes,
    long GeneratedDecodedBytes,
    long AppliedDecodedBytes,
    long SavedDecodedBytes,
    long LoadOperations,
    long GenerateOperations,
    long ApplyOperations,
    long SaveOperations,
    long UnloadOperations,
    long CancellationRequests,
    long CancelledJobs,
    long StaleResultsRejected,
    long FailedJobs,
    TimeSpan LoadTime,
    TimeSpan GenerateTime,
    TimeSpan ApplyTime,
    TimeSpan SaveTime)
{
    public int PendingRetryJobs { get; init; }

    public long RetryableFailures { get; init; }

    public long PermanentFailures { get; init; }

    public long RetryScheduled { get; init; }

    public long RetryExhausted { get; init; }

    public long RetryBackoffUpdatesScheduled { get; init; }

    public long DeferredApplyItemsByTime { get; init; }

    public long DeferredApplyItemsByBytes { get; init; }

    public long OversizeApplyOperations { get; init; }

    public int TerminalFailuresSuppressed { get; init; }

    public static ChunkStreamingTelemetry Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero);
}
