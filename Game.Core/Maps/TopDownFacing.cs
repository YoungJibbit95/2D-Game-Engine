using System.Numerics;

namespace Game.Core.Maps;

public enum TopDownFacing
{
    Down,
    Left,
    Right,
    Up
}

public static class TopDownFacingExtensions
{
    public static Vector2 ToVector(this TopDownFacing facing)
    {
        return facing switch
        {
            TopDownFacing.Left => new Vector2(-1, 0),
            TopDownFacing.Right => new Vector2(1, 0),
            TopDownFacing.Up => new Vector2(0, -1),
            _ => new Vector2(0, 1)
        };
    }

    public static TopDownFacing FromVector(Vector2 direction, TopDownFacing fallback = TopDownFacing.Down)
    {
        if (direction.LengthSquared() <= float.Epsilon)
        {
            return fallback;
        }

        return MathF.Abs(direction.X) > MathF.Abs(direction.Y)
            ? direction.X < 0 ? TopDownFacing.Left : TopDownFacing.Right
            : direction.Y < 0 ? TopDownFacing.Up : TopDownFacing.Down;
    }

    public static TopDownFacing Parse(string? value, TopDownFacing fallback = TopDownFacing.Down)
    {
        return Enum.TryParse<TopDownFacing>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }
}
