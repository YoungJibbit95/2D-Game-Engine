using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Interaction;

public sealed class BuildingSystem
{
    public bool CanPlace(
        World.World world,
        ItemRegistry items,
        TileRegistry tiles,
        TilePos target,
        ItemStack item,
        Vector2 actorCenterWorld,
        float reachPixels,
        RectI actorBounds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(tiles);

        if (item.IsEmpty || !world.IsInBounds(target.X, target.Y) || !world.GetTile(target.X, target.Y).IsAir)
        {
            return false;
        }

        if (!IsWithinReach(actorCenterWorld, target, reachPixels))
        {
            return false;
        }

        var targetBounds = new RectI(
            target.X * GameConstants.TileSize,
            target.Y * GameConstants.TileSize,
            GameConstants.TileSize,
            GameConstants.TileSize);

        if (targetBounds.Intersects(actorBounds))
        {
            return false;
        }

        var itemDefinition = items.GetById(item.ItemId);
        return itemDefinition.Type == ItemType.PlaceableTile &&
               !string.IsNullOrWhiteSpace(itemDefinition.PlacesTileId) &&
               tiles.TryGetById(itemDefinition.PlacesTileId, out _);
    }

    public bool PlaceTile(
        World.World world,
        InventoryModel inventory,
        ItemRegistry items,
        TileRegistry tiles,
        TilePos target,
        string itemId,
        Vector2 actorCenterWorld,
        float reachPixels,
        RectI actorBounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var itemStack = new ItemStack(itemId, 1);
        if (!CanPlace(world, items, tiles, target, itemStack, actorCenterWorld, reachPixels, actorBounds))
        {
            return false;
        }

        if (!inventory.RemoveItem(itemId, 1))
        {
            return false;
        }

        var itemDefinition = items.GetById(itemId);
        var tileDefinition = tiles.GetById(itemDefinition.PlacesTileId!);
        world.SetTile(target.X, target.Y, TileInstance.FromTileId(tileDefinition.NumericId, TileFlags.IsNatural));
        return true;
    }

    private static bool IsWithinReach(Vector2 actorCenterWorld, TilePos target, float reachPixels)
    {
        var tileCenter = CoordinateUtils.TileToWorld(target) + new Vector2(GameConstants.TileSize * 0.5f);
        return Vector2.Distance(actorCenterWorld, tileCenter) <= reachPixels;
    }
}
