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

    private static World CreateWorld()
    {
        return new World(8, 8, WorldMetadata.CreateDefault(seed: 123));
    }
}
