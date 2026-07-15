using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Tiles;
using GameWorld = Game.Core.World.World;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Interaction;

public sealed class BuildingPlacementTransactionService
{
    private readonly BuildingPlacementValidator _validator;

    public BuildingPlacementTransactionService(BuildingPlacementValidator? validator = null)
    {
        _validator = validator ?? new BuildingPlacementValidator();
    }

    public BuildingPlacementPrepareResult Prepare(
        GameWorld world,
        InventoryModel inventory,
        ItemRegistry items,
        TileRegistry tiles,
        in BuildingPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var validation = _validator.Validate(world, items, tiles, request);
        if (!validation.Success)
        {
            return new BuildingPlacementPrepareResult(false, validation.Failure, null);
        }

        if (inventory.CountItem(request.Stack.ItemId) < 1)
        {
            return new BuildingPlacementPrepareResult(false, BuildingPlacementFailure.InsufficientItem, null);
        }

        return new BuildingPlacementPrepareResult(
            true,
            BuildingPlacementFailure.None,
            new BuildingPlacementPlan(world, inventory, items, tiles, request, validation));
    }

    public BuildingPlacementPrepareResult Prepare(
        GameWorld world,
        PlayerInventory inventory,
        ItemRegistry items,
        TileRegistry tiles,
        in BuildingPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var validation = _validator.Validate(world, items, tiles, request);
        if (!validation.Success)
        {
            return new BuildingPlacementPrepareResult(false, validation.Failure, null);
        }

        if (inventory.CountItem(request.Stack.ItemId) < 1)
        {
            return new BuildingPlacementPrepareResult(false, BuildingPlacementFailure.InsufficientItem, null);
        }

        return new BuildingPlacementPrepareResult(
            true,
            BuildingPlacementFailure.None,
            new BuildingPlacementPlan(world, inventory, items, tiles, request, validation));
    }

    public BuildingPlacementCommitResult Commit(
        BuildingPlacementPlan plan,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsInventoryUnchanged())
        {
            return Rejected(plan, BuildingPlacementFailure.InventoryChanged);
        }

        var validation = _validator.Validate(plan.World, plan.Items, plan.Tiles, plan.Request);
        if (!validation.Success || !validation.ExpectedTile.Equals(plan.ExpectedTile) ||
            validation.TileId != plan.TileId)
        {
            return Rejected(
                plan,
                validation.Success ? BuildingPlacementFailure.WorldChanged : validation.Failure);
        }

        var removal = plan.RemoveItem();
        if (!removal.Completed)
        {
            return Rejected(plan, BuildingPlacementFailure.InsufficientItem);
        }

        var definition = plan.Tiles.GetByNumericId(plan.TileId);
        var placed = Game.Core.World.TileInstance.FromTileId(plan.TileId, isSolid: definition.Solid);
        if (!plan.World.TrySetTile(plan.Target.X, plan.Target.Y, placed))
        {
            var rollback = plan.RestoreItem();
            if (!rollback.Completed)
            {
                throw new InvalidOperationException(
                    "Building placement failed and its inventory rollback could not be completed.");
            }

            return Rejected(plan, BuildingPlacementFailure.CommitFailed);
        }

        events?.Publish(new TilePlacedEvent(plan.Target, plan.TileId, plan.ItemId));
        return new BuildingPlacementCommitResult(
            true,
            BuildingPlacementFailure.None,
            plan.Target,
            plan.ItemId,
            plan.TileId);
    }

    private static BuildingPlacementCommitResult Rejected(
        BuildingPlacementPlan plan,
        BuildingPlacementFailure failure)
    {
        return new BuildingPlacementCommitResult(
            false,
            failure,
            plan.Target,
            plan.ItemId,
            plan.TileId);
    }
}
