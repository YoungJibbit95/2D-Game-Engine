using Game.Core;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class LightingRenderer
{
    public void Draw(RenderContext context, World world, Camera2D camera)
    {
        foreach (var chunkPosition in camera.GetVisibleChunks())
        {
            if (!world.TryGetChunk(chunkPosition, out var chunk) || chunk is null)
            {
                continue;
            }

            DrawChunk(context, world, chunk, camera);
        }
    }

    private static void DrawChunk(RenderContext context, World world, Chunk chunk, Camera2D camera)
    {
        var chunkBounds = CoordinateUtils.ChunkTileBounds(chunk.Position);
        var minX = world.IsHorizontallyInfinite ? chunkBounds.Left : Math.Max(chunkBounds.Left, 0);
        var maxX = world.IsHorizontallyInfinite ? chunkBounds.Right : Math.Min(chunkBounds.Right, world.WidthTiles);
        var minY = Math.Max(chunkBounds.Top, 0);
        var maxY = Math.Min(chunkBounds.Bottom, world.HeightTiles);

        for (var y = minY; y < maxY; y++)
        {
            for (var x = minX; x < maxX; x++)
            {
                var light = world.GetTile(x, y).Light;
                var darkness = 255 - light;
                if (darkness <= 12)
                {
                    continue;
                }

                DrawTileDarkness(context, camera, x, y, darkness);
            }
        }
    }

    private static void DrawTileDarkness(RenderContext context, Camera2D camera, int tileX, int tileY, int darkness)
    {
        var worldPosition = new Vector2(tileX * GameConstants.TileSize, tileY * GameConstants.TileSize);
        var screenPosition = camera.WorldToScreen(worldPosition, context.ViewportBounds);
        var tileSize = MathF.Ceiling(GameConstants.TileSize * camera.Zoom);

        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            (int)tileSize,
            (int)tileSize);

        context.SpriteBatch.Draw(context.Pixel, destination, new Color(0, 0, 0, Math.Clamp(darkness, 0, 230)));
    }
}
