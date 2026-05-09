using System.Numerics;

namespace Game.Core.World.Queries;

public readonly record struct AreaQueryShape(
    AreaQueryShapeKind Kind,
    RectI Bounds,
    Vector2 Origin,
    Vector2 Direction,
    float Radius,
    float HalfAngleRadians)
{
    public static AreaQueryShape Rectangle(RectI bounds)
    {
        return new AreaQueryShape(AreaQueryShapeKind.Rectangle, bounds, Vector2.Zero, Vector2.UnitX, 0, 0);
    }

    public static AreaQueryShape Circle(Vector2 center, float radius)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        var bounds = new RectI(
            (int)MathF.Floor(center.X - radius),
            (int)MathF.Floor(center.Y - radius),
            (int)MathF.Ceiling(radius * 2),
            (int)MathF.Ceiling(radius * 2));
        return new AreaQueryShape(AreaQueryShapeKind.Circle, bounds, center, Vector2.UnitX, radius, 0);
    }

    public static AreaQueryShape Cone(Vector2 origin, Vector2 direction, float radius, float angleRadians)
    {
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (direction == Vector2.Zero)
        {
            direction = Vector2.UnitX;
        }

        direction = Vector2.Normalize(direction);
        var bounds = new RectI(
            (int)MathF.Floor(origin.X - radius),
            (int)MathF.Floor(origin.Y - radius),
            (int)MathF.Ceiling(radius * 2),
            (int)MathF.Ceiling(radius * 2));
        return new AreaQueryShape(AreaQueryShapeKind.Cone, bounds, origin, direction, radius, Math.Clamp(angleRadians * 0.5f, 0, MathF.PI));
    }

    public bool Intersects(RectI target)
    {
        return Kind switch
        {
            AreaQueryShapeKind.Rectangle => Bounds.Intersects(target),
            AreaQueryShapeKind.Circle => IntersectsCircle(target),
            AreaQueryShapeKind.Cone => IntersectsCone(target),
            _ => false
        };
    }

    private bool IntersectsCircle(RectI target)
    {
        var closestX = Math.Clamp(Origin.X, target.Left, target.Right);
        var closestY = Math.Clamp(Origin.Y, target.Top, target.Bottom);
        var delta = new Vector2(closestX, closestY) - Origin;
        return delta.LengthSquared() <= Radius * Radius;
    }

    private bool IntersectsCone(RectI target)
    {
        var center = new Vector2(
            target.Left + target.Width * 0.5f,
            target.Top + target.Height * 0.5f);
        var delta = center - Origin;
        var distance = delta.Length();
        if (distance > Radius)
        {
            return false;
        }

        if (distance <= 0.0001f)
        {
            return true;
        }

        var dot = Vector2.Dot(Vector2.Normalize(delta), Direction);
        return dot >= MathF.Cos(HalfAngleRadians);
    }
}
