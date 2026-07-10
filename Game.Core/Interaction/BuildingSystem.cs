using Game.Core.Inventory;
using Game.Core.Events;
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
        return EvaluatePlacement(world, items, tiles, target, item, actorCenterWorld, reachPixels, actorBounds).Success;
    }

    public BuildingResult EvaluatePlacement(
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

        if (item.IsEmpty)
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.InsufficientItem);
        }

        if (!world.IsInBounds(target.X, target.Y))
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.InvalidTarget);
        }

        if (!world.GetTile(target.X, target.Y).IsAir)
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.Occupied);
        }

        if (!IsWithinReach(actorCenterWorld, target, reachPixels))
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.OutOfReach);
        }

        var targetBounds = new RectI(
            target.X * GameConstants.TileSize,
            target.Y * GameConstants.TileSize,
            GameConstants.TileSize,
            GameConstants.TileSize);

        if (targetBounds.Intersects(actorBounds))
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.Occupied);
        }

        if (!items.TryGetById(item.ItemId, out var itemDefinition) || itemDefinition.Type != ItemType.PlaceableTile)
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.InvalidItem);
        }

        if (string.IsNullOrWhiteSpace(itemDefinition.PlacesTileId) ||
            !tiles.TryGetById(itemDefinition.PlacesTileId, out var tileDefinition))
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.InvalidItem);
        }

        if (!SatisfiesPlacementSupport(world, target, itemDefinition.PlacementSupport))
        {
            return BuildingResult.BlockedResult(target, item.ItemId, GameplayActionFailureReason.UnsupportedPlacement);
        }

        return new BuildingResult(true, target, item.ItemId, tileDefinition.NumericId, GameplayActionFailureReason.None);
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
        RectI actorBounds,
        GameEventBus? events = null)
    {
        return PlaceTileWithResult(
            world,
            inventory,
            items,
            tiles,
            target,
            itemId,
            actorCenterWorld,
            reachPixels,
            actorBounds,
            events).Success;
    }

    public BuildingResult PlaceTileWithResult(
        World.World world,
        InventoryModel inventory,
        ItemRegistry items,
        TileRegistry tiles,
        TilePos target,
        string itemId,
        Vector2 actorCenterWorld,
        float reachPixels,
        RectI actorBounds,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return PlaceTileInternal(
            world,
            items,
            tiles,
            target,
            itemId,
            actorCenterWorld,
            reachPixels,
            actorBounds,
            inventory.RemoveItem,
            events);
    }

    public bool PlaceTile(
        World.World world,
        PlayerInventory inventory,
        ItemRegistry items,
        TileRegistry tiles,
        TilePos target,
        string itemId,
        Vector2 actorCenterWorld,
        float reachPixels,
        RectI actorBounds,
        GameEventBus? events = null)
    {
        return PlaceTileWithResult(
            world,
            inventory,
            items,
            tiles,
            target,
            itemId,
            actorCenterWorld,
            reachPixels,
            actorBounds,
            events).Success;
    }

    public BuildingResult PlaceTileWithResult(
        World.World world,
        PlayerInventory inventory,
        ItemRegistry items,
        TileRegistry tiles,
        TilePos target,
        string itemId,
        Vector2 actorCenterWorld,
        float reachPixels,
        RectI actorBounds,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return PlaceTileInternal(
            world,
            items,
            tiles,
            target,
            itemId,
            actorCenterWorld,
            reachPixels,
            actorBounds,
            inventory.RemoveItem,
            events);
    }

    private BuildingResult PlaceTileInternal(
        World.World world,
        ItemRegistry items,
        TileRegistry tiles,
        TilePos target,
        string itemId,
        Vector2 actorCenterWorld,
        float reachPixels,
        RectI actorBounds,
        Func<string, int, bool> removeItem,
        GameEventBus? events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var evaluation = EvaluatePlacement(
            world,
            items,
            tiles,
            target,
            new ItemStack(itemId, 1),
            actorCenterWorld,
            reachPixels,
            actorBounds);
        if (!evaluation.Success)
        {
            return evaluation;
        }

        if (!removeItem(itemId, 1))
        {
            return BuildingResult.BlockedResult(target, itemId, GameplayActionFailureReason.InsufficientItem);
        }

        var itemDefinition = items.GetById(itemId);
        var tileDefinition = tiles.GetById(itemDefinition.PlacesTileId!);
        world.SetTile(target.X, target.Y, TileInstance.FromTileId(tileDefinition.NumericId, isSolid: tileDefinition.Solid));
        events?.Publish(new TilePlacedEvent(target, tileDefinition.NumericId, itemId));
        return BuildingResult.Placed(target, itemId, tileDefinition.NumericId);
    }

    private static bool IsWithinReach(Vector2 actorCenterWorld, TilePos target, float reachPixels)
    {
        var tileCenter = CoordinateUtils.TileToWorld(target) + new Vector2(GameConstants.TileSize * 0.5f);
        return Vector2.Distance(actorCenterWorld, tileCenter) <= reachPixels;
    }

    private static bool SatisfiesPlacementSupport(World.World world, TilePos target, PlacementSupportRule support)
    {
        return support switch
        {
            PlacementSupportRule.None => true,
            PlacementSupportRule.AdjacentSolid => HasAdjacentSolid(world, target),
            PlacementSupportRule.AdjacentSolidOrWall => HasAdjacentSolid(world, target) || HasAdjacentWall(world, target),
            PlacementSupportRule.OnSolidGround => world.IsSolid(target.X, target.Y + 1),
            _ => false
        };
    }

    private static bool HasAdjacentSolid(World.World world, TilePos target)
    {
        return world.IsSolid(target.X - 1, target.Y) ||
               world.IsSolid(target.X + 1, target.Y) ||
               world.IsSolid(target.X, target.Y - 1) ||
               world.IsSolid(target.X, target.Y + 1);
    }

    private static bool HasAdjacentWall(World.World world, TilePos target)
    {
        return HasWall(world, target.X - 1, target.Y) ||
               HasWall(world, target.X + 1, target.Y) ||
               HasWall(world, target.X, target.Y - 1) ||
               HasWall(world, target.X, target.Y + 1);
    }

    private static bool HasWall(World.World world, int x, int y)
    {
        return world.IsInBounds(x, y) && world.GetTile(x, y).WallId != 0;
    }
}
