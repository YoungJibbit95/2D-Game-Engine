using Game.Core.Events;
using Game.Core.Saving;
using Game.Core.World.Generation;

namespace Game.Core.World.Streaming;

public sealed class ChunkStreamingService
{
    private readonly ChunkStreamingPlanner _planner;
    private readonly IChunkStreamingJobRunner _jobs;
    private readonly IChunkStreamingFailureClassifier _failureClassifier;
    private readonly IChunkStreamingTimeSource _timeSource;
    private readonly List<PendingLoadJob> _pendingLoads = new();
    private readonly List<PendingSaveJob> _pendingSaves = new();
    private readonly List<ScheduledLoadRetry> _scheduledLoadRetries = new();
    private readonly List<ScheduledSaveRetry> _scheduledSaveRetries = new();
    private readonly Queue<ApplyWorkItem> _applyQueue = new();
    private readonly HashSet<StreamingJobKey> _queuedLoads = new();
    private readonly HashSet<StreamingJobKey> _queuedUnloads = new();
    private readonly HashSet<StreamingJobKey> _terminalLoadFailures = new();
    private readonly HashSet<StreamingJobKey> _terminalSaveFailures = new();
    private World? _activeWorld;
    private ChunkStreamingRequestSnapshot? _latestRequest;
    private long _worldSessionGeneration;
    private long _requestSequence;
    private long _applyQueueBytes;
    private long _loadedDecodedBytes;
    private long _generatedDecodedBytes;
    private long _appliedDecodedBytes;
    private long _savedDecodedBytes;
    private long _loadOperations;
    private long _generateOperations;
    private long _applyOperations;
    private long _saveOperations;
    private long _unloadOperations;
    private long _cancellationRequests;
    private long _cancelledJobs;
    private long _staleResultsRejected;
    private long _failedJobs;
    private long _retryableFailures;
    private long _permanentFailures;
    private long _retryScheduled;
    private long _retryExhausted;
    private long _retryBackoffUpdatesScheduled;
    private long _applyDeferredByTime;
    private long _applyDeferredByBytes;
    private long _oversizeApplyOperations;
    private TimeSpan _loadTime;
    private TimeSpan _generateTime;
    private TimeSpan _applyTime;
    private TimeSpan _saveTime;

    public ChunkStreamingService()
        : this(
            new ChunkStreamingPlanner(),
            new ChunkStreamingJobRunner(
                new InfiniteWorldChunkGenerator(),
                new WorldSaveService(WorldChunkStorageMode.RegionFiles)),
            ChunkStreamingFailureClassifier.Default,
            StopwatchChunkStreamingTimeSource.Instance)
    {
    }

    public ChunkStreamingService(
        ChunkStreamingPlanner? planner = null,
        InfiniteWorldChunkGenerator? generator = null,
        WorldSaveService? saves = null)
        : this(
            planner ?? new ChunkStreamingPlanner(),
            new ChunkStreamingJobRunner(
                generator ?? new InfiniteWorldChunkGenerator(),
                saves ?? new WorldSaveService(WorldChunkStorageMode.RegionFiles)),
            ChunkStreamingFailureClassifier.Default,
            StopwatchChunkStreamingTimeSource.Instance)
    {
    }

    public ChunkStreamingService(
        IChunkStreamingJobRunner jobs,
        ChunkStreamingPlanner? planner = null,
        IChunkStreamingFailureClassifier? failureClassifier = null,
        IChunkStreamingTimeSource? timeSource = null)
        : this(
            planner ?? new ChunkStreamingPlanner(),
            jobs,
            failureClassifier ?? ChunkStreamingFailureClassifier.Default,
            timeSource ?? StopwatchChunkStreamingTimeSource.Instance)
    {
    }

    private ChunkStreamingService(
        ChunkStreamingPlanner planner,
        IChunkStreamingJobRunner jobs,
        IChunkStreamingFailureClassifier failureClassifier,
        IChunkStreamingTimeSource timeSource)
    {
        _planner = planner;
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _failureClassifier = failureClassifier ?? throw new ArgumentNullException(nameof(failureClassifier));
        _timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
    }

    public long WorldSessionGeneration => _worldSessionGeneration;

    public ChunkStreamingRequestSnapshot? LatestRequest => _latestRequest;

    public ChunkStreamingTelemetry Telemetry => BuildTelemetry(0, 0);

