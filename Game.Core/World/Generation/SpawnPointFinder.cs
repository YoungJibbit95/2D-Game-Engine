namespace Game.Core.World.Generation;

public sealed class SpawnPointFinder
{
    public TilePos FindSurfaceSpawn(World world, int? preferredTileX = null, int clearanceTiles = 3)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (clearanceTiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clearanceTiles), "Clearance must be at least one tile.");
        }

        var centerX = Math.Clamp(preferredTileX ?? world.WidthTiles / 2, 0, world.WidthTiles - 1);
        for (var offset = 0; offset < world.WidthTiles; offset++)
        {
            var right = centerX + offset;
            if (right < world.WidthTiles && TryFindSurfaceAt(world, right, clearanceTiles, out var rightSpawn))
            {
                return rightSpawn;
            }

            var left = centerX - offset;
            if (offset > 0 && left >= 0 && TryFindSurfaceAt(world, left, clearanceTiles, out var leftSpawn))
            {
                return leftSpawn;
            }
        }

        return new TilePos(centerX, 0);
    }

    private static bool TryFindSurfaceAt(World world, int x, int clearanceTiles, out TilePos spawn)
    {
        for (var y = 1; y < world.HeightTiles; y++)
        {
            if (!IsValidSurface(world, x, y))
            {
                continue;
            }

            var spawnY = y - clearanceTiles;
            if (spawnY < 0)
            {
                break;
            }

            if (HasClearance(world, x, spawnY, clearanceTiles))
            {
                spawn = new TilePos(x, spawnY);
                return true;
            }
        }

        spawn = default;
        return false;
    }

    private static bool IsValidSurface(World world, int x, int y)
    {
        var tile = world.GetTile(x, y);
        if (!tile.IsSolid)
        {
            return false;
        }

        if (tile.TileId is not (KnownTileIds.Grass or KnownTileIds.Dirt or KnownTileIds.Stone))
        {
            return false;
        }

        return y == world.HeightTiles - 1 || world.IsSolid(x, y + 1);
    }

    private static bool HasClearance(World world, int x, int startY, int clearanceTiles)
    {
        for (var y = startY; y < startY + clearanceTiles; y++)
        {
            if (world.IsSolid(x, y))
            {
                return false;
            }
        }

        return true;
    }
}
