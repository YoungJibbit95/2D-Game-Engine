using Game.Core.Events;
using Game.Core.Saving;
using Game.Core.World.Generation;

namespace Game.Core.World.Streaming;

public sealed class ChunkStreamingService
{
    private readonly ChunkStreamingPlanner _planner;
    private readonly InfiniteWorldChunkGenerator _generator;
    private readonly WorldSaveService _saves;

    public ChunkStreamingService()
        : this(
            new ChunkStreamingPlanner(),
            new InfiniteWorldChunkGenerator(),
            new WorldSaveService(WorldChunkStorageMode.RegionFiles))
    {
    }

    public ChunkStreamingService(
        ChunkStreamingPlanner? planner = null,
        InfiniteWorldChunkGenerator? generator = null,
        WorldSaveService? saves = null)
    {
        _planner = planner ?? new ChunkStreamingPlanner();
        _generator = generator ?? new InfiniteWorldChunkGenerator();
        _saves = saves ?? new WorldSaveService(WorldChunkStorageMode.RegionFiles);
    }

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
        var plan = _planner.Plan(world, visibleTileArea, resolvedOptions);
        var operationBudget = resolvedOptions.MaxChunkOperationsPerUpdate;
        var operationsProcessed = 0;
        var loaded = 0;
        var generated = 0;
        var savedBeforeUnload = 0;
        var unloaded = 0;
        var skippedDirtyUnloads = 0;
        var loadedPositions = new List<ChunkPos>();
        var generatedPositions = new List<ChunkPos>();
        var savedPositions = new List<ChunkPos>();
        var unloadedPositions = new List<ChunkPos>();
        var skippedDirtyUnloadPositions = new List<ChunkPos>();

        var centerChunk = CoordinateUtils.TileToChunk(
            visibleTileArea.Left + Math.Max(0, visibleTileArea.Width - 1) / 2,
            visibleTileArea.Top + Math.Max(0, visibleTileArea.Height - 1) / 2);
        var orderedLoads = plan.ChunksToLoad
            .OrderBy(position => DistanceSquared(position, centerChunk))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
        var loadOperationCount = Math.Min(orderedLoads.Length, operationBudget);
        foreach (var position in orderedLoads.Take(loadOperationCount))
        {
            operationsProcessed++;
            if (!string.IsNullOrWhiteSpace(worldDirectory) && _saves.TryLoadChunk(world, worldDirectory, position))
            {
                loaded++;
                loadedPositions.Add(position);
                events?.Publish(new ChunkLoadedEvent(position, LoadedFromSave: true));
                continue;
            }

            if (_generator.EnsureChunk(world, profile, position))
            {
                generated++;
                generatedPositions.Add(position);
                events?.Publish(new ChunkGeneratedEvent(position));
            }
        }

        var remainingBudget = Math.Max(0, operationBudget - operationsProcessed);
        var orderedUnloads = plan.ChunksToUnload
            .OrderByDescending(position => DistanceSquared(position, centerChunk))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
        var unloadOperationCount = Math.Min(orderedUnloads.Length, remainingBudget);
        foreach (var position in orderedUnloads.Take(unloadOperationCount))
        {
            operationsProcessed++;
            if (!world.TryGetChunk(position, out var chunk) || chunk is null)
            {
                continue;
            }

            if (chunk.IsDirty)
            {
                if (string.IsNullOrWhiteSpace(worldDirectory))
                {
                    skippedDirtyUnloads++;
                    skippedDirtyUnloadPositions.Add(position);
                    events?.Publish(new ChunkUnloadSkippedEvent(position, "dirty_without_save_directory"));
                    continue;
                }

                if (_saves.SaveChunk(world, worldDirectory, position))
                {
                    savedBeforeUnload++;
                    savedPositions.Add(position);
                    events?.Publish(new ChunkSavedEvent(position, SavedBeforeUnload: true));
                }
            }

            if (world.UnloadChunk(position, requireClean: true))
            {
                unloaded++;
                unloadedPositions.Add(position);
                events?.Publish(new ChunkUnloadedEvent(position));
            }
        }

        return new ChunkStreamingUpdateResult(
            plan,
            loaded,
            generated,
            savedBeforeUnload,
            unloaded,
            skippedDirtyUnloads,
            loadedPositions,
            generatedPositions,
            savedPositions,
            unloadedPositions,
            skippedDirtyUnloadPositions,
            operationsProcessed,
            Math.Max(0, orderedLoads.Length - loadOperationCount),
            Math.Max(0, orderedUnloads.Length - unloadOperationCount));
    }

    private static long DistanceSquared(ChunkPos first, ChunkPos second)
    {
        var x = (long)first.X - second.X;
        var y = (long)first.Y - second.Y;
        return x * x + y * y;
    }
}
