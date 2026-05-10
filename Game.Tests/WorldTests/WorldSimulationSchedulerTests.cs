using Game.Core;
using Game.Core.World;
using Game.Core.World.Liquids;
using Game.Core.World.Simulation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldSimulationSchedulerTests
{
    [Fact]
    public void Tick_ProcessesLiquidOnlyAfterConfiguredInterval()
    {
        var world = CreateWorld();
        world.SetTile(3, 2, TileInstance.Liquid(255));
        var scheduler = new WorldSimulationScheduler();
        scheduler.MarkLiquidTile(new TilePos(3, 2));
        var options = new WorldSimulationOptions(LiquidStepIntervalSeconds: 0.5f, SeedExistingLiquids: false);

        var early = scheduler.Tick(world, 0.25f, new LiquidSimulationSystem(), options);
        var after = scheduler.Tick(world, 0.25f, new LiquidSimulationSystem(), options);

        Assert.Equal(0, early.Liquids.MovedLiquid);
        Assert.True(after.Liquids.MovedLiquid > 0);
        Assert.True(after.LiquidRegionsProcessed > 0);
    }

    [Fact]
    public void Tick_RequeuesChangedLiquidRegionsForFutureSteps()
    {
        var world = CreateWorld();
        world.SetTile(3, 2, TileInstance.Liquid(255));
        var scheduler = new WorldSimulationScheduler();
        scheduler.MarkLiquidTile(new TilePos(3, 2));
        var options = new WorldSimulationOptions(LiquidStepIntervalSeconds: 0, SeedExistingLiquids: false);

        var result = scheduler.Tick(world, 0.016f, new LiquidSimulationSystem(), options);

        Assert.True(result.Liquids.MovedLiquid > 0);
        Assert.NotEmpty(result.LiquidDirtyRegions);
        Assert.NotEmpty(result.RenderDirtyRegions);
        Assert.NotEmpty(result.LightDirtyRegions);
    }

    [Fact]
    public void SeedExistingLiquids_AddsLiquidRegions()
    {
        var world = CreateWorld();
        world.SetTile(1, 1, TileInstance.Liquid(128));
        var scheduler = new WorldSimulationScheduler();

        scheduler.SeedExistingLiquids(world);

        Assert.True(scheduler.PendingLiquidRegionCount > 0);
    }

    [Fact]
    public void SeedExistingLiquids_ScansLoadedChunksInHorizontallyInfiniteWorld()
    {
        var world = new World(GameConstants.ChunkSize, 8, WorldMetadata.CreateDefault(seed: 1), isHorizontallyInfinite: true);
        world.SetTile(-1, 1, TileInstance.Liquid(128));
        var scheduler = new WorldSimulationScheduler();

        scheduler.SeedExistingLiquids(world);

        Assert.True(scheduler.PendingLiquidRegionCount > 0);
    }

    private static World CreateWorld()
    {
        return new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
    }
}
