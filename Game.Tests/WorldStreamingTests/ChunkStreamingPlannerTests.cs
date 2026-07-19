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

    [Fact]
    public void CreateRequestSnapshot_IsPureAfterWorldChangesAndOrdersNegativeXLoads()
    {
        var world = new World(
            Game.Core.GameConstants.ChunkSize,
            96,
            WorldMetadata.CreateDefault(seed: 1),
            isHorizontallyInfinite: true);
        var planner = new ChunkStreamingPlanner();

        var snapshot = planner.CreateRequestSnapshot(
            world,
            new RectI(-96, 0, 96, 32),
            worldSessionGeneration: 7,
            requestSequence: 11,
            new ChunkStreamingOptions
            {
                LoadMarginChunks = 0,
                UnloadMarginChunks = 0
            });
        world.GetOrCreateChunk(new ChunkPos(-2, 0));

        Assert.Equal(7, snapshot.WorldSessionGeneration);
        Assert.Equal(11, snapshot.RequestSequence);
        Assert.Equal(new ChunkPos(-2, 0), snapshot.CenterChunk);
        Assert.Equal(
            new[] { new ChunkPos(-2, 0), new ChunkPos(-3, 0), new ChunkPos(-1, 0) },
            snapshot.ChunksToLoad);
        Assert.Contains(new ChunkPos(-2, 0), snapshot.ChunksToLoad);
    }

    [Fact]
    public void Plan_RejectsNonPositiveOperationBudget()
    {
        var world = new World(128, 64, WorldMetadata.CreateDefault(seed: 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkStreamingPlanner().Plan(
            world,
            new RectI(0, 0, 32, 32),
            new ChunkStreamingOptions { MaxChunkOperationsPerUpdate = 0 }));
    }

    [Fact]
    public void Plan_RejectsInvalidBackgroundAndApplyQueueLimits()
    {
        var world = new World(128, 64, WorldMetadata.CreateDefault(seed: 1));
        var planner = new ChunkStreamingPlanner();
        var visible = new RectI(0, 0, 32, 32);

        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
            world,
            visible,
            new ChunkStreamingOptions { MaxConcurrentLoadJobs = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
            world,
            visible,
            new ChunkStreamingOptions { MaxConcurrentSaveJobs = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
            world,
            visible,
            new ChunkStreamingOptions { MaxApplyQueueLength = 0 }));
    }

    [Fact]
    public void Plan_RejectsInvalidApplyAndRetryBudgets()
    {
        var world = new World(128, 64, WorldMetadata.CreateDefault(seed: 1));
        var planner = new ChunkStreamingPlanner();
        var visible = new RectI(0, 0, 32, 32);

        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
            world,
            visible,
            new ChunkStreamingOptions { MaxApplyTimePerUpdate = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
            world,
            visible,
            new ChunkStreamingOptions { MaxApplyDecodedBytesPerUpdate = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
            world,
            visible,
            new ChunkStreamingOptions
            {
                RetryPolicy = new ChunkStreamingRetryPolicy { MaxAttempts = 0 }
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
            world,
            visible,
            new ChunkStreamingOptions
            {
                RetryPolicy = new ChunkStreamingRetryPolicy
                {
                    InitialBackoffUpdates = 4,
                    MaxBackoffUpdates = 2
                }
            }));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue - 64)]
    public void Plan_ExtremeVisibleCoordinatesStayBoundedWithoutIntegerWrap(int tileX)
    {
        var world = new World(
            Game.Core.GameConstants.ChunkSize,
            96,
            WorldMetadata.CreateDefault(seed: 99),
            isHorizontallyInfinite: true);

        var plan = new ChunkStreamingPlanner().Plan(
            world,
            new RectI(tileX, 0, 64, 32),
            new ChunkStreamingOptions { LoadMarginChunks = 2, UnloadMarginChunks = 4 });

        Assert.NotEmpty(plan.RequiredChunks);
        Assert.InRange(plan.RequiredChunks.Count, 1, 21);
        Assert.All(plan.RequiredChunks, position => Assert.InRange(position.Y, 0, 2));
    }

    [Fact]
    public void CreateRequestSnapshot_LongBidirectionalCameraTraceRemainsBoundedAndOrdered()
    {
        var world = new World(
            Game.Core.GameConstants.ChunkSize,
            160,
            WorldMetadata.CreateDefault(seed: 912_445),
            isHorizontallyInfinite: true);
        var planner = new ChunkStreamingPlanner();
        var options = new ChunkStreamingOptions
        {
            LoadMarginChunks = 2,
            UnloadMarginChunks = 5,
            MaxChunkOperationsPerUpdate = 8
        };
        long sequence = 0;
        var minCenter = int.MaxValue;
        var maxCenter = int.MinValue;

        for (var step = -2_048; step <= 2_048; step++)
        {
            var tileX = checked(step * 2_048);
            var snapshot = planner.CreateRequestSnapshot(
                world,
                new RectI(tileX, 32, 96, 64),
                worldSessionGeneration: 4,
                requestSequence: ++sequence,
                options);

            Assert.InRange(snapshot.RequiredChunks.Count, 1, 48);
            Assert.Equal(snapshot.CenterChunk, snapshot.ChunksToLoad[0]);
            Assert.All(snapshot.RequiredChunks, position => Assert.InRange(position.Y, 0, 4));
            minCenter = Math.Min(minCenter, snapshot.CenterChunk.X);
            maxCenter = Math.Max(maxCenter, snapshot.CenterChunk.X);
        }

        Assert.True(minCenter < -100_000);
        Assert.True(maxCenter > 100_000);
        Assert.Equal(4_097, sequence);
    }

    [Fact]
    public void CreateRequestSnapshot_CompactWindowsKeepPlanningAllocationBounded()
    {
        var world = new World(
            Game.Core.GameConstants.ChunkSize,
            160,
            WorldMetadata.CreateDefault(seed: 912_445),
            isHorizontallyInfinite: true);
        var planner = new ChunkStreamingPlanner();
        var options = new ChunkStreamingOptions
        {
            LoadMarginChunks = 2,
            UnloadMarginChunks = 5,
            MaxChunkOperationsPerUpdate = 8
        };
        var visible = new RectI(-48, 32, 96, 64);

        for (var index = 0; index < 128; index++)
        {
            GC.KeepAlive(planner.CreateRequestSnapshot(world, visible, 4, index, options).ToPlan());
        }

        const int sampleCount = 1_024;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < sampleCount; index++)
        {
            GC.KeepAlive(planner.CreateRequestSnapshot(world, visible, 4, index, options).ToPlan());
        }

        var allocatedBytesPerPlan = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)sampleCount;

        Assert.True(
            allocatedBytesPerPlan <= 2_500,
            $"Streaming plan allocated {allocatedBytesPerPlan:0.0} B per unchanged camera snapshot.");
    }
}
