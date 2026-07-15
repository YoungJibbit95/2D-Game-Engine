using Game.Core;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using Xunit;

namespace Game.Tests.WorldStreamingTests;

public sealed class ChunkStreamingRetryAndBudgetTests
{
    private readonly InfiniteWorldChunkGenerator _generator = new();
    private readonly WorldGenerationProfile _profile = WorldGenerationProfile.Small;

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(20, 5)]
    public void RetryPolicy_UsesDeterministicBoundedExponentialBackoff(int failedAttempt, int expectedUpdates)
    {
        var policy = new ChunkStreamingRetryPolicy
        {
            InitialBackoffUpdates = 1,
            MaxBackoffUpdates = 5,
            BackoffMultiplier = 2
        };

        Assert.Equal(expectedUpdates, policy.GetBackoffUpdatesAfterFailure(failedAttempt));
    }

    [Fact]
    public void Update_RetryableNegativeXLoadSucceedsAfterDeterministicBackoff()
    {
        var position = new ChunkPos(-7, 1);
        var world = CreateWorld();
        var runner = new SequencedJobRunner(loadFailuresBeforeSuccess: 2);
        var service = new ChunkStreamingService(runner);
        var options = Options() with
        {
            RetryPolicy = new ChunkStreamingRetryPolicy
            {
                MaxAttempts = 3,
                InitialBackoffUpdates = 2,
                MaxBackoffUpdates = 4,
                BackoffMultiplier = 2
            }
        };

        var first = Update(service, world, position, options);
        Assert.Equal(1, runner.LoadCalls);
        Assert.Equal(1, first.RetryScheduled);

        var second = Update(service, world, position, options);
        Assert.Equal(1, runner.LoadCalls);
        Assert.Equal(1, second.Telemetry.PendingRetryJobs);

        var third = Update(service, world, position, options);
        Assert.Equal(2, runner.LoadCalls);
        Assert.Equal(1, third.RetryScheduled);

        Update(service, world, position, options);
        Update(service, world, position, options);
        Update(service, world, position, options);
        var completed = Update(service, world, position, options);

        Assert.Equal(3, runner.LoadCalls);
        Assert.Equal(1, completed.GeneratedChunks);
        Assert.True(world.TryGetChunk(position, out _));
        Assert.Equal(2, completed.Telemetry.RetryScheduled);
        Assert.Equal(2, completed.Telemetry.RetryableFailures);
        Assert.Equal(6, completed.Telemetry.RetryBackoffUpdatesScheduled);
        Assert.Equal(0, completed.Telemetry.PendingRetryJobs);
    }

    [Fact]
    public void Update_ExhaustedRetryCycleIsTerminalUntilExplicitReset()
    {
        var position = new ChunkPos(-9, 1);
        var world = CreateWorld();
        var runner = new SequencedJobRunner(loadFailuresBeforeSuccess: int.MaxValue);
        var service = new ChunkStreamingService(runner);
        var options = Options() with
        {
            RetryPolicy = new ChunkStreamingRetryPolicy
            {
                MaxAttempts = 3,
                InitialBackoffUpdates = 1,
                MaxBackoffUpdates = 2
            }
        };

        Update(service, world, position, options);
        Update(service, world, position, options);
        Update(service, world, position, options);
        var exhausted = Update(service, world, position, options);
        Update(service, world, position, options);
        Update(service, world, position, options);

        Assert.Equal(3, runner.LoadCalls);
        Assert.Equal(1, exhausted.RetryExhausted);
        Assert.Equal(3, exhausted.Telemetry.RetryableFailures);
        Assert.Equal(2, exhausted.Telemetry.RetryScheduled);
        Assert.Equal(1, exhausted.Telemetry.RetryExhausted);
        Assert.Equal(1, exhausted.Telemetry.TerminalFailuresSuppressed);
        Assert.False(world.TryGetChunk(position, out _));

        service.ResetTerminalFailures();
        Update(service, world, position, options);
        Assert.Equal(4, runner.LoadCalls);
    }

    [Fact]
    public void Update_PermanentFailureIsNotRetriedEveryFrame()
    {
        var position = new ChunkPos(5, 1);
        var world = CreateWorld();
        var runner = new SequencedJobRunner(
            loadFailuresBeforeSuccess: int.MaxValue,
            loadExceptionFactory: static () => new InvalidDataException("invalid chunk payload"));
        var service = new ChunkStreamingService(runner);

        var failed = Update(service, world, position, Options());
        for (var index = 0; index < 8; index++)
        {
            Update(service, world, position, Options());
        }

        Assert.Equal(1, runner.LoadCalls);
        Assert.Equal(1, failed.PermanentFailures);
        Assert.Equal(0, failed.RetryScheduled);
        Assert.Equal(1, service.Telemetry.PermanentFailures);
        Assert.Equal(1, service.Telemetry.TerminalFailuresSuppressed);
    }

    [Fact]
    public void Update_CustomClassifierCanMakeTransientFailurePermanent()
    {
        var position = new ChunkPos(6, 1);
        var world = CreateWorld();
        var runner = new SequencedJobRunner(loadFailuresBeforeSuccess: 1);
        var classifier = new FixedFailureClassifier(ChunkStreamingFailureClassification.Permanent);
        var service = new ChunkStreamingService(runner, failureClassifier: classifier);

        Update(service, world, position, Options());
        Update(service, world, position, Options());

        Assert.Equal(1, runner.LoadCalls);
        Assert.Equal(ChunkStreamingJobKind.LoadOrGenerate, classifier.LastJobKind);
        Assert.Equal(1, service.Telemetry.PermanentFailures);
        Assert.Equal(0, service.Telemetry.RetryScheduled);
    }

    [Fact]
    public void Update_SaveFailureRetriesSnapshotAndUnloadsAfterSuccess()
    {
        var visible = new ChunkPos(0, 1);
        var dirty = new ChunkPos(-4, 1);
        var world = CreateWorld();
        _generator.EnsureChunk(world, _profile, visible);
        _generator.EnsureChunk(world, _profile, dirty);
        world.ClearAllDirtyFlags();
        world.SetTile(
            dirty.X * GameConstants.ChunkSize + 2,
            dirty.Y * GameConstants.ChunkSize + 3,
            KnownTileIds.CopperOre);
        var runner = new SequencedJobRunner(saveFailuresBeforeSuccess: 1);
        var service = new ChunkStreamingService(runner);
        var options = Options() with
        {
            KeepDirtyChunksLoaded = false,
            RetryPolicy = new ChunkStreamingRetryPolicy
            {
                MaxAttempts = 2,
                InitialBackoffUpdates = 1,
                MaxBackoffUpdates = 1
            }
        };

        var first = Update(service, world, visible, options, worldDirectory: "synthetic-save");
        var second = Update(service, world, visible, options, worldDirectory: "synthetic-save");

        Assert.Equal(1, first.RetryScheduled);
        Assert.Equal(2, runner.SaveCalls);
        Assert.Equal(dirty, runner.LastSavePosition);
        Assert.Equal(1, second.SavedChunksBeforeUnload);
        Assert.Equal(1, second.UnloadedChunks);
        Assert.False(world.TryGetChunk(dirty, out _));
        Assert.Equal(1, second.Telemetry.RetryableFailures);
    }

    [Fact]
    public void Update_ElapsedBudgetDefersRemainingApplyItemsAfterGuaranteedProgress()
    {
        var world = CreateWorld();
        var clock = new ScriptedTimeSource(TimeSpan.FromMilliseconds(2));
        var service = new ChunkStreamingService(new SequencedJobRunner(), timeSource: clock);
        var area = ThreeChunkArea();
        var options = Options() with
        {
            MaxConcurrentLoadJobs = 3,
            MaxApplyQueueLength = 3,
            MaxChunkOperationsPerUpdate = 3,
            MaxApplyTimePerUpdate = TimeSpan.FromMilliseconds(1)
        };

        var first = service.Update(world, _profile, area, options: options);

        Assert.Equal(1, first.ApplyOperationsProcessed);
        Assert.Equal(1, first.GeneratedChunks);
        Assert.Equal(2, first.DeferredApplyItemsByTime);
        Assert.Equal(0, first.DeferredApplyItemsByBytes);
        Assert.Equal(2, first.Telemetry.DeferredApplyItemsByTime);
        Assert.Equal(2, first.Telemetry.ApplyQueueLength);
    }

    [Fact]
    public void Update_DecodedByteBudgetAppliesOnlyFittingChunks()
    {
        var world = CreateWorld();
        var runner = new SequencedJobRunner();
        var service = new ChunkStreamingService(runner, timeSource: ZeroTimeSource.Instance);
        var bytesPerChunk = ChunkStreamingChunkSnapshot.Capture(new Chunk(new ChunkPos(0, 0))).DecodedBytes;
        var options = Options() with
        {
            MaxConcurrentLoadJobs = 3,
            MaxApplyQueueLength = 3,
            MaxChunkOperationsPerUpdate = 3,
            MaxApplyDecodedBytesPerUpdate = bytesPerChunk
        };

        var first = service.Update(world, _profile, ThreeChunkArea(), options: options);

        Assert.Equal(1, first.ApplyOperationsProcessed);
        Assert.Equal(bytesPerChunk, first.AppliedDecodedBytes);
        Assert.Equal(2, first.DeferredApplyItemsByBytes);
        Assert.Equal(0, first.OversizeApplyOperations);
        Assert.Equal(bytesPerChunk, first.Telemetry.AppliedDecodedBytes);
        Assert.Equal(2, first.Telemetry.DeferredApplyItemsByBytes);
    }

    [Fact]
    public void Update_OversizeHeadMakesOneProgressPerUpdateWithoutQueueStarvation()
    {
        var world = CreateWorld();
        var service = new ChunkStreamingService(
            new SequencedJobRunner(),
            timeSource: ZeroTimeSource.Instance);
        var area = new RectI(
            -GameConstants.ChunkSize,
            GameConstants.ChunkSize,
            GameConstants.ChunkSize * 2,
            GameConstants.ChunkSize);
        var options = Options() with
        {
            MaxConcurrentLoadJobs = 2,
            MaxApplyQueueLength = 2,
            MaxChunkOperationsPerUpdate = 2,
            MaxApplyDecodedBytesPerUpdate = 1
        };

        var first = service.Update(world, _profile, area, options: options);
        var second = service.Update(world, _profile, area, options: options);

        Assert.Equal(1, first.GeneratedChunks);
        Assert.Equal(1, first.OversizeApplyOperations);
        Assert.Equal(1, first.DeferredApplyItemsByBytes);
        Assert.Equal(1, second.GeneratedChunks);
        Assert.Equal(1, second.OversizeApplyOperations);
        Assert.Equal(2, world.Chunks.Count);
        Assert.Equal(0, second.Telemetry.ApplyQueueLength);
        Assert.Equal(2, second.Telemetry.OversizeApplyOperations);
    }

    [Fact]
    public void Update_StaleCancelledFaultDoesNotEnterRetryQueue()
    {
        var world = CreateWorld();
        var runner = new ControlledFaultRunner();
        var service = new ChunkStreamingService(runner);
        var left = new ChunkPos(-8, 1);
        var right = new ChunkPos(8, 1);

        Update(service, world, left, Options());
        Update(service, world, right, Options());
        runner.FailFirstLoad();
        var result = Update(service, world, right, Options());

        Assert.True(result.CancelledJobs >= 1);
        Assert.Equal(0, result.RetryScheduled);
        Assert.Equal(0, result.Telemetry.PendingRetryJobs);
        Assert.False(world.TryGetChunk(left, out _));
    }

    private World CreateWorld()
    {
        return _generator.CreateWorld(_profile, seed: 42);
    }

    private ChunkStreamingUpdateResult Update(
        ChunkStreamingService service,
        World world,
        ChunkPos position,
        ChunkStreamingOptions options,
        string? worldDirectory = null)
    {
        return service.Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(position),
            worldDirectory,
            options);
    }

    private static RectI ThreeChunkArea()
    {
        return new RectI(
            -GameConstants.ChunkSize,
            GameConstants.ChunkSize,
            GameConstants.ChunkSize * 3,
            GameConstants.ChunkSize);
    }

    private static ChunkStreamingOptions Options()
    {
        return new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 0,
            KeepDirtyChunksLoaded = true,
            MaxApplyTimePerUpdate = TimeSpan.FromSeconds(1)
        };
    }

    private sealed class SequencedJobRunner : IChunkStreamingJobRunner
    {
        private readonly int _loadFailuresBeforeSuccess;
        private readonly int _saveFailuresBeforeSuccess;
        private readonly Func<Exception> _loadExceptionFactory;

        public SequencedJobRunner(
            int loadFailuresBeforeSuccess = 0,
            int saveFailuresBeforeSuccess = 0,
            Func<Exception>? loadExceptionFactory = null)
        {
            _loadFailuresBeforeSuccess = loadFailuresBeforeSuccess;
            _saveFailuresBeforeSuccess = saveFailuresBeforeSuccess;
            _loadExceptionFactory = loadExceptionFactory ??
                (() => new IOException("transient chunk read failure"));
        }

        public int LoadCalls { get; private set; }

        public int SaveCalls { get; private set; }

        public ChunkPos LastSavePosition { get; private set; }

        public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
            ChunkStreamingLoadJobRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCalls++;
            if (LoadCalls <= _loadFailuresBeforeSuccess)
            {
                return Task.FromException<ChunkStreamingLoadJobResult>(_loadExceptionFactory());
            }

            return Task.FromResult(new ChunkStreamingLoadJobResult(
                request,
                ChunkStreamingChunkSnapshot.Capture(new Chunk(request.Position)),
                LoadedFromSave: false,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(1)));
        }

        public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
            ChunkStreamingSaveJobRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;
            LastSavePosition = request.Chunk.Position;
            if (SaveCalls <= _saveFailuresBeforeSuccess)
            {
                return Task.FromException<ChunkStreamingSaveJobResult>(
                    new IOException("transient chunk write failure"));
            }

            return Task.FromResult(new ChunkStreamingSaveJobResult(
                request,
                Succeeded: true,
                TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class ControlledFaultRunner : IChunkStreamingJobRunner
    {
        private readonly List<TaskCompletionSource<ChunkStreamingLoadJobResult>> _loads = new();

        public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
            ChunkStreamingLoadJobRequest request,
            CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<ChunkStreamingLoadJobResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _loads.Add(source);
            return source.Task;
        }

        public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
            ChunkStreamingSaveJobRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void FailFirstLoad()
        {
            _loads[0].SetException(new IOException("late stale fault"));
        }
    }

    private sealed class FixedFailureClassifier : IChunkStreamingFailureClassifier
    {
        private readonly ChunkStreamingFailureClassification _classification;

        public FixedFailureClassifier(ChunkStreamingFailureClassification classification)
        {
            _classification = classification;
        }

        public ChunkStreamingJobKind? LastJobKind { get; private set; }

        public ChunkStreamingFailureClassification Classify(
            ChunkStreamingJobKind jobKind,
            Exception exception,
            bool cancellationRequested,
            bool isCurrentRequest)
        {
            LastJobKind = jobKind;
            return _classification;
        }
    }

    private sealed class ScriptedTimeSource : IChunkStreamingTimeSource
    {
        private readonly TimeSpan _elapsedPerOperation;

        public ScriptedTimeSource(TimeSpan elapsedPerOperation)
        {
            _elapsedPerOperation = elapsedPerOperation;
        }

        public long GetTimestamp()
        {
            return 0;
        }

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            return _elapsedPerOperation;
        }
    }

    private sealed class ZeroTimeSource : IChunkStreamingTimeSource
    {
        public static ZeroTimeSource Instance { get; } = new();

        public long GetTimestamp()
        {
            return 0;
        }

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            return TimeSpan.Zero;
        }
    }
}
