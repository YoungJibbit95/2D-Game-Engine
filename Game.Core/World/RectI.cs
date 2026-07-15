namespace Game.Core.World;

public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    public int Left => X;

    public int Top => Y;

    public int Right => SaturateToInt((long)X + Width);

    public int Bottom => SaturateToInt((long)Y + Height);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y)
    {
        return x >= Left && x < Right && y >= Top && y < Bottom;
    }

    public bool Contains(TilePos position)
    {
        return Contains(position.X, position.Y);
    }

    public bool Intersects(RectI other)
    {
        return Left < other.Right &&
               Right > other.Left &&
               Top < other.Bottom &&
               Bottom > other.Top;
    }

    public RectI Inflate(int amount)
    {
        var left = SaturateToInt((long)X - amount);
        var top = SaturateToInt((long)Y - amount);
        var right = SaturateToInt((long)Right + amount);
        var bottom = SaturateToInt((long)Bottom + amount);
        return new RectI(
            left,
            top,
            SaturateLength((long)right - left),
            SaturateLength((long)bottom - top));
    }

    public RectI ClampTo(RectI bounds)
    {
        if (IsEmpty || bounds.IsEmpty)
        {
            return new RectI(0, 0, 0, 0);
        }

        var left = Math.Max(Left, bounds.Left);
        var top = Math.Max(Top, bounds.Top);
        var right = Math.Min(Right, bounds.Right);
        var bottom = Math.Min(Bottom, bounds.Bottom);
        return right <= left || bottom <= top
            ? new RectI(0, 0, 0, 0)
            : new RectI(left, top, SaturateLength((long)right - left), SaturateLength((long)bottom - top));
    }

    public static RectI FromInclusiveTileBounds(int minX, int minY, int maxX, int maxY)
    {
        return maxX < minX || maxY < minY
            ? new RectI(0, 0, 0, 0)
            : new RectI(
                minX,
                minY,
                SaturateLength((long)maxX - minX + 1),
                SaturateLength((long)maxY - minY + 1));
    }

    private static int SaturateToInt(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }

    private static int SaturateLength(long value)
    {
        return (int)Math.Clamp(value, 0, int.MaxValue);
    }
}
