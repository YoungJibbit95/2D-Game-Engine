using Game.Core.World;

namespace Game.Core.Tiles;

public sealed class AutoTileSystem
{
    public AutoTileMask ComputeAutoTileMask(World.World world, TileRegistry tiles, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);

        var tile = world.GetTile(x, y);
        if (tile.IsAir || !tiles.TryGetByNumericId(tile.TileId, out var definition))
        {
            return AutoTileMask.None;
        }

        var mask = AutoTileMask.None;

        if (ConnectsTo(world, tiles, definition, x, y - 1))
        {
            mask |= AutoTileMask.Top;
        }

        if (ConnectsTo(world, tiles, definition, x + 1, y))
        {
            mask |= AutoTileMask.Right;
        }

        if (ConnectsTo(world, tiles, definition, x, y + 1))
        {
            mask |= AutoTileMask.Bottom;
        }

        if (ConnectsTo(world, tiles, definition, x - 1, y))
        {
            mask |= AutoTileMask.Left;
        }

        return mask;
    }

    public RectI GetSourceRectForMask(AutoTileMask mask, int tileSize)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), "Tile size must be greater than zero.");
        }

        var index = (int)mask;
        return new RectI(index * tileSize, 0, tileSize, tileSize);
    }

    private static bool ConnectsTo(World.World world, TileRegistry tiles, TileDefinition definition, int x, int y)
    {
        if (!world.IsInBounds(x, y))
        {
            return false;
        }

        var neighbor = world.GetTile(x, y);
        if (neighbor.IsAir || !tiles.TryGetByNumericId(neighbor.TileId, out var neighborDefinition))
        {
            return false;
        }

        if (neighborDefinition.NumericId == definition.NumericId)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(definition.MergeGroup) &&
               string.Equals(definition.MergeGroup, neighborDefinition.MergeGroup, StringComparison.OrdinalIgnoreCase);
    }
}
