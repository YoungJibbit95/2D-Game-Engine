using Game.Core.Combat;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class MeleeSweepHitShapeTests
{
    [Fact]
    public void Resolve_CreatesSweptShapeThatIntersectsTargetAcrossTipMotion()
    {
        var shape = new MeleeSweepResolver().Resolve(
            Vector2.Zero,
            Vector2.UnitX,
            new MeleeSweepDefinition
            {
                Reach = 40,
                Radius = 4,
                StartAngleRadians = -0.5f,
                EndAngleRadians = 0.5f
            },
            previousProgress: 0,
            currentProgress: 1);

        Assert.True(shape.Intersects(new RectI(32, -3, 8, 6)));
        Assert.False(shape.Intersects(new RectI(-40, -4, 8, 8)));
    }

    [Fact]
    public void Resolve_UsesFacingAsSweepBasis()
    {
        var resolver = new MeleeSweepResolver();
        var definition = new MeleeSweepDefinition
        {
            Reach = 30,
            Radius = 2,
            StartAngleRadians = 0,
            EndAngleRadians = 0
        };

        var right = resolver.Resolve(Vector2.Zero, Vector2.UnitX, definition, 0, 1);
        var left = resolver.Resolve(Vector2.Zero, -Vector2.UnitX, definition, 0, 1);

        Assert.True(right.CurrentTip.X > 0);
        Assert.True(left.CurrentTip.X < 0);
    }
}
