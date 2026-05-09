namespace Game.Core.World.Generation;

public sealed class WorldAnalyzer
{
    public WorldGenerationAnalysis Analyze(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var tileCounts = new Dictionary<ushort, int>();
        var air = 0;
        var solid = 0;
        var liquid = 0;
        var natural = 0;
        var surfaceSum = 0;
        var surfaceColumns = 0;
        var minSurface = int.MaxValue;
        var maxSurface = int.MinValue;

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
            }

            if (columnSurface is not { } surface)
            {
                continue;
            }

            surfaceColumns++;
            surfaceSum += surface;
            minSurface = Math.Min(minSurface, surface);
            maxSurface = Math.Max(maxSurface, surface);
        }

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
            tileCounts);
    }
}
