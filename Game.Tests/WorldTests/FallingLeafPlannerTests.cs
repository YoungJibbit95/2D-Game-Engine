using Game.Core;
using Game.Core.World;
using Game.Core.World.Vegetation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class FallingLeafPlannerTests
{
    [Fact]
    public void Plan_IsDeterministicAndUsesOnlyAuthoritativeCanopyEdges()
    {
        var world = CreateCanopyWorld();
        var visible = new RectI(0, 0, 64 * GameConstants.TileSize, 48 * GameConstants.TileSize);
        var first = new FallingLeafSpawn[6];
        var second = new FallingLeafSpawn[6];

        var firstCount = FallingLeafPlanner.Plan(
            world,
            visible,
            surfaceTileY: 32,
            tickNumber: 72,
            wind: 0.65f,
            vegetationDensity: 3f,
            first);
        var secondCount = FallingLeafPlanner.Plan(
            world,
            visible,
            surfaceTileY: 32,
            tickNumber: 72,
            wind: 0.65f,
            vegetationDensity: 3f,
            second);

        Assert.InRange(firstCount, 1, first.Length);
        Assert.Equal(firstCount, secondCount);
        Assert.Equal(first.AsSpan(0, firstCount).ToArray(), second.AsSpan(0, secondCount).ToArray());
        for (var index = 0; index < firstCount; index++)
        {
            var tile = CoordinateUtils.WorldToTile(first[index].WorldPosition);
            Assert.True(KnownTileIds.IsFoliage(world.GetTile(tile.X, tile.Y).TileId));
            Assert.True(first[index].InitialVelocity.X > 0f);
        }
    }

    [Fact]
    public void Plan_RemainsAllocationFreeWithCallerOwnedBuffer()
    {
        var world = CreateCanopyWorld();
        var visible = new RectI(0, 0, 64 * GameConstants.TileSize, 48 * GameConstants.TileSize);
        var destination = new FallingLeafSpawn[6];
        for (var tick = 0L; tick < 64; tick++)
        {
            FallingLeafPlanner.Plan(world, visible, 32, tick, -0.4f, 1.5f, destination);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 64L; tick < 1_064; tick++)
        {
            FallingLeafPlanner.Plan(world, visible, 32, tick, -0.4f, 1.5f, destination);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Plan_RejectsUndenseOrCanopyFreeRegions()
    {
        var world = CreateCanopyWorld();
        var destination = new FallingLeafSpawn[4];

        Assert.Equal(0, FallingLeafPlanner.Plan(
            world,
            new RectI(0, 0, 64 * GameConstants.TileSize, 48 * GameConstants.TileSize),
            32,
            24,
            0f,
            0f,
            destination));
        Assert.Equal(0, FallingLeafPlanner.Plan(
            world,
            new RectI(80 * GameConstants.TileSize, 0, 32 * GameConstants.TileSize, 48 * GameConstants.TileSize),
            32,
            24,
            0f,
            3f,
            destination));
    }

    private static World CreateCanopyWorld()
    {
        var world = new World(
            128,
            96,
            new WorldMetadata("falling-leaf-test", 17, DateTimeOffset.UnixEpoch));
        ushort[] foliageIds =
        [
            KnownTileIds.Leaves,
            KnownTileIds.OakLeaves,
            KnownTileIds.AutumnLeaves,
            KnownTileIds.MarshLeaves
        ];
        for (var y = 20; y <= 24; y++)
        {
            for (var x = 20; x <= 28; x++)
            {
                if ((x + y) % 5 != 0)
                {
                    world.SetTile(
                        x,
                        y,
                        TileInstance.FromTileId(
                            foliageIds[(x + y) & 3],
                            TileFlags.IsNatural,
                            isSolid: false));
                }
            }
        }

        return world;
    }
}
