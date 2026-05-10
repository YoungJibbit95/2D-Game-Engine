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
        ChunkStreamingOptions? options = null)
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

        foreach (var position in plan.ChunksToLoad.OrderBy(chunk => chunk.Y).ThenBy(chunk => chunk.X))
        {
            if (!string.IsNullOrWhiteSpace(worldDirectory) && _saves.TryLoadChunk(world, worldDirectory, position))
            {
                loaded++;
                continue;
            }

            if (_generator.EnsureChunk(world, profile, position))
            {
                generated++;
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
                    continue;
                }

                if (_saves.SaveChunk(world, worldDirectory, position))
                {
                    savedBeforeUnload++;
                }
            }

            if (world.UnloadChunk(position, requireClean: true))
            {
                unloaded++;
            }
        }

        return new ChunkStreamingUpdateResult(
            plan,
            loaded,
            generated,
            savedBeforeUnload,
            unloaded,
            skippedDirtyUnloads);
    }
}