    public ChunkStreamingUpdateResult Update(
        World world,
        WorldGenerationProfile profile,
        RectI visibleTileArea,
        string? worldDirectory = null,
        ChunkStreamingOptions? options = null,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(profile);

        if (!world.IsHorizontallyInfinite)
        {
            throw new InvalidOperationException("Chunk streaming service requires a horizontally infinite world.");
        }

        var resolvedOptions = options ?? new ChunkStreamingOptions();
        ChunkStreamingPlanner.ValidateOptions(resolvedOptions);
        var update = new UpdateAccumulator();

        EnsureWorldSession(world, update);
        var request = _planner.CreateRequestSnapshot(
            world,
            visibleTileArea,
            _worldSessionGeneration,
            ++_requestSequence,
            resolvedOptions);
        _latestRequest = request;

        CancelIrrelevantJobs(request, update);
        DiscardIrrelevantRetries(request, update);
        HarvestCompletedJobs(request, resolvedOptions, update);

        var applyBudget = new ApplyBudgetState(resolvedOptions);
        ApplyQueuedWork(world, request, ref applyBudget, events, update);

        ScheduleLoadJobs(world, profile, worldDirectory, request, resolvedOptions);
        HarvestCompletedJobs(request, resolvedOptions, update);

        ScheduleUnloadWork(world, worldDirectory, request, resolvedOptions, events, update);
        HarvestCompletedJobs(request, resolvedOptions, update);

        if (applyBudget.OperationsProcessed < resolvedOptions.MaxChunkOperationsPerUpdate)
        {
            ApplyQueuedWork(world, request, ref applyBudget, events, update);
        }

        var deferredLoads = CountDeferredLoads(world, request);
        var deferredUnloads = CountDeferredUnloads(world, request);
        var telemetry = BuildTelemetry(deferredLoads, deferredUnloads);

        return new ChunkStreamingUpdateResult(
            request.ToPlan(),
            update.LoadedPositions.Count,
            update.GeneratedPositions.Count,
            update.SavedPositions.Count,
            update.UnloadedPositions.Count,
            update.SkippedDirtyUnloadPositions.Count,
            update.LoadedPositions,
            update.GeneratedPositions,
            update.SavedPositions,
            update.UnloadedPositions,
            update.SkippedDirtyUnloadPositions,
            update.ApplyItemsProcessed,
            deferredLoads,
            deferredUnloads)
        {
            ApplyOperationsProcessed = update.ApplyItemsProcessed,
            CancellationRequests = update.CancellationRequests,
            CancelledJobs = update.CancelledJobs,
            StaleResultsRejected = update.StaleResultsRejected,
            FailedJobs = update.FailedJobs,
            RetryableFailures = update.RetryableFailures,
            PermanentFailures = update.PermanentFailures,
            RetryScheduled = update.RetryScheduled,
            RetryExhausted = update.RetryExhausted,
            DeferredApplyItemsByTime = update.DeferredApplyItemsByTime,
            DeferredApplyItemsByBytes = update.DeferredApplyItemsByBytes,
            AppliedDecodedBytes = update.AppliedDecodedBytes,
            OversizeApplyOperations = update.OversizeApplyOperations,
            Telemetry = telemetry
        };
    }

    public void CancelPendingJobs()
    {
        var update = new UpdateAccumulator();
        foreach (var pending in _pendingLoads)
        {
            RequestCancellation(pending, update);
        }

        foreach (var pending in _pendingSaves)
        {
            RequestCancellation(pending, update);
        }

        var scheduledCount = _scheduledLoadRetries.Count + _scheduledSaveRetries.Count;
        _scheduledLoadRetries.Clear();
        _scheduledSaveRetries.Clear();
        _cancellationRequests += scheduledCount;
    }

    public void ResetTerminalFailures()
    {
        _terminalLoadFailures.Clear();
        _terminalSaveFailures.Clear();
    }

    private void EnsureWorldSession(World world, UpdateAccumulator update)
    {
        if (ReferenceEquals(_activeWorld, world))
        {
            return;
        }

        if (_activeWorld is not null)
        {
            foreach (var pending in _pendingLoads)
            {
                RequestCancellation(pending, update);
            }

            foreach (var pending in _pendingSaves)
            {
                RequestCancellation(pending, update);
            }

            while (_applyQueue.Count > 0)
            {
                var stale = _applyQueue.Dequeue();
                RemoveQueuedKey(stale);
                _applyQueueBytes -= stale.DecodedBytes;
                RecordStale(update);
            }

            for (var index = _scheduledLoadRetries.Count - 1; index >= 0; index--)
            {
                _scheduledLoadRetries.RemoveAt(index);
                RecordStale(update);
            }

            for (var index = _scheduledSaveRetries.Count - 1; index >= 0; index--)
            {
                _scheduledSaveRetries.RemoveAt(index);
                RecordStale(update);
            }
        }

        _activeWorld = world;
        _latestRequest = null;
        _terminalLoadFailures.Clear();
        _terminalSaveFailures.Clear();
        _worldSessionGeneration++;
    }

    private void CancelIrrelevantJobs(ChunkStreamingRequestSnapshot request, UpdateAccumulator update)
    {
        foreach (var pending in _pendingLoads)
        {
            if (pending.Request.WorldSessionGeneration != request.WorldSessionGeneration ||
                !request.RequiredChunks.Contains(pending.Request.Position))
            {
                RequestCancellation(pending, update);
            }
        }

        foreach (var pending in _pendingSaves)
        {
            if (pending.Request.WorldSessionGeneration != request.WorldSessionGeneration ||
                !request.ChunksToUnload.Contains(pending.Request.Chunk.Position))
            {
                RequestCancellation(pending, update);
            }
        }
    }

    private void DiscardIrrelevantRetries(ChunkStreamingRequestSnapshot request, UpdateAccumulator update)
    {
        for (var index = _scheduledLoadRetries.Count - 1; index >= 0; index--)
        {
            var retry = _scheduledLoadRetries[index];
            if (retry.Request.WorldSessionGeneration == request.WorldSessionGeneration &&
                request.RequiredChunks.Contains(retry.Request.Position))
            {
                continue;
            }

            _scheduledLoadRetries.RemoveAt(index);
            RecordStale(update);
        }

        for (var index = _scheduledSaveRetries.Count - 1; index >= 0; index--)
        {
            var retry = _scheduledSaveRetries[index];
            if (retry.Request.WorldSessionGeneration == request.WorldSessionGeneration &&
                request.ChunksToUnload.Contains(retry.Request.Chunk.Position))
            {
                continue;
            }

            _scheduledSaveRetries.RemoveAt(index);
            RecordStale(update);
        }
    }

