namespace Game.Core.UI;

public readonly record struct UiRect(float X, float Y, float Width, float Height)
{
    public float Left => X;

    public float Top => Y;

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public UiPoint Position => new(X, Y);

    public UiSize Size => new(Width, Height);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(UiPoint point)
    {
        return point.X >= Left &&
               point.X < Right &&
               point.Y >= Top &&
               point.Y < Bottom;
    }

    public UiRect Deflate(UiThickness thickness)
    {
        var width = Math.Max(0, Width - thickness.Left - thickness.Right);
        var height = Math.Max(0, Height - thickness.Top - thickness.Bottom);
        return new UiRect(X + thickness.Left, Y + thickness.Top, width, height);
    }

    public UiRect Translate(float offsetX, float offsetY)
    {
        return new UiRect(X + offsetX, Y + offsetY, Width, Height);
    }
}
