using Game.Core;
using Game.Core.World;
using Game.Core.World.Liquids;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class LiquidSimulationSystemTests
{
    [Fact]
    public void Step_MovesLiquidDownIntoEmptyTile()
    {
        var world = CreateWorld();
        world.SetTile(3, 2, TileInstance.Liquid(255));

        var result = new LiquidSimulationSystem().Step(world, new RectI(0, 0, 8, 8));

        Assert.True(result.MovedLiquid > 0);
        Assert.True(result.ChangedTiles >= 2);
        Assert.NotEmpty(result.ChangedRegions);
        Assert.False(world.GetTile(3, 2).HasLiquid);
        Assert.True(world.GetTile(3, 3).HasLiquid);
        Assert.Equal(255, world.GetTile(3, 3).LiquidAmount);
    }

    [Fact]
    public void Step_DoesNotMoveLiquidIntoSolidTiles()
    {
        var world = CreateWorld();
        world.SetTile(3, 2, TileInstance.Liquid(255));
        world.SetTile(3, 3, KnownTileIds.Stone);

        new LiquidSimulationSystem().Step(world, new RectI(0, 0, 8, 8));

        Assert.True(world.GetTile(3, 2).HasLiquid);
        Assert.False(world.GetTile(3, 3).HasLiquid);
        Assert.True(world.GetTile(3, 3).IsSolid);
    }

    [Fact]
    public void Step_BalancesLiquidSidewaysWhenBlockedBelow()
    {
        var world = CreateWorld();
        world.SetTile(3, 2, TileInstance.Liquid(200));
        world.SetTile(3, 3, KnownTileIds.Stone);
        world.SetTile(2, 3, KnownTileIds.Stone);
        world.SetTile(4, 3, KnownTileIds.Stone);

        new LiquidSimulationSystem().Step(world, new RectI(0, 0, 8, 8));

        var left = world.GetTile(2, 2).LiquidAmount;
        var right = world.GetTile(4, 2).LiquidAmount;
        Assert.True(left + right > 0);
        Assert.True(world.GetTile(3, 2).LiquidAmount < 200);
    }

    [Fact]
    public void StepMany_CombinesChangedRegions()
    {
        var world = CreateWorld();
        world.SetTile(2, 1, TileInstance.Liquid(255));
        world.SetTile(5, 1, TileInstance.Liquid(255));

        var result = new LiquidSimulationSystem().Step(world, new[]
        {
            new RectI(0, 0, 4, 4),
            new RectI(4, 0, 4, 4)
        });

        Assert.True(result.MovedLiquid > 0);
        Assert.True(result.ChangedRegions.Count >= 1);
    }

    [Fact]
    public void Step_AllowsNegativeXInHorizontallyInfiniteWorld()
    {
        var world = new World(GameConstants.ChunkSize, 8, WorldMetadata.CreateDefault(seed: 1), isHorizontallyInfinite: true);
        world.SetTile(-1, 1, TileInstance.Liquid(255));

        var result = new LiquidSimulationSystem().Step(world, new RectI(-2, 0, 4, 4));

        Assert.True(result.MovedLiquid > 0);
        Assert.True(world.GetTile(-1, 2).HasLiquid);
    }

    [Fact]
    public void ActiveCells_PropagateAcrossStepsAndActivateNeighbors()
    {
        var world = CreateWorld();
        world.SetTile(3, 1, TileInstance.Liquid(255));
        var workspace = new LiquidSimulationWorkspace();
        var system = new LiquidSimulationSystem();
        workspace.Activate(new TilePos(3, 1));

        var first = system.Step(world, workspace);

        Assert.Equal(255, first.MovedLiquid);
        Assert.Equal(1, first.ProcessedCells);
        Assert.True(first.PendingActiveCells > 0);
        Assert.True(world.GetTile(3, 2).HasLiquid);
        Assert.False(world.GetTile(3, 3).HasLiquid);

        var second = system.Step(world, workspace);

        Assert.True(second.MovedLiquid > 0);
        Assert.True(world.GetTile(3, 3).HasLiquid);
    }

    [Fact]
    public void Activate_DeduplicatesPendingCellsAndReportsCapacityExhaustion()
    {
        var workspace = new LiquidSimulationWorkspace(
            maximumQueuedCells: 2,
            maximumQueuedRegions: 1,
            initialCapacity: 2);

        Assert.True(workspace.Activate(new TilePos(1, 1)));
        Assert.False(workspace.Activate(new TilePos(1, 1)));
        Assert.True(workspace.Activate(new TilePos(2, 1)));
        Assert.False(workspace.Activate(new TilePos(3, 1)));
        Assert.Equal(2, workspace.PendingActiveCellCount);

        var result = new LiquidSimulationSystem().Step(CreateWorld(), workspace);

        Assert.Equal(1, result.DroppedActivations);
        Assert.True(result.CapacityExhausted);
    }

    [Fact]
    public void Step_EnforcesCellAndTransferOperationBudgets()
    {
        var world = CreateWorld();
        world.SetTile(2, 1, TileInstance.Liquid(255));
        world.SetTile(5, 1, TileInstance.Liquid(255));
        var workspace = new LiquidSimulationWorkspace();
        workspace.Activate(new TilePos(2, 1));
        workspace.Activate(new TilePos(5, 1));
        var options = LiquidSimulationOptions.Default with
        {
            MaxCellsPerStep = 1,
            MaxTransferOperationsPerStep = 1
        };

        var result = new LiquidSimulationSystem().Step(world, workspace, options);

        Assert.Equal(1, result.ProcessedCells);
        Assert.Equal(1, result.TransferOperations);
        Assert.True(result.CellBudgetExhausted);
        Assert.True(result.TransferBudgetExhausted);
        Assert.True(result.PendingActiveCells > 0);
    }

    [Fact]
    public void RegionSeeding_IsBudgetedAndEventuallyCompletesWithoutStarvation()
    {
        var world = CreateWorld();
        var workspace = new LiquidSimulationWorkspace();
        workspace.ActivateRegion(new RectI(0, 0, 8, 8));
        var system = new LiquidSimulationSystem();
        var options = LiquidSimulationOptions.Default with
        {
            MaxSeedTileChecksPerStep = 7
        };
        var checkedTiles = 0;
        var steps = 0;

        while (workspace.HasPendingWork)
        {
            var result = system.Step(world, workspace, options);
            checkedTiles += result.SeedTilesChecked;
            steps++;
            Assert.InRange(result.SeedTilesChecked, 1, 7);
            Assert.True(steps <= 10);
        }

        Assert.Equal(64, checkedTiles);
        Assert.Equal(10, steps);
    }

    [Fact]
    public void ActiveQueue_ProcessesOldestCellsWithoutStarvation()
    {
        var world = CreateWorld();
        var workspace = new LiquidSimulationWorkspace();
        for (var x = 0; x < 8; x++)
        {
            workspace.Activate(new TilePos(x, 1));
        }

        var options = LiquidSimulationOptions.Default with
        {
            MaxCellsPerStep = 2,
            MaxTransferOperationsPerStep = 6
        };
        var system = new LiquidSimulationSystem();
        var processed = 0;
        for (var step = 0; step < 4; step++)
        {
            processed += system.Step(world, workspace, options).ProcessedCells;
        }

        Assert.Equal(8, processed);
        Assert.False(workspace.HasPendingWork);
    }

    [Fact]
    public void ActiveSimulation_IsDeterministicAndConservesLiquid()
    {
        var firstWorld = CreateWorld();
        var secondWorld = CreateWorld();
        ConfigureBasin(firstWorld);
        ConfigureBasin(secondWorld);
        var firstWorkspace = new LiquidSimulationWorkspace();
        var secondWorkspace = new LiquidSimulationWorkspace();
        firstWorkspace.Activate(new TilePos(3, 1));
        secondWorkspace.Activate(new TilePos(3, 1));
        var system = new LiquidSimulationSystem();

        for (var step = 0; step < 12; step++)
        {
            var first = system.Step(firstWorld, firstWorkspace);
            var second = system.Step(secondWorld, secondWorkspace);
            Assert.Equal(first.ProcessedCells, second.ProcessedCells);
            Assert.Equal(first.TransferOperations, second.TransferOperations);
            Assert.Equal(first.MovedLiquid, second.MovedLiquid);
            Assert.Equal(first.PendingActiveCells, second.PendingActiveCells);
        }

        var firstTotal = 0;
        var secondTotal = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var firstTile = firstWorld.GetTile(x, y);
                var secondTile = secondWorld.GetTile(x, y);
                Assert.Equal(firstTile, secondTile);
                firstTotal += firstTile.LiquidAmount;
                secondTotal += secondTile.LiquidAmount;
            }
        }

        Assert.Equal(255, firstTotal);
        Assert.Equal(firstTotal, secondTotal);
    }

    [Fact]
    public void ActiveSimulation_DoesNotMaterializeUnloadedNeighborChunk()
    {
        var world = new World(
            GameConstants.ChunkSize,
            8,
            WorldMetadata.CreateDefault(seed: 7),
            isHorizontallyInfinite: true);
        world.SetTile(GameConstants.ChunkSize - 1, 1, TileInstance.Liquid(255));
        world.SetTile(GameConstants.ChunkSize - 1, 2, KnownTileIds.Stone);
        var workspace = new LiquidSimulationWorkspace();
        workspace.Activate(new TilePos(GameConstants.ChunkSize - 1, 1));

        new LiquidSimulationSystem().Step(world, workspace);

        Assert.False(world.TryGetChunk(new ChunkPos(1, 0), out _));
    }

    [Fact]
    public void ActiveSimulation_DoesNotWrapAtMinimumInfiniteWorldX()
    {
        var world = new World(
            GameConstants.ChunkSize,
            8,
            WorldMetadata.CreateDefault(seed: 8),
            isHorizontallyInfinite: true);
        world.SetTile(int.MinValue, 1, TileInstance.Liquid(255));
        world.SetTile(int.MinValue, 2, KnownTileIds.Stone);
        var workspace = new LiquidSimulationWorkspace();
        workspace.Activate(new TilePos(int.MinValue, 1));

        var result = new LiquidSimulationSystem().Step(world, workspace);

        Assert.True(result.MovedLiquid > 0);
        Assert.True(world.GetTile(int.MinValue + 1, 1).HasLiquid);
        Assert.False(world.GetTile(int.MaxValue, 1).HasLiquid);
    }

    private static World CreateWorld()
    {
        return new World(8, 8, WorldMetadata.CreateDefault(seed: 123));
    }

    private static void ConfigureBasin(World world)
    {
        world.SetTile(3, 1, TileInstance.Liquid(255));
        for (var x = 1; x <= 5; x++)
        {
            world.SetTile(x, 6, KnownTileIds.Stone);
        }

        for (var y = 1; y <= 6; y++)
        {
            world.SetTile(0, y, KnownTileIds.Stone);
            world.SetTile(6, y, KnownTileIds.Stone);
        }
    }
}
