using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class TilemapRenderer
{
    private readonly ChunkRenderCache _cache = new();

    public bool ShowGrid { get; set; }

    public bool DrawLiquids { get; set; } = true;

    public ClientTextureRegistry? Textures { get; set; }

    public Func<ushort, string?>? TileSpriteResolver { get; set; }

    public TileRegistry? Tiles { get; set; }

    public TilemapRenderMetrics LastMetrics { get; private set; }

    public void Draw(RenderContext context, World world, Camera2D camera)
    {
        var visibleChunks = 0;
        var rebuiltChunks = 0;
        var tileCommands = 0;
        var liquidCommands = 0;
        var tiles = Tiles ?? TileRegistry.Create(Array.Empty<TileDefinition>());

        foreach (var chunkPosition in camera.GetVisibleChunks())
        {
            if (!world.TryGetChunk(chunkPosition, out var chunk) || chunk is null)
            {
                continue;
            }

            visibleChunks++;
            var result = _cache.GetOrBuild(world, tiles, chunk);
            if (result.Rebuilt)
            {
                rebuiltChunks++;
            }

            DrawChunk(context, world, chunk, result.Commands, camera, ref tileCommands, ref liquidCommands);
        }

        var evicted = _cache.TrimToLoadedChunks(world.Chunks.Keys);
        LastMetrics = new TilemapRenderMetrics(
            visibleChunks,
            _cache.CachedChunkCount,
            rebuiltChunks,
            evicted,
            tileCommands,
            liquidCommands);
    }

    public void ClearCache()
    {
        _cache.Clear();
        LastMetrics = default;
    }

    private void DrawChunk(
        RenderContext context,
        World world,
        Chunk chunk,
        IReadOnlyList<ChunkRenderCommand> commands,
        Camera2D camera,
        ref int tileCommands,
        ref int liquidCommands)
    {
        var chunkBounds = CoordinateUtils.ChunkTileBounds(chunk.Position);
        foreach (var command in commands)
        {
            var tileX = chunkBounds.Left + command.LocalX;
            var tileY = chunkBounds.Top + command.LocalY;
            if (!world.IsInBounds(tileX, tileY))
            {
                continue;
            }

            if (!command.Tile.IsAir)
            {
                DrawTile(context, camera, tileX, tileY, command.Tile, command.AutoTileMask);
                tileCommands++;
            }

            if (DrawLiquids && command.Tile.HasLiquid)
            {
                DrawLiquid(context, camera, tileX, tileY, command.Tile);
                liquidCommands++;
            }
        }
    }

    private void DrawTile(
        RenderContext context,
        Camera2D camera,
        int tileX,
        int tileY,
        TileInstance tile,
        AutoTileMask autoTileMask)
    {
        var worldPosition = new Vector2(tileX * GameConstants.TileSize, tileY * GameConstants.TileSize);
        var screenPosition = camera.WorldToScreen(worldPosition, context.ViewportBounds);
        var tileSize = MathF.Ceiling(GameConstants.TileSize * camera.Zoom);

        var destination = new Rectangle(
            (int)MathF.Floor(screenPosition.X),
            (int)MathF.Floor(screenPosition.Y),
            (int)tileSize,
            (int)tileSize);

        if (TryDrawSpriteTexture(context, destination, tile.TileId, autoTileMask))
        {
            if (ShowGrid)
            {
                DrawGrid(context, destination);
            }

            return;
        }

        context.SpriteBatch.Draw(context.Pixel, destination, GetTileColor(tile.TileId));

        if (ShowGrid)
        {
            DrawGrid(context, destination);
        }
    }

    private bool TryDrawSpriteTexture(RenderContext context, Rectangle destination, ushort tileId, AutoTileMask autoTileMask)
    {
        if (Textures is null || TileSpriteResolver is null)
        {
            return false;
        }

        var spriteId = TileSpriteResolver(tileId);
        if (string.IsNullOrWhiteSpace(spriteId) ||
            !Textures.TryGetRealTextureForAutoTileMask(spriteId, (int)autoTileMask, out var sprite))
        {
            return false;
        }

        context.SpriteBatch.Draw(sprite.Texture, destination, sprite.SourceRectangle, Color.White);
        return true;
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
