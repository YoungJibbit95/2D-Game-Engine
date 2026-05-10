using System.Numerics;

namespace Game.Core.World;

public static class CoordinateUtils
{
    public static TilePos WorldToTile(Vector2 pixelPosition)
    {
        return WorldToTile(pixelPosition.X, pixelPosition.Y);
    }

    public static TilePos WorldToTile(float pixelX, float pixelY)
    {
        return new TilePos(
            (int)MathF.Floor(pixelX / GameConstants.TileSize),
            (int)MathF.Floor(pixelY / GameConstants.TileSize));
    }

    public static Vector2 TileToWorld(TilePos tilePosition)
    {
        return new Vector2(
            tilePosition.X * GameConstants.TileSize,
            tilePosition.Y * GameConstants.TileSize);
    }

    public static ChunkPos TileToChunk(TilePos tilePosition)
    {
        return TileToChunk(tilePosition.X, tilePosition.Y);
    }

    public static ChunkPos TileToChunk(int tileX, int tileY)
    {
        return new ChunkPos(
            FloorDiv(tileX, GameConstants.ChunkSize),
            FloorDiv(tileY, GameConstants.ChunkSize));
    }

    public static TilePos LocalTileInChunk(TilePos tilePosition)
    {
        return LocalTileInChunk(tilePosition.X, tilePosition.Y);
    }

    public static TilePos LocalTileInChunk(int tileX, int tileY)
    {
        return new TilePos(
            FloorMod(tileX, GameConstants.ChunkSize),
            FloorMod(tileY, GameConstants.ChunkSize));
    }

    public static RectI ChunkTileBounds(ChunkPos chunkPosition)
    {
        return new RectI(
            chunkPosition.X * GameConstants.ChunkSize,
            chunkPosition.Y * GameConstants.ChunkSize,
            GameConstants.ChunkSize,
            GameConstants.ChunkSize);
    }

    public static IEnumerable<ChunkPos> EnumerateChunksOverlapping(RectI tileRegion)
    {
        if (tileRegion.IsEmpty)
        {
            yield break;
        }

        var min = TileToChunk(tileRegion.Left, tileRegion.Top);
        var max = TileToChunk(tileRegion.Right - 1, tileRegion.Bottom - 1);

        for (var cy = min.Y; cy <= max.Y; cy++)
        {
            for (var cx = min.X; cx <= max.X; cx++)
            {
                yield return new ChunkPos(cx, cy);
            }
        }
    }

    private static int FloorDiv(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;

        if (remainder != 0 && ((remainder < 0) != (divisor < 0)))
        {
            quotient--;
        }

        return quotient;
    }

    private static int FloorMod(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + Math.Abs(divisor) : remainder;
    }
}
