using Game.Core.Interaction;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.InteractionTests;

public sealed class InteractionTargetingSystemTests
{
    [Fact]
    public void FindMiningTarget_ReturnsFirstSolidTileAlongAimRay()
    {
        var world = CreateWorld();
        world.SetTile(4, 2, KnownTileIds.Stone);
        world.SetTile(5, 2, KnownTileIds.Dirt);

        var target = new InteractionTargetingSystem().FindMiningTarget(
            world,
            new Vector2(16, 40),
            new Vector2(120, 40),
            reachPixels: 160);

        Assert.True(target.Found);
        Assert.Equal(new TilePos(4, 2), target.TilePosition);
    }

    [Fact]
    public void FindMiningTarget_CanTargetPassThroughMineableTiles()
    {
        var world = CreateWorld();
        world.SetTile(4, 2, TileInstance.FromTileId(KnownTileIds.Wood, isSolid: false));

        var target = new InteractionTargetingSystem().FindMiningTarget(
            world,
            new Vector2(16, 40),
            new Vector2(120, 40),
            reachPixels: 160);

        Assert.True(target.Found);
        Assert.Equal(new TilePos(4, 2), target.TilePosition);
    }

    [Fact]
    public void FindMiningTarget_ClampsToReach()
    {
        var world = CreateWorld();
        world.SetTile(8, 2, KnownTileIds.Stone);

        var target = new InteractionTargetingSystem().FindMiningTarget(
            world,
            new Vector2(16, 40),
            new Vector2(160, 40),
            reachPixels: 32);

        Assert.False(target.Found);
    }

    [Fact]
    public void FindPlacementTarget_RejectsTargetBehindSolidTile()
    {
        var world = CreateWorld();
        world.SetTile(3, 2, KnownTileIds.Stone);

        var target = new InteractionTargetingSystem().FindPlacementTarget(
            world,
            new Vector2(16, 40),
            new Vector2(88, 40),
            reachPixels: 160);

        Assert.False(target.Found);
    }

    private static World CreateWorld()
    {
        return new World(10, 8, WorldMetadata.CreateDefault(seed: 1));
    }
}
