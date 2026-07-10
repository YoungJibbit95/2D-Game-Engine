namespace Game.Core.World.Generation;

public sealed class WorldAnalyzer
{
    private const int CavernRegionMinimumTiles = 64;

    public WorldGenerationAnalysis Analyze(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var tileCounts = new Dictionary<ushort, int>();
        var air = 0;
        var solid = 0;
        var liquid = 0;
        var natural = 0;
        var wall = 0;
        var exposedWall = 0;
        var wallCounts = new Dictionary<ushort, int>();
        var surfaceSum = 0;
        var surfaceColumns = 0;
        var minSurface = int.MaxValue;
        var maxSurface = int.MinValue;
        var surfaceHeights = new int[world.WidthTiles];
        Array.Fill(surfaceHeights, -1);

        for (var x = 0; x < world.WidthTiles; x++)
        {
            int? columnSurface = null;
            for (var y = 0; y < world.HeightTiles; y++)
            {
                var tile = world.GetTile(x, y);
                tileCounts[tile.TileId] = tileCounts.GetValueOrDefault(tile.TileId) + 1;

                if (tile.IsAir)
                {
                    air++;
                }

                if (tile.IsSolid)
                {
                    solid++;
                    columnSurface ??= y;
                }

                if (tile.HasLiquid)
                {
                    liquid++;
                }

                if (tile.Flags.HasFlag(TileFlags.IsNatural))
                {
                    natural++;
                }

                if (tile.WallId != 0)
                {
                    wall++;
                    wallCounts[tile.WallId] = wallCounts.GetValueOrDefault(tile.WallId) + 1;
                    if (!tile.IsSolid)
                    {
                        exposedWall++;
                    }
                }
            }

            if (columnSurface is not { } surface)
            {
                continue;
            }

            surfaceColumns++;
            surfaceSum += surface;
            minSurface = Math.Min(minSurface, surface);
            maxSurface = Math.Max(maxSurface, surface);
            surfaceHeights[x] = surface;
        }

        var undergroundAir = 0;
        var surfaceLiquid = 0;
        var caveLiquid = 0;
        for (var x = 0; x < world.WidthTiles; x++)
        {
            for (var y = 0; y < world.HeightTiles; y++)
            {
                var tile = world.GetTile(x, y);
                if (IsUndergroundDryAir(tile, x, y, surfaceHeights))
                {
                    undergroundAir++;
                }

                if (!tile.HasLiquid)
                {
                    continue;
                }

                if (surfaceHeights[x] >= 0 && y <= surfaceHeights[x] + 4)
                {
                    surfaceLiquid++;
                }
                else
                {
                    caveLiquid++;
                }
            }
        }

        var undergroundAirRegions = FindRegionSizes(
            world,
            (x, y, tile) => IsUndergroundDryAir(tile, x, y, surfaceHeights));
        var cavernRegions = undergroundAirRegions.Where(size => size >= CavernRegionMinimumTiles).ToArray();
        var liquidBodies = FindRegionSizes(world, (_, _, tile) => tile.HasLiquid);

        return new WorldGenerationAnalysis(
            world.WidthTiles,
            world.HeightTiles,
            air,
            solid,
            liquid,
            natural,
            surfaceColumns == 0 ? 0 : minSurface,
            surfaceColumns == 0 ? 0 : maxSurface,
            surfaceColumns == 0 ? 0 : (float)surfaceSum / surfaceColumns,
            tileCounts)
        {
            UndergroundAirTileCount = undergroundAir,
            CavernTileCount = cavernRegions.Sum(),
            CavernRegionCount = cavernRegions.Length,
            LargestCavernTileCount = cavernRegions.Length == 0 ? 0 : cavernRegions.Max(),
            LiquidBodyCount = liquidBodies.Count,
            LargestLiquidBodyTileCount = liquidBodies.Count == 0 ? 0 : liquidBodies.Max(),
            SurfaceLiquidTileCount = surfaceLiquid,
            CaveLiquidTileCount = caveLiquid,
            WallTileCount = wall,
            ExposedWallTileCount = exposedWall,
            WallCounts = wallCounts
        };
    }

    private static bool IsUndergroundDryAir(TileInstance tile, int x, int y, IReadOnlyList<int> surfaceHeights)
    {
        return tile.IsAir && !tile.HasLiquid && surfaceHeights[x] >= 0 && y > surfaceHeights[x] + 2;
    }

    private static List<int> FindRegionSizes(World world, Func<int, int, TileInstance, bool> predicate)
    {
        var visited = new bool[world.WidthTiles * world.HeightTiles];
        var regionSizes = new List<int>();
        var pending = new Queue<TilePos>();
        ReadOnlySpan<TilePos> offsets =
        [
            new TilePos(-1, 0),
            new TilePos(1, 0),
            new TilePos(0, -1),
            new TilePos(0, 1)
        ];

        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                var index = y * world.WidthTiles + x;
                if (visited[index] || !predicate(x, y, world.GetTile(x, y)))
                {
                    visited[index] = true;
                    continue;
                }

                visited[index] = true;
                pending.Enqueue(new TilePos(x, y));
                var size = 0;
                while (pending.Count > 0)
                {
                    var current = pending.Dequeue();
                    size++;

                    foreach (var offset in offsets)
                    {
                        var nextX = current.X + offset.X;
                        var nextY = current.Y + offset.Y;
                        if (!world.IsInBounds(nextX, nextY))
                        {
                            continue;
                        }

                        var nextIndex = nextY * world.WidthTiles + nextX;
                        if (visited[nextIndex])
                        {
                            continue;
                        }

                        visited[nextIndex] = true;
                        if (predicate(nextX, nextY, world.GetTile(nextX, nextY)))
                        {
                            pending.Enqueue(new TilePos(nextX, nextY));
                        }
                    }
                }

                regionSizes.Add(size);
            }
        }

        return regionSizes;
    }
}
