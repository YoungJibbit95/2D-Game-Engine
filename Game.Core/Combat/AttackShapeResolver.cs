using Game.Core.Entities;
using Game.Core.World;
using Game.Core.World.Queries;
using System.Numerics;

namespace Game.Core.Combat;

public sealed class AttackShapeResolver
{
    public AreaQueryShape Resolve(PlayerEntity player, Vector2 targetWorldPosition, AttackShapeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(definition);

        var center = player.Body.Center;
        var direction = targetWorldPosition - center;
        if (direction == Vector2.Zero)
        {
            direction = Vector2.UnitX;
        }

        direction = Vector2.Normalize(direction);
        return definition.Kind switch
        {
            AttackShapeKind.Circle => AreaQueryShape.Circle(center + direction * definition.Range * 0.5f, definition.Range),
            AttackShapeKind.Cone => AreaQueryShape.Cone(center, direction, definition.Range, definition.AngleRadians),
            _ => AreaQueryShape.Rectangle(CreateRectangle(player.Bounds, center, direction, definition))
        };
    }

    private static RectI CreateRectangle(RectI playerBounds, Vector2 center, Vector2 direction, AttackShapeDefinition definition)
    {
        var width = Math.Max(1, (int)MathF.Ceiling(definition.Width));
        var height = Math.Max(1, (int)MathF.Ceiling(definition.Height));
        var range = Math.Max(1, (int)MathF.Ceiling(definition.Range));

        if (Math.Abs(direction.X) >= Math.Abs(direction.Y))
        {
            var x = direction.X >= 0 ? playerBounds.Right : playerBounds.Left - range;
            var y = (int)MathF.Floor(center.Y - height / 2f);
            return new RectI(x, y, range, height);
        }

        var verticalY = direction.Y >= 0 ? playerBounds.Bottom : playerBounds.Top - range;
        var verticalX = (int)MathF.Floor(center.X - width / 2f);
        return new RectI(verticalX, verticalY, width, range);
    }
}
