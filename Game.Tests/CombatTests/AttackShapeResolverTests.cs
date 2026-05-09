using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.World.Queries;
using System.Numerics;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class AttackShapeResolverTests
{
    [Fact]
    public void Resolve_RectangleCreatesForwardHitbox()
    {
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());

        var shape = new AttackShapeResolver().Resolve(player, new Vector2(100, 0), new AttackShapeDefinition
        {
            Kind = AttackShapeKind.Rectangle,
            Range = 48,
            Height = 24
        });

        Assert.Equal(AreaQueryShapeKind.Rectangle, shape.Kind);
        Assert.True(shape.Bounds.X >= player.Bounds.Right);
        Assert.Equal(48, shape.Bounds.Width);
    }

    [Fact]
    public void Resolve_ConeUsesAimDirection()
    {
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());

        var shape = new AttackShapeResolver().Resolve(player, new Vector2(100, 0), new AttackShapeDefinition
        {
            Kind = AttackShapeKind.Cone,
            Range = 64,
            AngleRadians = MathF.PI / 2
        });

        Assert.Equal(AreaQueryShapeKind.Cone, shape.Kind);
        Assert.Equal(64, shape.Radius);
        Assert.True(shape.Direction.X > 0);
    }
}
