namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingRetryPolicy
{
    public int MaxAttempts { get; init; } = 3;

    public int InitialBackoffUpdates { get; init; } = 1;

    public int MaxBackoffUpdates { get; init; } = 16;

    public int BackoffMultiplier { get; init; } = 2;

    public int GetBackoffUpdatesAfterFailure(int failedAttempt)
    {
        if (failedAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(failedAttempt));
        }

        var delay = (long)InitialBackoffUpdates;
        for (var exponent = 1; exponent < failedAttempt && delay < MaxBackoffUpdates; exponent++)
        {
            delay = Math.Min((long)MaxBackoffUpdates, delay * BackoffMultiplier);
        }

        return (int)Math.Min(delay, MaxBackoffUpdates);
    }

    internal void Validate()
    {
        if (MaxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "Retry attempts must be at least one.");
        }

        if (InitialBackoffUpdates < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(InitialBackoffUpdates), "Initial retry backoff must be at least one update.");
        }

        if (MaxBackoffUpdates < InitialBackoffUpdates)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBackoffUpdates), "Maximum retry backoff must not be below the initial backoff.");
        }

        if (BackoffMultiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(BackoffMultiplier), "Retry backoff multiplier must be at least one.");
        }
    }
}

public enum ChunkStreamingJobKind
{
    LoadOrGenerate,
    Save
}

public enum ChunkStreamingFailureClassification
{
    Retryable,
    Cancelled,
    Stale,
    Permanent
}

public interface IChunkStreamingFailureClassifier
{
    ChunkStreamingFailureClassification Classify(
        ChunkStreamingJobKind jobKind,
        Exception exception,
        bool cancellationRequested,
        bool isCurrentRequest);
}

public sealed class ChunkStreamingFailureClassifier : IChunkStreamingFailureClassifier
{
    public static ChunkStreamingFailureClassifier Default { get; } = new();

    public ChunkStreamingFailureClassification Classify(
        ChunkStreamingJobKind jobKind,
        Exception exception,
        bool cancellationRequested,
        bool isCurrentRequest)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (!isCurrentRequest)
        {
            return ChunkStreamingFailureClassification.Stale;
        }

        if (cancellationRequested || exception is OperationCanceledException)
        {
            return ChunkStreamingFailureClassification.Cancelled;
        }

        return exception switch
        {
            IOException => ChunkStreamingFailureClassification.Retryable,
            TimeoutException => ChunkStreamingFailureClassification.Retryable,
            UnauthorizedAccessException => ChunkStreamingFailureClassification.Permanent,
            InvalidDataException => ChunkStreamingFailureClassification.Permanent,
            ArgumentException => ChunkStreamingFailureClassification.Permanent,
            NotSupportedException => ChunkStreamingFailureClassification.Permanent,
            _ => ChunkStreamingFailureClassification.Permanent
        };
    }
}
