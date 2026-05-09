using Game.Core;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class TilemapRenderer
{
    public bool ShowGrid { get; set; }

    public bool DrawLiquids { get; set; } = true;

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

    private void DrawChunk(RenderContext context, World world, Chunk chunk, Camera2D camera)
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
                var tile = world.GetTile(x, y);
                if (tile.IsAir && !tile.HasLiquid)
                {
                    continue;
                }

                if (!tile.IsAir)
                {
                    DrawTile(context, camera, x, y, tile);
                }

                if (DrawLiquids && tile.HasLiquid)
                {
                    DrawLiquid(context, camera, x, y, tile);
                }
            }
        }
    }

    private void DrawTile(RenderContext context, Camera2D camera, int tileX, int tileY, TileInstance tile)
    {
        var worldPosition = new Vector2(tileX * GameConstants.TileSize, tileY * GameConstants.TileSize);
        var screenPosition = camera.WorldToScreen(worldPosition, context.ViewportBounds);
        var tileSize = MathF.Ceiling(GameConstants.TileSize * camera.Zoom);

        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            (int)tileSize,
            (int)tileSize);

        context.SpriteBatch.Draw(context.Pixel, destination, GetTileColor(tile.TileId));

        if (ShowGrid)
        {
            DrawGrid(context, destination);
        }
    }

    private static void DrawLiquid(RenderContext context, Camera2D camera, int tileX, int tileY, TileInstance tile)
    {
        var worldPosition = new Vector2(tileX * GameConstants.TileSize, tileY * GameConstants.TileSize);
        var screenPosition = camera.WorldToScreen(worldPosition, context.ViewportBounds);
        var tileSize = MathF.Ceiling(GameConstants.TileSize * camera.Zoom);
        var fillRatio = MathHelper.Clamp(tile.LiquidAmount / 255f, 0.08f, 1f);
        var liquidHeight = Math.Max(1, (int)MathF.Ceiling(tileSize * fillRatio));

        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y + tileSize - liquidHeight),
            (int)tileSize,
            liquidHeight);

        context.SpriteBatch.Draw(context.Pixel, destination, new Color(52, 121, 207, 168));
    }

    private static void DrawGrid(RenderContext context, Rectangle destination)
    {
        var color = new Color(0, 0, 0, 35);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(destination.X, destination.Y, destination.Width, 1), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(destination.X, destination.Y, 1, destination.Height), color);
    }

    private static Color GetTileColor(ushort tileId)
    {
        return tileId switch
        {
            KnownTileIds.Dirt => new Color(112, 76, 48),
            KnownTileIds.Grass => new Color(70, 142, 62),
            KnownTileIds.Stone => new Color(92, 94, 104),
            KnownTileIds.CopperOre => new Color(173, 99, 62),
            KnownTileIds.IronOre => new Color(158, 136, 112),
            KnownTileIds.Wood => new Color(118, 80, 43),
            KnownTileIds.Leaves => new Color(52, 132, 62),
            KnownTileIds.Workbench => new Color(141, 92, 48),
            _ => Color.Magenta
        };
    }
}