    private void ScheduleLoadJobs(
        World world,
        WorldGenerationProfile profile,
        string? worldDirectory,
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingOptions options)
    {
        var activeLoads = CountActiveLoads();
        for (var index = 0; index < _scheduledLoadRetries.Count && activeLoads < options.MaxConcurrentLoadJobs;)
        {
            var retry = _scheduledLoadRetries[index];
            if (retry.NotBeforeRequestSequence > request.RequestSequence)
            {
                index++;
                continue;
            }

            var key = new StreamingJobKey(request.WorldSessionGeneration, retry.Request.Position);
            if (world.TryGetChunk(retry.Request.Position, out _) || _queuedLoads.Contains(key))
            {
                _scheduledLoadRetries.RemoveAt(index);
                continue;
            }

            _scheduledLoadRetries.RemoveAt(index);
            StartLoadJob(retry.Request with { RequestSequence = request.RequestSequence }, retry.Attempt);
            activeLoads++;
        }

        foreach (var position in request.ChunksToLoad)
        {
            if (activeLoads >= options.MaxConcurrentLoadJobs)
            {
                break;
            }

            var key = new StreamingJobKey(request.WorldSessionGeneration, position);
            if (world.TryGetChunk(position, out _) ||
                HasPendingLoad(key) ||
                HasScheduledLoadRetry(key) ||
                _terminalLoadFailures.Contains(key) ||
                _queuedLoads.Contains(key))
            {
                continue;
            }

            var jobRequest = new ChunkStreamingLoadJobRequest(
                request.WorldSessionGeneration,
                request.RequestSequence,
                position,
                world.WidthTiles,
                world.HeightTiles,
                world.IsHorizontallyInfinite,
                world.Metadata,
                profile,
                worldDirectory);
            StartLoadJob(jobRequest, attempt: 1);
            activeLoads++;
        }
    }

    private void StartLoadJob(ChunkStreamingLoadJobRequest request, int attempt)
    {
        var cancellation = new CancellationTokenSource();
        Task<ChunkStreamingLoadJobResult> task;
        try
        {
            task = _jobs.RunLoadOrGenerateAsync(request, cancellation.Token) ??
                Task.FromException<ChunkStreamingLoadJobResult>(
                    new InvalidOperationException("Chunk streaming load runner returned a null task."));
        }
        catch (Exception exception)
        {
            task = Task.FromException<ChunkStreamingLoadJobResult>(exception);
        }

        _pendingLoads.Add(new PendingLoadJob(request, task, cancellation, attempt));
    }

    private void ScheduleUnloadWork(
        World world,
        string? worldDirectory,
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingOptions options,
        GameEventBus? events,
        UpdateAccumulator update)
    {
        var activeSaves = CountActiveSaves();
        for (var index = 0; index < _scheduledSaveRetries.Count && activeSaves < options.MaxConcurrentSaveJobs;)
        {
            var retry = _scheduledSaveRetries[index];
            if (retry.NotBeforeRequestSequence > request.RequestSequence)
            {
                index++;
                continue;
            }

            var key = new StreamingJobKey(request.WorldSessionGeneration, retry.Request.Chunk.Position);
            if (_queuedUnloads.Contains(key))
            {
                _scheduledSaveRetries.RemoveAt(index);
                continue;
            }

            _scheduledSaveRetries.RemoveAt(index);
            StartSaveJob(retry.Request with { RequestSequence = request.RequestSequence }, retry.Attempt);
            activeSaves++;
        }

        foreach (var position in request.ChunksToUnload)
        {
            if (!world.TryGetChunk(position, out var chunk) || chunk is null)
            {
                continue;
            }

            var key = new StreamingJobKey(request.WorldSessionGeneration, position);
            if (_queuedUnloads.Contains(key) ||
                HasPendingSave(key) ||
                HasScheduledSaveRetry(key) ||
                _terminalSaveFailures.Contains(key))
            {
                continue;
            }

            if (!chunk.IsDirty)
            {
                if (_applyQueue.Count >= options.MaxApplyQueueLength)
                {
                    continue;
                }

                Enqueue(ApplyWorkItem.CleanUnload(request, position));
                continue;
            }

            if (string.IsNullOrWhiteSpace(worldDirectory))
            {
                update.RecordSkippedDirtyUnload(position);
                events?.Publish(new ChunkUnloadSkippedEvent(position, "dirty_without_save_directory"));
                continue;
            }

            if (activeSaves >= options.MaxConcurrentSaveJobs)
            {
                continue;
            }

            var saveRequest = new ChunkStreamingSaveJobRequest(
                request.WorldSessionGeneration,
                request.RequestSequence,
                world.WidthTiles,
                world.HeightTiles,
                world.IsHorizontallyInfinite,
                world.Metadata,
                worldDirectory,
                ChunkStreamingChunkSnapshot.Capture(chunk));
            StartSaveJob(saveRequest, attempt: 1);
            activeSaves++;
        }
    }

