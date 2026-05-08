namespace Game.Core.World;

public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    public int Left => X;

    public int Top => Y;

    public int Right => X + Width;

    public int Bottom => Y + Height;

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
        return new RectI(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);
    }
}
