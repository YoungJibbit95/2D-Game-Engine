using System.Numerics;

namespace Game.Core.Movement;

internal static class TopDownMovementMath
{
    public static float ResolveSpeed(TopDownMovementOptions options)
    {
        return float.IsFinite(options.MoveSpeedPixelsPerSecond)
            ? Math.Max(0f, options.MoveSpeedPixelsPerSecond)
            : 0f;
    }

    public static Vector2 ResolveDirection(Vector2 direction, TopDownMovementOptions options)
    {
        if (!float.IsFinite(direction.X) ||
            !float.IsFinite(direction.Y) ||
            direction.LengthSquared() <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        if (!options.AllowDiagonalMovement)
        {
            direction = MathF.Abs(direction.X) >= MathF.Abs(direction.Y)
                ? new Vector2(MathF.Sign(direction.X), 0)
                : new Vector2(0, MathF.Sign(direction.Y));
        }

        if (options.NormalizeDiagonalSpeed && direction.LengthSquared() > 1f)
        {
            direction = Vector2.Normalize(direction);
        }

        return direction;
    }
}
