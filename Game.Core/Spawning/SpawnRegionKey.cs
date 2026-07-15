using Game.Core.World;

namespace Game.Core.Spawning;

public readonly record struct SpawnRegionKey(int X, int Y)
{
    public static SpawnRegionKey FromTile(TilePos tile, int regionSizeTiles)
    {
        if (regionSizeTiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(regionSizeTiles));
        }

        return new SpawnRegionKey(
            FloorDivide(tile.X, regionSizeTiles),
            FloorDivide(tile.Y, regionSizeTiles));
    }

    private static int FloorDivide(int value, int divisor)
    {
        var quotient = value / divisor;
        return value % divisor < 0 ? quotient - 1 : quotient;
    }
}
