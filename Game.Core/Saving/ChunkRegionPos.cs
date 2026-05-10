using Game.Core.World;

namespace Game.Core.Saving;

public readonly record struct ChunkRegionPos(int X, int Y)
{
    public static ChunkRegionPos FromChunk(ChunkPos chunk, int regionSizeChunks)
    {
        if (regionSizeChunks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(regionSizeChunks), "Region size must be greater than zero.");
        }

        return new ChunkRegionPos(FloorDiv(chunk.X, regionSizeChunks), FloorDiv(chunk.Y, regionSizeChunks));
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
}