    private void StartSaveJob(ChunkStreamingSaveJobRequest request, int attempt)
    {
        var cancellation = new CancellationTokenSource();
        Task<ChunkStreamingSaveJobResult> task;
        try
        {
            task = _jobs.RunSaveAsync(request, cancellation.Token) ??
                Task.FromException<ChunkStreamingSaveJobResult>(
                    new InvalidOperationException("Chunk streaming save runner returned a null task."));
        }
        catch (Exception exception)
        {
            task = Task.FromException<ChunkStreamingSaveJobResult>(exception);
        }

        _pendingSaves.Add(new PendingSaveJob(request, task, cancellation, attempt));
    }

    private void HarvestCompletedJobs(
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingOptions options,
        UpdateAccumulator update)
    {
        for (var index = 0; index < _pendingLoads.Count;)
        {
            var pending = _pendingLoads[index];
            if (!pending.Task.IsCompleted)
            {
                index++;
                continue;
            }

            if (pending.Task.IsCanceled || (pending.Task.IsFaulted && pending.Cancellation.IsCancellationRequested))
            {
                RecordCancelled(update);
                RemovePendingLoad(index, pending);
                continue;
            }

            if (pending.Task.IsFaulted)
            {
                var exception = pending.Task.Exception?.GetBaseException() ??
                    new InvalidOperationException("Chunk streaming load failed without an exception.");
                HandleLoadFailure(pending, request, options.RetryPolicy, exception, update);
                RemovePendingLoad(index, pending);
                continue;
            }

            if (_applyQueue.Count >= options.MaxApplyQueueLength)
            {
                index++;
                continue;
            }

            var result = pending.Task.GetAwaiter().GetResult();
            RecordLoadJob(result);
            RemovePendingLoad(index, pending);
            if (result.Request.WorldSessionGeneration != request.WorldSessionGeneration ||
                !request.RequiredChunks.Contains(result.Request.Position))
            {
                RecordStale(update);
                continue;
            }

            Enqueue(ApplyWorkItem.Load(result));
        }

        for (var index = 0; index < _pendingSaves.Count;)
        {
            var pending = _pendingSaves[index];
            if (!pending.Task.IsCompleted)
            {
                index++;
                continue;
            }

            if (pending.Task.IsCanceled || (pending.Task.IsFaulted && pending.Cancellation.IsCancellationRequested))
            {
                RecordCancelled(update);
                RemovePendingSave(index, pending);
                continue;
            }

            if (pending.Task.IsFaulted)
            {
                var exception = pending.Task.Exception?.GetBaseException() ??
                    new InvalidOperationException("Chunk streaming save failed without an exception.");
                HandleSaveFailure(pending, request, options.RetryPolicy, exception, update);
                RemovePendingSave(index, pending);
                continue;
            }

            if (_applyQueue.Count >= options.MaxApplyQueueLength)
            {
                index++;
                continue;
            }

            var result = pending.Task.GetAwaiter().GetResult();
            RecordSaveJob(result);
            RemovePendingSave(index, pending);
            if (result.Request.WorldSessionGeneration != request.WorldSessionGeneration ||
                !request.ChunksToUnload.Contains(result.Request.Chunk.Position))
            {
                RecordStale(update);
                continue;
            }

            if (!result.Succeeded)
            {
                HandleRetryableSaveFailure(pending, request, options.RetryPolicy, update);
                continue;
            }

            Enqueue(ApplyWorkItem.SavedUnload(result));
        }
    }

    private void HandleLoadFailure(
        PendingLoadJob pending,
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingRetryPolicy retryPolicy,
        Exception exception,
        UpdateAccumulator update)
    {
        var isCurrent = pending.Request.WorldSessionGeneration == request.WorldSessionGeneration &&
            request.RequiredChunks.Contains(pending.Request.Position);
        var classification = _failureClassifier.Classify(
            ChunkStreamingJobKind.LoadOrGenerate,
            exception,
            pending.Cancellation.IsCancellationRequested,
            isCurrent);
        HandleFailureClassification(
            classification,
            pending.Attempt,
            retryPolicy,
            () => _scheduledLoadRetries.Add(new ScheduledLoadRetry(
                pending.Request,
                pending.Attempt + 1,
                GetRetrySequence(request.RequestSequence, retryPolicy, pending.Attempt))),
            () => _terminalLoadFailures.Add(new StreamingJobKey(
                pending.Request.WorldSessionGeneration,
                pending.Request.Position)),
            update);
    }

    private void HandleSaveFailure(
        PendingSaveJob pending,
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingRetryPolicy retryPolicy,
        Exception exception,
        UpdateAccumulator update)
    {
        var isCurrent = pending.Request.WorldSessionGeneration == request.WorldSessionGeneration &&
            request.ChunksToUnload.Contains(pending.Request.Chunk.Position);
        var classification = _failureClassifier.Classify(
            ChunkStreamingJobKind.Save,
            exception,
            pending.Cancellation.IsCancellationRequested,
            isCurrent);
        HandleFailureClassification(
            classification,
            pending.Attempt,
            retryPolicy,
            () => _scheduledSaveRetries.Add(new ScheduledSaveRetry(
                pending.Request,
                pending.Attempt + 1,
                GetRetrySequence(request.RequestSequence, retryPolicy, pending.Attempt))),
            () => _terminalSaveFailures.Add(new StreamingJobKey(
                pending.Request.WorldSessionGeneration,
                pending.Request.Chunk.Position)),
            update);
    }

