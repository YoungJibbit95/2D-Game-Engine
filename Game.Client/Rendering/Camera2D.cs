using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class Camera2D
{
    public Vector2 Position { get; set; }

    public float Zoom { get; set; } = 2f;

    public Matrix ViewMatrix { get; private set; } = Matrix.Identity;

    public Rectangle VisibleWorldRect { get; private set; }

    public void Follow(Vector2 targetWorldPosition, Rectangle viewportBounds, float smoothing = 1f)
    {
        smoothing = MathHelper.Clamp(smoothing, 0f, 1f);
        Position = Vector2.Lerp(Position, targetWorldPosition, smoothing);
        Recalculate(viewportBounds);
    }

    public void Recalculate(Rectangle viewportBounds)
    {
        var zoom = Math.Max(0.1f, Zoom);
        var halfViewportWorld = new Vector2(viewportBounds.Width, viewportBounds.Height) * 0.5f / zoom;
        var topLeft = Position - halfViewportWorld;
        var size = halfViewportWorld * 2f;

        VisibleWorldRect = new Rectangle(
            (int)MathF.Floor(topLeft.X),
            (int)MathF.Floor(topLeft.Y),
            (int)MathF.Ceiling(size.X),
            (int)MathF.Ceiling(size.Y));

        ViewMatrix =
            Matrix.CreateTranslation(new Vector3(-Position, 0f)) *
            Matrix.CreateScale(zoom, zoom, 1f) *
            Matrix.CreateTranslation(viewportBounds.Width * 0.5f, viewportBounds.Height * 0.5f, 0f);
    }

    public Vector2 WorldToScreen(Vector2 worldPosition, Rectangle viewportBounds)
    {
        return (worldPosition - Position) * Zoom + new Vector2(viewportBounds.Width, viewportBounds.Height) * 0.5f;
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition, Rectangle viewportBounds)
    {
        return (screenPosition - new Vector2(viewportBounds.Width, viewportBounds.Height) * 0.5f) / Zoom + Position;
    }

    public IEnumerable<ChunkPos> GetVisibleChunks()
    {
        var minTile = CoordinateUtils.WorldToTile(VisibleWorldRect.Left, VisibleWorldRect.Top);
        var maxTile = CoordinateUtils.WorldToTile(VisibleWorldRect.Right, VisibleWorldRect.Bottom);
        var minChunk = CoordinateUtils.TileToChunk(minTile);
        var maxChunk = CoordinateUtils.TileToChunk(maxTile);

        for (var cy = minChunk.Y; cy <= maxChunk.Y; cy++)
        {
            for (var cx = minChunk.X; cx <= maxChunk.X; cx++)
            {
                yield return new ChunkPos(cx, cy);
            }
        }
    }
}
