using System.Numerics;
using Game.Core.World;

namespace Game.Core.Maps;

public sealed class TopDownMapBody
{
    public TopDownMapBody(Vector2 position, Vector2? size = null)
    {
        Position = position;
        Size = size ?? new Vector2(12, 12);
    }

    public Vector2 Position { get; set; }

    public Vector2 Velocity { get; set; }

    public Vector2 Size { get; set; }

    public TopDownFacing Facing { get; set; } = TopDownFacing.Down;

    public Vector2 Center => Position + Size / 2f;

    public RectI BoundsPixels
    {
        get
        {
            var left = (int)MathF.Floor(Position.X);
            var top = (int)MathF.Floor(Position.Y);
            var right = (int)MathF.Ceiling(Position.X + Size.X);
            var bottom = (int)MathF.Ceiling(Position.Y + Size.Y);
            return new RectI(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }
    }

    public TilePos CenterTile(int tileSize)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), "Tile size must be positive.");
        }

        return new TilePos(FloorToTile(Center.X, tileSize), FloorToTile(Center.Y, tileSize));
    }

    public RectI BoundsTiles(int tileSize)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), "Tile size must be positive.");
        }

        var bounds = BoundsPixels;
        if (bounds.IsEmpty)
        {
            return new RectI(0, 0, 0, 0);
        }

        var minX = FloorToTile(bounds.Left, tileSize);
        var minY = FloorToTile(bounds.Top, tileSize);
        var maxX = FloorToTile(bounds.Right - 1, tileSize);
        var maxY = FloorToTile(bounds.Bottom - 1, tileSize);
        return RectI.FromInclusiveTileBounds(minX, minY, maxX, maxY);
    }

    private static int FloorToTile(float pixel, int tileSize)
    {
        return (int)MathF.Floor(pixel / tileSize);
    }
}
