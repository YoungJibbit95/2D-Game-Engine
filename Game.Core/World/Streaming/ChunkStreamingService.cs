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

        var plan = _planner.Plan(world, visibleTileArea, options);
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

        foreach (var position in plan.ChunksToLoad.OrderBy(chunk => chunk.Y).ThenBy(chunk => chunk.X))
        {
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

        foreach (var position in plan.ChunksToUnload.OrderBy(chunk => chunk.Y).ThenBy(chunk => chunk.X))
        {
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
            skippedDirtyUnloadPositions);
    }
}
