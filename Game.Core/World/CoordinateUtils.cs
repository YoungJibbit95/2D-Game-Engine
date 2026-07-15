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
            FloorToInt(pixelX / GameConstants.TileSize),
            FloorToInt(pixelY / GameConstants.TileSize));
    }

    public static Vector2 TileToWorld(TilePos tilePosition)
    {
        return new Vector2(
            (float)tilePosition.X * GameConstants.TileSize,
            (float)tilePosition.Y * GameConstants.TileSize);
    }

    public static ChunkPos TileToChunk(TilePos tilePosition)
    {
        return TileToChunk(tilePosition.X, tilePosition.Y);
    }

    public static ChunkPos TileToChunk(int tileX, int tileY)
    {
        return new ChunkPos(FloorDiv(tileX), FloorDiv(tileY));
    }

    public static TilePos LocalTileInChunk(TilePos tilePosition)
    {
        return LocalTileInChunk(tilePosition.X, tilePosition.Y);
    }

    public static TilePos LocalTileInChunk(int tileX, int tileY)
    {
        return new TilePos(FloorMod(tileX), FloorMod(tileY));
    }

    public static TilePos ChunkToTileOrigin(ChunkPos chunkPosition)
    {
        return new TilePos(
            SaturateToInt((long)chunkPosition.X * GameConstants.ChunkSize),
            SaturateToInt((long)chunkPosition.Y * GameConstants.ChunkSize));
    }

    public static RectI ChunkTileBounds(ChunkPos chunkPosition)
    {
        return new RectI(
            SaturatingChunkOrigin(chunkPosition.X),
            SaturatingChunkOrigin(chunkPosition.Y),
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
        var max = TileToChunk(
            SaturateToInt((long)tileRegion.Right - 1),
            SaturateToInt((long)tileRegion.Bottom - 1));
        for (var y = (long)min.Y; y <= max.Y; y++)
        {
            for (var x = (long)min.X; x <= max.X; x++)
            {
                yield return new ChunkPos((int)x, (int)y);
            }
        }
    }

    private static int FloorDiv(int value)
    {
        var quotient = value / GameConstants.ChunkSize;
        return value % GameConstants.ChunkSize < 0 ? quotient - 1 : quotient;
    }

    private static int FloorMod(int value)
    {
        var remainder = value % GameConstants.ChunkSize;
        return remainder < 0 ? remainder + GameConstants.ChunkSize : remainder;
    }

    private static int FloorToInt(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        if (float.IsPositiveInfinity(value))
        {
            return int.MaxValue;
        }

        if (float.IsNegativeInfinity(value))
        {
            return int.MinValue;
        }

        return SaturateToInt((long)Math.Floor(value));
    }

    private static int SaturatingChunkOrigin(int chunkCoordinate)
    {
        var origin = (long)chunkCoordinate * GameConstants.ChunkSize;
        return (int)Math.Clamp(
            origin,
            int.MinValue,
            (long)int.MaxValue - GameConstants.ChunkSize);
    }

    private static int SaturateToInt(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}