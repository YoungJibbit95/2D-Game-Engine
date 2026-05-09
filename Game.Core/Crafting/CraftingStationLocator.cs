using Game.Core.Inventory;
using Game.Core.Tiles;
using Game.Core.World;

namespace Game.Core.Crafting;

public sealed class CraftingStationLocator
{
    public IReadOnlySet<string> FindStations(World.World world, TileRegistry tiles, TilePos actorTile, int radiusTiles)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);

        if (radiusTiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusTiles), "Crafting station search radius cannot be negative.");
        }

        var stations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var minX = Math.Max(0, actorTile.X - radiusTiles);
        var maxX = Math.Min(world.WidthTiles - 1, actorTile.X + radiusTiles);
        var minY = Math.Max(0, actorTile.Y - radiusTiles);
        var maxY = Math.Min(world.HeightTiles - 1, actorTile.Y + radiusTiles);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var tile = world.GetTile(x, y);
                if (tile.IsAir)
                {
                    continue;
                }

                var definition = tiles.GetByNumericId(tile.TileId);
                if (!string.IsNullOrWhiteSpace(definition.CraftingStationId))
                {
                    stations.Add(definition.CraftingStationId);
                }
            }
        }

        return stations;
    }

    public CraftingContext CreateContext(
        PlayerInventory inventory,
        World.World world,
        TileRegistry tiles,
        TilePos actorTile,
        int radiusTiles,
        IReadOnlySet<string>? knownRecipeIds = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return new CraftingContext(
            inventory,
            FindStations(world, tiles, actorTile, radiusTiles),
            knownRecipeIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}
