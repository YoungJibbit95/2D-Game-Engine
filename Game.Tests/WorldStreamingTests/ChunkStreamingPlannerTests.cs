using Game.Core.World;
using Game.Core.World.Streaming;
using Xunit;

namespace Game.Tests.WorldStreamingTests;

public sealed class ChunkStreamingPlannerTests
{
    [Fact]
    public void Plan_ComputesChunksToLoadAroundVisibleArea()
    {
        var world = new World(128, 64, WorldMetadata.CreateDefault(seed: 1));
        var planner = new ChunkStreamingPlanner();

        var plan = planner.Plan(world, new RectI(32, 0, 32, 32), new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 1
        });

        Assert.Contains(new ChunkPos(1, 0), plan.RequiredChunks);
        Assert.Contains(new ChunkPos(1, 0), plan.ChunksToLoad);
    }

    [Fact]
    public void Plan_KeepsDirtyChunksLoaded()
    {
        var world = new World(128, 64, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(100, 40, KnownTileIds.Dirt);

        var plan = new ChunkStreamingPlanner().Plan(world, new RectI(0, 0, 32, 32), new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 0,
            KeepDirtyChunksLoaded = true
        });

        Assert.DoesNotContain(new ChunkPos(3, 1), plan.ChunksToUnload);
    }

    [Fact]
    public void ApplyUnloadPlan_UnloadsCleanChunksOutsideRetainArea()
    {
        var world = new World(128, 64, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(100, 40, KnownTileIds.Dirt);
        world.ClearAllDirtyFlags();
        var planner = new ChunkStreamingPlanner();
        var plan = planner.Plan(world, new RectI(0, 0, 32, 32), new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 0
        });

        var unloaded = planner.ApplyUnloadPlan(world, plan);

        Assert.Equal(1, unloaded);
        Assert.False(world.TryGetChunk(new ChunkPos(3, 1), out _));
    }

    [Fact]
    public void Plan_AllowsNegativeChunksForHorizontallyInfiniteWorld()
    {
        var world = new World(Game.Core.GameConstants.ChunkSize, 96, WorldMetadata.CreateDefault(seed: 1), isHorizontallyInfinite: true);

        var plan = new ChunkStreamingPlanner().Plan(world, new RectI(-96, 0, 96, 32), new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 0
        });

        Assert.Contains(new ChunkPos(-3, 0), plan.RequiredChunks);
        Assert.Contains(new ChunkPos(-1, 0), plan.RequiredChunks);
        Assert.Contains(new ChunkPos(-3, 0), plan.ChunksToLoad);
    }
}
