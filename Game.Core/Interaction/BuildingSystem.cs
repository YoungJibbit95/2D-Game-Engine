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
    private readonly BuildingPlacementValidator _placementValidator;
    private readonly BuildingPlacementTransactionService _placementTransactions;

    public BuildingSystem(
        BuildingPlacementValidator? placementValidator = null,
        BuildingPlacementTransactionService? placementTransactions = null)
    {
        _placementValidator = placementValidator ?? new BuildingPlacementValidator();
        _placementTransactions = placementTransactions ??
            new BuildingPlacementTransactionService(_placementValidator);
    }

    public BuildingPlacementValidationResult ValidatePlacement(
        World.World world,
        ItemRegistry items,
        TileRegistry tiles,
        in BuildingPlacementRequest request)
    {
        return _placementValidator.Validate(world, items, tiles, request);
    }

    public BuildingPlacementPrepareResult PreparePlacement(
        World.World world,
        InventoryModel inventory,
        ItemRegistry items,
        TileRegistry tiles,
        in BuildingPlacementRequest request)
    {
        return _placementTransactions.Prepare(world, inventory, items, tiles, request);
    }

    public BuildingPlacementPrepareResult PreparePlacement(
        World.World world,
        PlayerInventory inventory,
        ItemRegistry items,
        TileRegistry tiles,
        in BuildingPlacementRequest request)
    {
        return _placementTransactions.Prepare(world, inventory, items, tiles, request);
    }

    public BuildingPlacementCommitResult CommitPlacement(
        BuildingPlacementPlan plan,
        GameEventBus? events = null)
    {
        return _placementTransactions.Commit(plan, events);
    }

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
        var request = new BuildingPlacementRequest(
            target,
            new ItemStack(itemId, 1),
            actorCenterWorld,
            actorBounds,
            reachPixels,
            BuildingPlacementOptions.Strict with
            {
                RequireLoadedChunk = world.IsHorizontallyInfinite
            });
        var prepared = _placementTransactions.Prepare(
            world,
            inventory,
            items,
            tiles,
            request);
        return CommitPreparedPlacement(prepared, target, itemId, events);
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
        var request = new BuildingPlacementRequest(
            target,
            new ItemStack(itemId, 1),
            actorCenterWorld,
            actorBounds,
            reachPixels,
            BuildingPlacementOptions.Strict with
            {
                RequireLoadedChunk = world.IsHorizontallyInfinite
            });
        var prepared = _placementTransactions.Prepare(
            world,
            inventory,
            items,
            tiles,
            request);
        return CommitPreparedPlacement(prepared, target, itemId, events);
    }

    private BuildingResult CommitPreparedPlacement(
        in BuildingPlacementPrepareResult prepared,
        TilePos target,
        string itemId,
        GameEventBus? events)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        if (!prepared.Success || prepared.Plan is null)
        {
            return BuildingResult.BlockedResult(
                target,
                itemId,
                MapPlacementFailure(prepared.Failure));
        }

        var committed = _placementTransactions.Commit(prepared.Plan, events);
        return committed.Success
            ? BuildingResult.Placed(target, itemId, committed.TileId)
            : BuildingResult.BlockedResult(target, itemId, MapPlacementFailure(committed.Failure));
    }

    private static GameplayActionFailureReason MapPlacementFailure(BuildingPlacementFailure failure)
    {
        return failure switch
        {
            BuildingPlacementFailure.OutOfReach => GameplayActionFailureReason.OutOfReach,
            BuildingPlacementFailure.ActorCollision or
                BuildingPlacementFailure.Occupied or
                BuildingPlacementFailure.LiquidOccupied or
                BuildingPlacementFailure.WorldChanged => GameplayActionFailureReason.Occupied,
            BuildingPlacementFailure.Unsupported => GameplayActionFailureReason.UnsupportedPlacement,
            BuildingPlacementFailure.InsufficientItem or
                BuildingPlacementFailure.InventoryChanged => GameplayActionFailureReason.InsufficientItem,
            BuildingPlacementFailure.UnknownItem or
                BuildingPlacementFailure.ItemNotPlaceable or
                BuildingPlacementFailure.UnknownTile => GameplayActionFailureReason.InvalidItem,
            _ => GameplayActionFailureReason.InvalidTarget
        };
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
