using Game.Core;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.PhysicsTests;

public sealed class TileCollisionResolverTests
{
    [Fact]
    public void Move_FallingBodyLandsOnSolidTile()
    {
        var world = CreateWorld();
        world.SetTile(1, 10, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(GameConstants.TileSize, 9 * GameConstants.TileSize - 10),
            Size = new Vector2(10, 10),
            Velocity = new Vector2(0, 100)
        };

        new TileCollisionResolver().Move(world, body, 1f);

        Assert.Equal(10 * GameConstants.TileSize - body.Size.Y, body.Position.Y);
        Assert.Equal(0, body.Velocity.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void Move_BodyStopsAgainstSolidTileOnRight()
    {
        var world = CreateWorld();
        world.SetTile(5, 5, KnownTileIds.Dirt);
        var body = new PhysicsBody
        {
            Position = new Vector2(4 * GameConstants.TileSize, 5 * GameConstants.TileSize),
            Size = new Vector2(16, 16),
            Velocity = new Vector2(100, 0)
        };

        new TileCollisionResolver().Move(world, body, 1f);

        Assert.Equal(5 * GameConstants.TileSize - body.Size.X, body.Position.X);
        Assert.Equal(0, body.Velocity.X);
    }

    private static World CreateWorld()
    {
        return new World(32, 32, WorldMetadata.CreateDefault(seed: 1));
    }
}
