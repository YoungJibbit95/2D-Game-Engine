using Game.Core;
using Game.Core.Events;
using Game.Core.Saving;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using Game.Tests.PerformanceTests;
using Xunit;

namespace Game.Tests.WorldStreamingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ChunkStreamingServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "terraria-like-streaming-tests",
        Guid.NewGuid().ToString("N"));
    private readonly InfiniteWorldChunkGenerator _generator = new();
    private readonly WorldGenerationProfile _profile = WorldGenerationProfile.Small;

    [Fact]
    public async Task Update_LoadsAndDecodesSavedNegativeChunkInBackgroundBeforeApply()
    {
        var position = new ChunkPos(-2, 1);
        var tile = new TilePos(position.X * GameConstants.ChunkSize, position.Y * GameConstants.ChunkSize + 1);
        var source = _generator.CreateWorld(_profile, seed: 42);
        _generator.EnsureChunk(source, _profile, position);
        source.SetTile(tile.X, tile.Y, KnownTileIds.CopperOre);
        new WorldSaveService(WorldChunkStorageMode.RegionFiles).SaveChunk(source, _tempDirectory, position);

        var target = _generator.CreateWorld(_profile, seed: 42);
        var service = new ChunkStreamingService();
        var result = await PumpUntilAsync(
            () => service.Update(
                target,
                _profile,
                CoordinateUtils.ChunkTileBounds(position),
                _tempDirectory,
                NoMarginOptions()),
            update => update.LoadedChunks == 1);

        Assert.Equal(new[] { position }, result.LoadedChunkPositions);
        Assert.Equal(KnownTileIds.CopperOre, target.GetTile(tile.X, tile.Y).TileId);
        Assert.False(target.Chunks[position].IsDirty);
        Assert.Equal(1, result.Telemetry.LoadOperations);
        Assert.True(result.Telemetry.LoadedDecodedBytes > 0);
        Assert.True(result.Telemetry.LoadTime >= TimeSpan.Zero);
    }

    [Fact]
    public void Update_GeneratesMissingRequiredChunksThroughJobRunner()
    {
        var position = new ChunkPos(3, 1);
        var world = _generator.CreateWorld(_profile, seed: 42);
        var runner = new ImmediateJobRunner();
        ChunkGeneratedEvent? generated = null;
        var events = new GameEventBus();
        events.Subscribe<ChunkGeneratedEvent>(gameEvent => generated = gameEvent);

        var result = new ChunkStreamingService(runner).Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(position),
            _tempDirectory,
            NoMarginOptions(),
            events);

        Assert.Equal(0, result.LoadedChunks);
        Assert.Equal(1, result.GeneratedChunks);
        Assert.Equal(new[] { position }, result.GeneratedChunkPositions);
        Assert.True(world.TryGetChunk(position, out _));
        Assert.Equal(1, result.Telemetry.GenerateOperations);
        Assert.Equal(1, result.Telemetry.ApplyOperations);
        Assert.True(result.Telemetry.GeneratedDecodedBytes > 0);
        Assert.NotNull(generated);
        Assert.Equal(position, generated.Position);
    }

    [Fact]
    public void Update_SavesDirtySnapshotBeforeApplyingUnload()
    {
        var dirtyChunk = new ChunkPos(4, 1);
        var dirtyTile = new TilePos(dirtyChunk.X * GameConstants.ChunkSize, dirtyChunk.Y * GameConstants.ChunkSize + 3);
        var visibleChunk = new ChunkPos(0, 0);
        var world = _generator.CreateWorld(_profile, seed: 99);
        _generator.EnsureChunk(world, _profile, visibleChunk);
        _generator.EnsureChunk(world, _profile, dirtyChunk);
        world.ClearAllDirtyFlags();
        world.SetTile(dirtyTile.X, dirtyTile.Y, KnownTileIds.IronOre);
        var runner = new ImmediateJobRunner();
        ChunkSavedEvent? saved = null;
        ChunkUnloadedEvent? unloaded = null;
        var events = new GameEventBus();
        events.Subscribe<ChunkSavedEvent>(gameEvent => saved = gameEvent);
        events.Subscribe<ChunkUnloadedEvent>(gameEvent => unloaded = gameEvent);

        var result = new ChunkStreamingService(runner).Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(visibleChunk),
            _tempDirectory,
            NoMarginOptions() with { KeepDirtyChunksLoaded = false },
            events);

        Assert.Equal(1, result.SavedChunksBeforeUnload);
        Assert.Equal(1, result.UnloadedChunks);
        Assert.Equal(new[] { dirtyChunk }, result.SavedChunkPositions);
        Assert.Equal(new[] { dirtyChunk }, result.UnloadedChunkPositions);
        Assert.False(world.TryGetChunk(dirtyChunk, out _));
        Assert.Single(runner.SaveRequests);
        Assert.Equal(KnownTileIds.IronOre, runner.SaveRequests[0].Chunk.Materialize().GetTile(0, 3).TileId);
        Assert.Equal(1, result.Telemetry.SaveOperations);
        Assert.Equal(1, result.Telemetry.UnloadOperations);
        Assert.NotNull(saved);
        Assert.Equal(dirtyChunk, saved.Position);
        Assert.NotNull(unloaded);
        Assert.Equal(dirtyChunk, unloaded.Position);
    }

    [Fact]
    public async Task Update_DefaultRunnerDirtyUnloadRoundTripsThroughExistingRegionFormat()
    {
        var dirtyChunk = new ChunkPos(-5, 1);
        var dirtyTile = new TilePos(
            dirtyChunk.X * GameConstants.ChunkSize + 4,
            dirtyChunk.Y * GameConstants.ChunkSize + 6);
        var visibleChunk = new ChunkPos(0, 0);
        var world = _generator.CreateWorld(_profile, seed: 99);
        _generator.EnsureChunk(world, _profile, visibleChunk);
        _generator.EnsureChunk(world, _profile, dirtyChunk);
        world.ClearAllDirtyFlags();
        world.SetTile(dirtyTile.X, dirtyTile.Y, KnownTileIds.IronOre);
        var service = new ChunkStreamingService();
        var options = NoMarginOptions() with { KeepDirtyChunksLoaded = false };

        var result = await PumpUntilAsync(
            () => service.Update(
                world,
                _profile,
                CoordinateUtils.ChunkTileBounds(visibleChunk),
                _tempDirectory,
                options),
            update => update.SavedChunksBeforeUnload == 1 && update.UnloadedChunks == 1);

        var loaded = _generator.CreateWorld(_profile, seed: 99);
        var saveService = new WorldSaveService(WorldChunkStorageMode.RegionFiles);
        Assert.True(saveService.TryLoadChunk(loaded, _tempDirectory, dirtyChunk));
        Assert.Equal(KnownTileIds.IronOre, loaded.GetTile(dirtyTile.X, dirtyTile.Y).TileId);
        Assert.True(result.Telemetry.SaveTime >= TimeSpan.Zero);
        Assert.True(result.Telemetry.SavedDecodedBytes > 0);
    }

    [Fact]
    public void Update_DoesNotUnloadDirtyChunkChangedWhileSaveWasRunning()
    {
        var dirtyChunk = new ChunkPos(4, 1);
        var dirtyTileX = dirtyChunk.X * GameConstants.ChunkSize + 3;
        var dirtyTileY = dirtyChunk.Y * GameConstants.ChunkSize + 3;
        var visibleChunk = new ChunkPos(0, 0);
        var world = _generator.CreateWorld(_profile, seed: 99);
        _generator.EnsureChunk(world, _profile, visibleChunk);
        _generator.EnsureChunk(world, _profile, dirtyChunk);
        world.ClearAllDirtyFlags();
        world.SetTile(dirtyTileX, dirtyTileY, KnownTileIds.IronOre);
        var runner = new ControlledJobRunner();
        var service = new ChunkStreamingService(runner);
        var options = NoMarginOptions() with { KeepDirtyChunksLoaded = false };

        service.Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(visibleChunk),
            _tempDirectory,
            options);
        world.SetTile(dirtyTileX, dirtyTileY, KnownTileIds.CopperOre);
        runner.CompleteSave(0);

        var result = service.Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(visibleChunk),
            _tempDirectory,
            options);

        Assert.Equal(1, result.SavedChunksBeforeUnload);
        Assert.Equal(0, result.UnloadedChunks);
        Assert.True(world.TryGetChunk(dirtyChunk, out var current));
        Assert.NotNull(current);
        Assert.True(current.IsDirty);
        Assert.Equal(KnownTileIds.CopperOre, world.GetTile(dirtyTileX, dirtyTileY).TileId);
    }

    [Fact]
    public void Update_SkipsDirtyUnloadWhenNoSaveDirectoryIsAvailable()
    {
        var dirtyChunk = new ChunkPos(4, 1);
        var visibleChunk = new ChunkPos(0, 0);
        var world = _generator.CreateWorld(_profile, seed: 99);
        _generator.EnsureChunk(world, _profile, visibleChunk);
        _generator.EnsureChunk(world, _profile, dirtyChunk);
        world.ClearAllDirtyFlags();
        world.SetTile(dirtyChunk.X * GameConstants.ChunkSize, dirtyChunk.Y * GameConstants.ChunkSize + 3, KnownTileIds.IronOre);
        ChunkUnloadSkippedEvent? skipped = null;
        var bus = new GameEventBus();
        bus.Subscribe<ChunkUnloadSkippedEvent>(gameEvent => skipped = gameEvent);

        var result = new ChunkStreamingService(new ImmediateJobRunner()).Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(visibleChunk),
            worldDirectory: null,
            NoMarginOptions() with { KeepDirtyChunksLoaded = false },
            bus);

        Assert.Equal(1, result.SkippedDirtyUnloads);
        Assert.Equal(new[] { dirtyChunk }, result.SkippedDirtyUnloadPositions);
        Assert.True(world.TryGetChunk(dirtyChunk, out _));
        Assert.NotNull(skipped);
        Assert.Equal("dirty_without_save_directory", skipped.Reason);
    }

    [Fact]
    public void Update_RapidCameraReversalCancelsObsoleteRequestAndRejectsLateResult()
    {
        var world = _generator.CreateWorld(_profile, seed: 42);
        var runner = new ControlledJobRunner();
        var service = new ChunkStreamingService(runner);
        var left = new ChunkPos(-6, 1);
        var right = new ChunkPos(6, 1);

        service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(left), _tempDirectory, NoMarginOptions());
        var reversal = service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(right), _tempDirectory, NoMarginOptions());

        Assert.True(runner.Loads[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(1, reversal.CancellationRequests);
        runner.CompleteLoad(0, loadedFromSave: false);

        var late = service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(right), _tempDirectory, NoMarginOptions());

        Assert.False(world.TryGetChunk(left, out _));
        Assert.True(late.StaleResultsRejected >= 1);
        Assert.True(late.Telemetry.StaleResultsRejected >= 1);
        Assert.Contains(runner.Loads, load => load.Request.Position == right);
    }

    [Fact]
    public void Update_HarvestsCancellationFromCooperativeJob()
    {
        var world = _generator.CreateWorld(_profile, seed: 42);
        var runner = new CancellationAwareJobRunner();
        var service = new ChunkStreamingService(runner);
        var left = new ChunkPos(-3, 1);
        var right = new ChunkPos(3, 1);

        service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(left), _tempDirectory, NoMarginOptions());
        var reversal = service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(right), _tempDirectory, NoMarginOptions());
        var result = service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(right), _tempDirectory, NoMarginOptions());

        Assert.Equal(1, reversal.CancelledJobs);
        Assert.Equal(1, result.Telemetry.CancelledJobs);
        Assert.False(world.TryGetChunk(left, out _));
    }

    [Fact]
    public void Update_RejectsResultFromReplacedWorldSession()
    {
        var firstWorld = _generator.CreateWorld(_profile, seed: 1);
        var secondWorld = _generator.CreateWorld(_profile, seed: 2);
        var position = new ChunkPos(-2, 1);
        var runner = new ControlledJobRunner();
        var service = new ChunkStreamingService(runner);

        service.Update(firstWorld, _profile, CoordinateUtils.ChunkTileBounds(position), _tempDirectory, NoMarginOptions());
        var firstGeneration = service.WorldSessionGeneration;
        service.Update(secondWorld, _profile, CoordinateUtils.ChunkTileBounds(position), _tempDirectory, NoMarginOptions());
        var secondGeneration = service.WorldSessionGeneration;
        runner.CompleteLoad(0, loadedFromSave: false);

        var result = service.Update(secondWorld, _profile, CoordinateUtils.ChunkTileBounds(position), _tempDirectory, NoMarginOptions());

        Assert.True(secondGeneration > firstGeneration);
        Assert.False(secondWorld.TryGetChunk(position, out _));
        Assert.True(result.StaleResultsRejected >= 1);
        Assert.Equal(secondGeneration, service.LatestRequest?.WorldSessionGeneration);
    }

    [Fact]
    public void Update_EnforcesPerUpdateApplyBudgetAndNearestFirstOrdering()
    {
        var world = _generator.CreateWorld(_profile, seed: 42);
        var visibleArea = new RectI(
            -GameConstants.ChunkSize * 2,
            GameConstants.ChunkSize,
            GameConstants.ChunkSize * 5,
            GameConstants.ChunkSize);
        var options = NoMarginOptions() with
        {
            MaxChunkOperationsPerUpdate = 2,
            MaxConcurrentLoadJobs = 5,
            MaxApplyQueueLength = 5
        };
        var service = new ChunkStreamingService(new ImmediateJobRunner());

        var first = service.Update(world, _profile, visibleArea, _tempDirectory, options);

        Assert.Equal(2, first.ApplyOperationsProcessed);
        Assert.Equal(2, first.GeneratedChunks);
        Assert.Equal(3, first.DeferredLoadChunks);
        Assert.Equal(3, first.Telemetry.ApplyQueueLength);
        Assert.Contains(new ChunkPos(0, 1), first.GeneratedChunkPositions);
        Assert.Contains(new ChunkPos(-1, 1), first.GeneratedChunkPositions);

        var second = service.Update(world, _profile, visibleArea, _tempDirectory, options);
        var third = service.Update(world, _profile, visibleArea, _tempDirectory, options);

        Assert.Equal(2, second.ApplyOperationsProcessed);
        Assert.Equal(1, third.ApplyOperationsProcessed);
        Assert.Equal(0, third.DeferredLoadChunks);
        Assert.Equal(5, third.Telemetry.GenerateOperations);
        Assert.Equal(5, third.Telemetry.ApplyOperations);
    }

    [Fact]
    public void Update_NeverExceedsBoundedApplyQueueCapacity()
    {
        var world = _generator.CreateWorld(_profile, seed: 42);
        var visibleArea = new RectI(
            -GameConstants.ChunkSize * 3,
            GameConstants.ChunkSize,
            GameConstants.ChunkSize * 7,
            GameConstants.ChunkSize);
        var options = NoMarginOptions() with
        {
            MaxChunkOperationsPerUpdate = 1,
            MaxConcurrentLoadJobs = 7,
            MaxApplyQueueLength = 2
        };
        var service = new ChunkStreamingService(new ImmediateJobRunner());

        for (var updateIndex = 0; updateIndex < 7; updateIndex++)
        {
            var result = service.Update(world, _profile, visibleArea, _tempDirectory, options);
            Assert.InRange(result.Telemetry.ApplyQueueLength, 0, 2);
        }

        Assert.Equal(7, world.Chunks.Count);
        Assert.Equal(0, service.Telemetry.ApplyQueueLength);
    }

    [Fact]
    public void Update_FaultedBackgroundJobIsReportedWithoutEscapingGameLoop()
    {
        var world = _generator.CreateWorld(_profile, seed: 42);
        var service = new ChunkStreamingService(new FaultingJobRunner());

        var result = service.Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(new ChunkPos(-2, 1)),
            _tempDirectory,
            NoMarginOptions());

        Assert.Equal(1, result.FailedJobs);
        Assert.Equal(1, result.Telemetry.FailedJobs);
        Assert.Empty(world.Chunks);
    }

    [Fact]
    public void Update_UncooperativeCancellationStillCountsAgainstConcurrencyLimit()
    {
        var world = _generator.CreateWorld(_profile, seed: 42);
        var runner = new ControlledJobRunner();
        var service = new ChunkStreamingService(runner);
        var options = NoMarginOptions() with { MaxConcurrentLoadJobs = 1 };
        var left = new ChunkPos(-8, 1);
        var right = new ChunkPos(8, 1);

        service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(left), _tempDirectory, options);
        service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(right), _tempDirectory, options);

        Assert.Single(runner.Loads);
        Assert.True(runner.Loads[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(1, service.Telemetry.PendingLoadJobs);

        runner.CompleteLoad(0, loadedFromSave: false);
        service.Update(world, _profile, CoordinateUtils.ChunkTileBounds(right), _tempDirectory, options);
        Assert.Equal(2, runner.Loads.Count);
        Assert.Equal(right, runner.Loads[1].Request.Position);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static ChunkStreamingOptions NoMarginOptions()
    {
        return new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 0,
            KeepDirtyChunksLoaded = true
        };
    }

    private static async Task<ChunkStreamingUpdateResult> PumpUntilAsync(
        Func<ChunkStreamingUpdateResult> update,
        Func<ChunkStreamingUpdateResult, bool> completed)
    {
        for (var attempt = 0; attempt < 1_000; attempt++)
        {
            var result = update();
            if (completed(result))
            {
                return result;
            }

            await Task.Delay(1);
        }

        throw new TimeoutException("Streaming job did not complete within the deterministic pump budget.");
    }

    private sealed class ImmediateJobRunner : IChunkStreamingJobRunner
    {
        public List<ChunkStreamingSaveJobRequest> SaveRequests { get; } = new();

        public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
            ChunkStreamingLoadJobRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = new Chunk(request.Position);
            var result = new ChunkStreamingLoadJobResult(
                request,
                ChunkStreamingChunkSnapshot.Capture(chunk),
                LoadedFromSave: false,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(2));
            return Task.FromResult(result);
        }

        public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
            ChunkStreamingSaveJobRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveRequests.Add(request);
            return Task.FromResult(new ChunkStreamingSaveJobResult(
                request,
                Succeeded: true,
                TimeSpan.FromMilliseconds(3)));
        }
    }

    private sealed class ControlledJobRunner : IChunkStreamingJobRunner
    {
        public List<ControlledLoad> Loads { get; } = new();

        public List<ControlledSave> Saves { get; } = new();

        public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
            ChunkStreamingLoadJobRequest request,
            CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<ChunkStreamingLoadJobResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Loads.Add(new ControlledLoad(request, cancellationToken, source));
            return source.Task;
        }

        public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
            ChunkStreamingSaveJobRequest request,
            CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<ChunkStreamingSaveJobResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Saves.Add(new ControlledSave(request, cancellationToken, source));
            return source.Task;
        }

        public void CompleteLoad(int index, bool loadedFromSave)
        {
            var load = Loads[index];
            load.Completion.SetResult(new ChunkStreamingLoadJobResult(
                load.Request,
                ChunkStreamingChunkSnapshot.Capture(new Chunk(load.Request.Position)),
                loadedFromSave,
                loadedFromSave ? TimeSpan.FromMilliseconds(2) : TimeSpan.Zero,
                loadedFromSave ? TimeSpan.Zero : TimeSpan.FromMilliseconds(3)));
        }

        public void CompleteSave(int index)
        {
            var save = Saves[index];
            save.Completion.SetResult(new ChunkStreamingSaveJobResult(
                save.Request,
                Succeeded: true,
                TimeSpan.FromMilliseconds(4)));
        }
    }

    private sealed class CancellationAwareJobRunner : IChunkStreamingJobRunner
    {
        public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
            ChunkStreamingLoadJobRequest request,
            CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource<ChunkStreamingLoadJobResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));
            return source.Task;
        }

        public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
            ChunkStreamingSaveJobRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FaultingJobRunner : IChunkStreamingJobRunner
    {
        public Task<ChunkStreamingLoadJobResult> RunLoadOrGenerateAsync(
            ChunkStreamingLoadJobRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<ChunkStreamingLoadJobResult>(new IOException("synthetic load failure"));
        }

        public Task<ChunkStreamingSaveJobResult> RunSaveAsync(
            ChunkStreamingSaveJobRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<ChunkStreamingSaveJobResult>(new IOException("synthetic save failure"));
        }
    }

    private sealed record ControlledLoad(
        ChunkStreamingLoadJobRequest Request,
        CancellationToken CancellationToken,
        TaskCompletionSource<ChunkStreamingLoadJobResult> Completion);

    private sealed record ControlledSave(
        ChunkStreamingSaveJobRequest Request,
        CancellationToken CancellationToken,
        TaskCompletionSource<ChunkStreamingSaveJobResult> Completion);
}
