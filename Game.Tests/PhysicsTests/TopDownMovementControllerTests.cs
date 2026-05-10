using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.PhysicsTests;

public sealed class TopDownMovementControllerTests
{
    [Fact]
    public void Move_NormalizesDiagonalMovement()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
        var body = new PhysicsBody { Position = Vector2.Zero, Size = new Vector2(8, 8) };

        new TopDownMovementController().Move(
            world,
            body,
            new Vector2(1, 1),
            deltaSeconds: 1f,
            new TopDownMovementOptions { MoveSpeedPixelsPerSecond = 10 });

        Assert.Equal(7.071f, body.Position.X, precision: 3);
        Assert.Equal(7.071f, body.Position.Y, precision: 3);
    }

    [Fact]
    public void Move_ResolvesTopDownCollisionAgainstSolidTiles()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(2, 0, TileInstance.FromTileId(KnownTileIds.Stone, isSolid: true));
        var body = new PhysicsBody { Position = new Vector2(16, 0), Size = new Vector2(16, 16) };

        new TopDownMovementController().Move(
            world,
            body,
            Vector2.UnitX,
            deltaSeconds: 1f,
            new TopDownMovementOptions { MoveSpeedPixelsPerSecond = 32 });

        Assert.Equal(16, body.Position.X);
        Assert.Equal(0, body.Velocity.X);
    }
}