    private void HandleRetryableSaveFailure(
        PendingSaveJob pending,
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingRetryPolicy retryPolicy,
        UpdateAccumulator update)
    {
        HandleFailureClassification(
            ChunkStreamingFailureClassification.Retryable,
            pending.Attempt,
            retryPolicy,
            () => _scheduledSaveRetries.Add(new ScheduledSaveRetry(
                pending.Request,
                pending.Attempt + 1,
                GetRetrySequence(request.RequestSequence, retryPolicy, pending.Attempt))),
            () => _terminalSaveFailures.Add(new StreamingJobKey(
                pending.Request.WorldSessionGeneration,
                pending.Request.Chunk.Position)),
            update);
    }

    private void HandleFailureClassification(
        ChunkStreamingFailureClassification classification,
        int failedAttempt,
        ChunkStreamingRetryPolicy retryPolicy,
        Action scheduleRetry,
        Action recordTerminalFailure,
        UpdateAccumulator update)
    {
        switch (classification)
        {
            case ChunkStreamingFailureClassification.Cancelled:
                RecordCancelled(update);
                return;
            case ChunkStreamingFailureClassification.Stale:
                RecordStale(update);
                return;
            case ChunkStreamingFailureClassification.Permanent:
                RecordPermanentFailure(update);
                recordTerminalFailure();
                return;
            case ChunkStreamingFailureClassification.Retryable:
                RecordRetryableFailure(update);
                if (failedAttempt >= retryPolicy.MaxAttempts)
                {
                    RecordRetryExhausted(update);
                    recordTerminalFailure();
                    return;
                }

                scheduleRetry();
                var backoff = retryPolicy.GetBackoffUpdatesAfterFailure(failedAttempt);
                _retryScheduled++;
                _retryBackoffUpdatesScheduled += backoff;
                update.RetryScheduled++;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(classification));
        }
    }

    private static long GetRetrySequence(
        long currentRequestSequence,
        ChunkStreamingRetryPolicy retryPolicy,
        int failedAttempt)
    {
        var delay = retryPolicy.GetBackoffUpdatesAfterFailure(failedAttempt);
        return currentRequestSequence > long.MaxValue - delay
            ? long.MaxValue
            : currentRequestSequence + delay;
    }

    private void ApplyQueuedWork(
        World world,
        ChunkStreamingRequestSnapshot request,
        ref ApplyBudgetState budget,
        GameEventBus? events,
        UpdateAccumulator update)
    {
        while (budget.OperationsProcessed < budget.MaxOperations && _applyQueue.Count > 0)
        {
            if (budget.OperationsProcessed > 0 && budget.Elapsed >= budget.MaxElapsed)
            {
                RecordApplyTimeDeferral(_applyQueue.Count, ref budget, update);
                break;
            }

            var next = _applyQueue.Peek();
            var exceedsByteBudget = next.DecodedBytes > 0 &&
                next.DecodedBytes > budget.MaxDecodedBytes - Math.Min(budget.DecodedBytesConsumed, budget.MaxDecodedBytes);
            if (budget.OperationsProcessed > 0 && exceedsByteBudget)
            {
                RecordApplyByteDeferral(_applyQueue.Count, ref budget, update);
                break;
            }

            var work = _applyQueue.Dequeue();
            RemoveQueuedKey(work);
            _applyQueueBytes -= work.DecodedBytes;
            if (exceedsByteBudget)
            {
                _oversizeApplyOperations++;
                update.OversizeApplyOperations++;
            }

            budget.DecodedBytesConsumed = SaturatingAdd(budget.DecodedBytesConsumed, work.DecodedBytes);
            budget.OperationsProcessed++;
            update.ApplyItemsProcessed++;
            _applyOperations++;

            if (!ReferenceEquals(_activeWorld, world) || work.WorldSessionGeneration != request.WorldSessionGeneration)
            {
                RecordStale(update);
                continue;
            }

            var appliedBytesBefore = _appliedDecodedBytes;
            var applyStarted = _timeSource.GetTimestamp();
            switch (work.Kind)
            {
                case ApplyWorkKind.Load:
                    ApplyLoadedChunk(world, request, work.LoadResult!, events, update);
                    break;
                case ApplyWorkKind.CleanUnload:
                    ApplyCleanUnload(world, request, work.Position, events, update);
                    break;
                case ApplyWorkKind.SavedUnload:
                    ApplySavedUnload(world, request, work.SaveResult!, events, update);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown streaming apply work kind '{work.Kind}'.");
            }

            var elapsed = _timeSource.GetElapsedTime(applyStarted);
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            budget.Elapsed += elapsed;
            _applyTime += elapsed;
            var appliedBytes = _appliedDecodedBytes - appliedBytesBefore;
            update.AppliedDecodedBytes += appliedBytes;
        }
    }

    private void RecordApplyTimeDeferral(
        int deferredItems,
        ref ApplyBudgetState budget,
        UpdateAccumulator update)
    {
        if (budget.RecordedTimeDeferral)
        {
            return;
        }

        budget.RecordedTimeDeferral = true;
        _applyDeferredByTime += deferredItems;
        update.DeferredApplyItemsByTime += deferredItems;
    }

    private void RecordApplyByteDeferral(
        int deferredItems,
        ref ApplyBudgetState budget,
        UpdateAccumulator update)
    {
        if (budget.RecordedByteDeferral)
        {
            return;
        }

        budget.RecordedByteDeferral = true;
        _applyDeferredByBytes += deferredItems;
        update.DeferredApplyItemsByBytes += deferredItems;
    }

    private static long SaturatingAdd(long first, long second)
    {
        return first > long.MaxValue - second ? long.MaxValue : first + second;
    }

    private void ApplyLoadedChunk(
        World world,
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingLoadJobResult result,
        GameEventBus? events,
        UpdateAccumulator update)
    {
        var position = result.Request.Position;
        if (!request.RequiredChunks.Contains(position))
        {
            RecordStale(update);
            return;
        }

        if (world.TryGetChunk(position, out _))
        {
            return;
        }

        result.Chunk.ApplyTo(world.GetOrCreateChunk(position));
        _appliedDecodedBytes += result.DecodedBytes;
        if (result.LoadedFromSave)
        {
            update.RecordLoaded(position);
            events?.Publish(new ChunkLoadedEvent(position, LoadedFromSave: true));
        }
        else
        {
            update.RecordGenerated(position);
            events?.Publish(new ChunkGeneratedEvent(position));
        }
    }

    private void ApplyCleanUnload(
        World world,
        ChunkStreamingRequestSnapshot request,
        ChunkPos position,
        GameEventBus? events,
        UpdateAccumulator update)
    {
        if (!request.ChunksToUnload.Contains(position))
        {
            RecordStale(update);
            return;
        }

        if (!world.TryGetChunk(position, out var chunk) || chunk is null || chunk.IsDirty)
        {
            return;
        }

        if (world.UnloadChunk(position, requireClean: true))
        {
            _unloadOperations++;
            update.RecordUnloaded(position);
            events?.Publish(new ChunkUnloadedEvent(position));
        }
    }

    private void ApplySavedUnload(
        World world,
        ChunkStreamingRequestSnapshot request,
        ChunkStreamingSaveJobResult result,
        GameEventBus? events,
        UpdateAccumulator update)
    {
        var position = result.Request.Chunk.Position;
        if (!request.ChunksToUnload.Contains(position))
        {
            RecordStale(update);
            return;
        }

        if (!result.Succeeded)
        {
            return;
        }

        update.RecordSaved(position);
        events?.Publish(new ChunkSavedEvent(position, SavedBeforeUnload: true));

        if (!world.TryGetChunk(position, out var chunk) ||
            chunk is null ||
            !result.Request.Chunk.Matches(chunk))
        {
            return;
        }

        chunk.ClearDirtyFlags();
        if (world.UnloadChunk(position, requireClean: true))
        {
            _unloadOperations++;
            update.RecordUnloaded(position);
            events?.Publish(new ChunkUnloadedEvent(position));
        }
    }

    private void Enqueue(ApplyWorkItem work)
    {
        _applyQueue.Enqueue(work);
        _applyQueueBytes += work.DecodedBytes;
        var key = new StreamingJobKey(work.WorldSessionGeneration, work.Position);
        if (work.Kind == ApplyWorkKind.Load)
        {
            _queuedLoads.Add(key);
        }
        else
        {
            _queuedUnloads.Add(key);
        }
    }

    private void RemoveQueuedKey(ApplyWorkItem work)
    {
        var key = new StreamingJobKey(work.WorldSessionGeneration, work.Position);
        if (work.Kind == ApplyWorkKind.Load)
        {
            _queuedLoads.Remove(key);
        }
        else
        {
            _queuedUnloads.Remove(key);
        }
    }

    private void RemovePendingLoad(int index, PendingLoadJob pending)
    {
        _pendingLoads.RemoveAt(index);
        pending.Cancellation.Dispose();
    }

    private void RemovePendingSave(int index, PendingSaveJob pending)
    {
        _pendingSaves.RemoveAt(index);
        pending.Cancellation.Dispose();
    }

    private void RequestCancellation(PendingLoadJob pending, UpdateAccumulator update)
    {
        if (pending.Cancellation.IsCancellationRequested)
        {
            return;
        }

        pending.Cancellation.Cancel();
        _cancellationRequests++;
        update.CancellationRequests++;
    }

    private void RequestCancellation(PendingSaveJob pending, UpdateAccumulator update)
    {
        if (pending.Cancellation.IsCancellationRequested)
        {
            return;
        }

        pending.Cancellation.Cancel();
        _cancellationRequests++;
        update.CancellationRequests++;
    }

    private void RecordCancelled(UpdateAccumulator update)
    {
        _cancelledJobs++;
        update.CancelledJobs++;
    }

    private void RecordStale(UpdateAccumulator update)
    {
        _staleResultsRejected++;
        update.StaleResultsRejected++;
    }

    private void RecordRetryableFailure(UpdateAccumulator update)
    {
        _failedJobs++;
        _retryableFailures++;
        update.FailedJobs++;
        update.RetryableFailures++;
    }

    private void RecordPermanentFailure(UpdateAccumulator update)
    {
        _failedJobs++;
        _permanentFailures++;
        update.FailedJobs++;
        update.PermanentFailures++;
    }

    private void RecordRetryExhausted(UpdateAccumulator update)
    {
        _retryExhausted++;
        update.RetryExhausted++;
    }

    private void RecordLoadJob(ChunkStreamingLoadJobResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Request.WorldDirectory))
        {
            _loadOperations++;
            _loadTime += result.LoadTime;
        }

        if (result.LoadedFromSave)
        {
            _loadedDecodedBytes += result.DecodedBytes;
        }
        else
        {
            _generateOperations++;
            _generatedDecodedBytes += result.DecodedBytes;
            _generateTime += result.GenerateTime;
        }
    }

    private void RecordSaveJob(ChunkStreamingSaveJobResult result)
    {
        _saveOperations++;
        if (result.Succeeded)
        {
            _savedDecodedBytes += result.DecodedBytes;
        }

        _saveTime += result.SaveTime;
    }

    private int CountActiveLoads()
    {
        var count = 0;
        foreach (var pending in _pendingLoads)
        {
            if (!pending.Task.IsCompleted)
            {
                count++;
            }
        }

        return count;
    }

    private int CountActiveSaves()
    {
        var count = 0;
        foreach (var pending in _pendingSaves)
        {
            if (!pending.Task.IsCompleted)
            {
                count++;
            }
        }

        return count;
    }

    private bool HasPendingLoad(StreamingJobKey key)
    {
        foreach (var pending in _pendingLoads)
        {
            if (pending.Request.WorldSessionGeneration == key.WorldSessionGeneration &&
                pending.Request.Position == key.Position &&
                !pending.Cancellation.IsCancellationRequested)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPendingSave(StreamingJobKey key)
    {
        foreach (var pending in _pendingSaves)
        {
            if (pending.Request.WorldSessionGeneration == key.WorldSessionGeneration &&
                pending.Request.Chunk.Position == key.Position &&
                !pending.Cancellation.IsCancellationRequested)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasScheduledLoadRetry(StreamingJobKey key)
    {
        foreach (var retry in _scheduledLoadRetries)
        {
            if (retry.Request.WorldSessionGeneration == key.WorldSessionGeneration &&
                retry.Request.Position == key.Position)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasScheduledSaveRetry(StreamingJobKey key)
    {
        foreach (var retry in _scheduledSaveRetries)
        {
            if (retry.Request.WorldSessionGeneration == key.WorldSessionGeneration &&
                retry.Request.Chunk.Position == key.Position)
            {
                return true;
            }
        }

        return false;
    }

    private int CountDeferredLoads(World world, ChunkStreamingRequestSnapshot request)
    {
        var deferred = 0;
        foreach (var position in request.ChunksToLoad)
        {
            if (!world.TryGetChunk(position, out _))
            {
                deferred++;
            }
        }

        return deferred;
    }

    private static int CountDeferredUnloads(World world, ChunkStreamingRequestSnapshot request)
    {
        var deferred = 0;
        foreach (var position in request.ChunksToUnload)
        {
            if (world.TryGetChunk(position, out _))
            {
                deferred++;
            }
        }

        return deferred;
    }

    private ChunkStreamingTelemetry BuildTelemetry(int deferredLoads, int deferredUnloads)
    {
        var pendingLoadJobs = 0;
        var pendingSaveJobs = 0;
        var queuedBytes = _applyQueueBytes;
        foreach (var pending in _pendingLoads)
        {
            pendingLoadJobs++;
            if (pending.Task.IsCompletedSuccessfully)
            {
                queuedBytes += pending.Task.Result.DecodedBytes;
            }
        }

        foreach (var pending in _pendingSaves)
        {
            pendingSaveJobs++;
            queuedBytes += pending.Request.Chunk.DecodedBytes;
        }

        return new ChunkStreamingTelemetry(
            pendingLoadJobs,
            pendingSaveJobs,
            _applyQueue.Count,
            deferredLoads,
            deferredUnloads,
            queuedBytes,
            _loadedDecodedBytes,
            _generatedDecodedBytes,
            _appliedDecodedBytes,
            _savedDecodedBytes,
            _loadOperations,
            _generateOperations,
            _applyOperations,
            _saveOperations,
            _unloadOperations,
            _cancellationRequests,
            _cancelledJobs,
            _staleResultsRejected,
            _failedJobs,
            _loadTime,
            _generateTime,
            _applyTime,
            _saveTime)
        {
            PendingRetryJobs = _scheduledLoadRetries.Count + _scheduledSaveRetries.Count,
            RetryableFailures = _retryableFailures,
            PermanentFailures = _permanentFailures,
            RetryScheduled = _retryScheduled,
            RetryExhausted = _retryExhausted,
            RetryBackoffUpdatesScheduled = _retryBackoffUpdatesScheduled,
            DeferredApplyItemsByTime = _applyDeferredByTime,
            DeferredApplyItemsByBytes = _applyDeferredByBytes,
            OversizeApplyOperations = _oversizeApplyOperations,
            TerminalFailuresSuppressed = _terminalLoadFailures.Count + _terminalSaveFailures.Count
        };
    }

    private sealed record PendingLoadJob(
        ChunkStreamingLoadJobRequest Request,
        Task<ChunkStreamingLoadJobResult> Task,
        CancellationTokenSource Cancellation,
        int Attempt);

    private sealed record PendingSaveJob(
        ChunkStreamingSaveJobRequest Request,
        Task<ChunkStreamingSaveJobResult> Task,
        CancellationTokenSource Cancellation,
        int Attempt);

    private sealed record ScheduledLoadRetry(
        ChunkStreamingLoadJobRequest Request,
        int Attempt,
        long NotBeforeRequestSequence);

    private sealed record ScheduledSaveRetry(
        ChunkStreamingSaveJobRequest Request,
        int Attempt,
        long NotBeforeRequestSequence);

    private readonly record struct StreamingJobKey(long WorldSessionGeneration, ChunkPos Position);

    private enum ApplyWorkKind
    {
        Load,
        CleanUnload,
        SavedUnload
    }

    private sealed record ApplyWorkItem(
        ApplyWorkKind Kind,
        long WorldSessionGeneration,
        long RequestSequence,
        ChunkPos Position,
        ChunkStreamingLoadJobResult? LoadResult,
        ChunkStreamingSaveJobResult? SaveResult,
        long DecodedBytes)
    {
        public static ApplyWorkItem Load(ChunkStreamingLoadJobResult result)
        {
            return new ApplyWorkItem(
                ApplyWorkKind.Load,
                result.Request.WorldSessionGeneration,
                result.Request.RequestSequence,
                result.Request.Position,
                result,
                null,
                result.DecodedBytes);
        }

        public static ApplyWorkItem CleanUnload(ChunkStreamingRequestSnapshot request, ChunkPos position)
        {
            return new ApplyWorkItem(
                ApplyWorkKind.CleanUnload,
                request.WorldSessionGeneration,
                request.RequestSequence,
                position,
                null,
                null,
                0);
        }

        public static ApplyWorkItem SavedUnload(ChunkStreamingSaveJobResult result)
        {
            return new ApplyWorkItem(
                ApplyWorkKind.SavedUnload,
                result.Request.WorldSessionGeneration,
                result.Request.RequestSequence,
                result.Request.Chunk.Position,
                null,
                result,
                result.DecodedBytes);
        }
    }

    private struct ApplyBudgetState
    {
        public ApplyBudgetState(ChunkStreamingOptions options)
        {
            MaxOperations = options.MaxChunkOperationsPerUpdate;
            MaxElapsed = options.MaxApplyTimePerUpdate;
            MaxDecodedBytes = options.MaxApplyDecodedBytesPerUpdate;
        }

        public int MaxOperations { get; }

        public TimeSpan MaxElapsed { get; }

        public long MaxDecodedBytes { get; }

        public int OperationsProcessed { get; set; }

        public long DecodedBytesConsumed { get; set; }

        public TimeSpan Elapsed { get; set; }

        public bool RecordedTimeDeferral { get; set; }

        public bool RecordedByteDeferral { get; set; }
    }

    private sealed class UpdateAccumulator
    {
        private List<ChunkPos>? _loadedPositions;
        private List<ChunkPos>? _generatedPositions;
        private List<ChunkPos>? _savedPositions;
        private List<ChunkPos>? _unloadedPositions;
        private List<ChunkPos>? _skippedDirtyUnloadPositions;

        public IReadOnlyList<ChunkPos> LoadedPositions =>
            _loadedPositions is null ? Array.Empty<ChunkPos>() : _loadedPositions;

        public IReadOnlyList<ChunkPos> GeneratedPositions =>
            _generatedPositions is null ? Array.Empty<ChunkPos>() : _generatedPositions;

        public IReadOnlyList<ChunkPos> SavedPositions =>
            _savedPositions is null ? Array.Empty<ChunkPos>() : _savedPositions;

        public IReadOnlyList<ChunkPos> UnloadedPositions =>
            _unloadedPositions is null ? Array.Empty<ChunkPos>() : _unloadedPositions;

        public IReadOnlyList<ChunkPos> SkippedDirtyUnloadPositions =>
            _skippedDirtyUnloadPositions is null ? Array.Empty<ChunkPos>() : _skippedDirtyUnloadPositions;

        public void RecordLoaded(ChunkPos position)
        {
            (_loadedPositions ??= new List<ChunkPos>()).Add(position);
        }

        public void RecordGenerated(ChunkPos position)
        {
            (_generatedPositions ??= new List<ChunkPos>()).Add(position);
        }

        public void RecordSaved(ChunkPos position)
        {
            (_savedPositions ??= new List<ChunkPos>()).Add(position);
        }

        public void RecordUnloaded(ChunkPos position)
        {
            (_unloadedPositions ??= new List<ChunkPos>()).Add(position);
        }

        public void RecordSkippedDirtyUnload(ChunkPos position)
        {
            (_skippedDirtyUnloadPositions ??= new List<ChunkPos>()).Add(position);
        }

        public int ApplyItemsProcessed { get; set; }

        public int CancellationRequests { get; set; }

        public int CancelledJobs { get; set; }

        public int StaleResultsRejected { get; set; }

        public int FailedJobs { get; set; }

        public int RetryableFailures { get; set; }

        public int PermanentFailures { get; set; }

        public int RetryScheduled { get; set; }

        public int RetryExhausted { get; set; }

        public int DeferredApplyItemsByTime { get; set; }

        public int DeferredApplyItemsByBytes { get; set; }

        public long AppliedDecodedBytes { get; set; }

        public int OversizeApplyOperations { get; set; }
    }
}
