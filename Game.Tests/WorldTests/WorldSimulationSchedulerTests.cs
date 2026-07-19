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
    public void Tick_ContinuesBudgetedActiveLiquidWorkWithoutNewDirtyRegions()
    {
        var world = CreateWorld();
        ConfigureEnclosedLiquidCell(world, 2, 2);
        ConfigureEnclosedLiquidCell(world, 5, 2);
        var scheduler = new WorldSimulationScheduler();
        scheduler.MarkLiquidRegion(new RectI(0, 0, 8, 8), padding: 0);
        var liquids = new LiquidSimulationSystem();
        var options = new WorldSimulationOptions(
            LiquidStepIntervalSeconds: 0,
            LiquidRegionPaddingTiles: 0,
            SeedExistingLiquids: false,
            LiquidOptions: LiquidSimulationOptions.Default with
            {
                MaxCellsPerStep = 1,
                MaxTransferOperationsPerStep = 3
            });

        var first = scheduler.Tick(world, 0.016f, liquids, options);
        var second = scheduler.Tick(world, 0.016f, liquids, options);

        Assert.Equal(1, first.Liquids.ProcessedCells);
        Assert.True(first.Liquids.PendingActiveCells > 0);
        Assert.Empty(first.Liquids.ChangedRegions);
        Assert.Equal(1, second.Liquids.ProcessedCells);
    }

    [Fact]
    public void SeedExistingLiquids_AddsLiquidRegions()
    {
        var world = CreateWorld();
        var scheduler = new WorldSimulationScheduler();

        scheduler.SeedExistingLiquids(world);

        Assert.True(scheduler.PendingLiquidRegionCount > 0);
    }

    [Fact]
    public void Tick_SeedsExistingWorldThroughTheLiquidScanBudget()
    {
        var world = new World(256, 128, WorldMetadata.CreateDefault(seed: 1));
        var scheduler = new WorldSimulationScheduler();
        var options = WorldSimulationOptions.Default with
        {
            LiquidStepIntervalSeconds = 0,
            LiquidOptions = LiquidSimulationOptions.Default with
            {
                MaxSeedTileChecksPerStep = 17
            }
        };

        var result = scheduler.Tick(world, 0.016f, new LiquidSimulationSystem(), options);

        Assert.Equal(17, result.Liquids.SeedTilesChecked);
        Assert.True(result.Liquids.PendingSeedRegions > 0);
        Assert.True(result.Liquids.SeedBudgetExhausted);
    }

    [Fact]
    public void Tick_RebindsInitialLiquidSeedingWhenSchedulerMovesToAnotherWorld()
    {
        var scheduler = new WorldSimulationScheduler();
        var liquids = new LiquidSimulationSystem();
        var options = WorldSimulationOptions.Default with
        {
            LiquidStepIntervalSeconds = 0,
            LiquidOptions = LiquidSimulationOptions.Default with
            {
                MaxSeedTileChecksPerStep = 1
            }
        };

        scheduler.Tick(CreateWorld(), 0.016f, liquids, options);
        var second = scheduler.Tick(CreateWorld(), 0.016f, liquids, options);

        Assert.Equal(1, second.Liquids.SeedTilesChecked);
        Assert.True(second.Liquids.PendingSeedRegions > 0);
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

    private static void ConfigureEnclosedLiquidCell(World world, int x, int y)
    {
        world.SetTile(x, y, TileInstance.Liquid(255));
        world.SetTile(x, y - 1, KnownTileIds.Stone);
        world.SetTile(x, y + 1, KnownTileIds.Stone);
        world.SetTile(x - 1, y, KnownTileIds.Stone);
        world.SetTile(x + 1, y, KnownTileIds.Stone);
    }
}
