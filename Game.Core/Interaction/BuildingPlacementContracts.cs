using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.World;
using System.Numerics;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Interaction;

public enum BuildingPlacementFailure
{
    None,
    InvalidRequest,
    OutOfBounds,
    ChunkNotLoaded,
    OutOfReach,
    Obstructed,
    ActorCollision,
    Occupied,
    LiquidOccupied,
    UnknownItem,
    ItemNotPlaceable,
    UnknownTile,
    Unsupported,
    InsufficientItem,
    InventoryChanged,
    WorldChanged,
    CommitFailed
}

public sealed record BuildingPlacementOptions
{
    public static BuildingPlacementOptions Strict { get; } = new();

    public bool RequireLoadedChunk { get; init; } = true;

    public bool RequireLineOfSight { get; init; } = true;

    public bool AllowReplaceLiquid { get; init; }
}

public readonly record struct BuildingPlacementRequest(
    TilePos Target,
    ItemStack Stack,
    Vector2 ActorCenterWorld,
    RectI ActorBoundsWorld,
    float ReachPixels,
    BuildingPlacementOptions? Options = null);

public readonly record struct BuildingPlacementValidationResult(
    bool Success,
    BuildingPlacementFailure Failure,
    TilePos Target,
    string ItemId,
    ushort TileId,
    TileInstance ExpectedTile)
{
    public static BuildingPlacementValidationResult Rejected(
        in BuildingPlacementRequest request,
        BuildingPlacementFailure failure,
        TileInstance expectedTile = default)
    {
        return new BuildingPlacementValidationResult(
            false,
            failure,
            request.Target,
            request.Stack.ItemId,
            0,
            expectedTile);
    }
}

public sealed class BuildingPlacementPlan
{
    internal BuildingPlacementPlan(
        Game.Core.World.World world,
        InventoryModel inventory,
        Game.Core.Items.ItemRegistry items,
        Game.Core.Tiles.TileRegistry tiles,
        in BuildingPlacementRequest request,
        in BuildingPlacementValidationResult validation)
    {
        World = world;
        Inventory = inventory;
        Items = items;
        Tiles = tiles;
        Request = request;
        Target = validation.Target;
        ItemId = validation.ItemId;
        TileId = validation.TileId;
        ExpectedTile = validation.ExpectedTile;
        InventoryVersion = inventory.Version;
    }

    internal BuildingPlacementPlan(
        Game.Core.World.World world,
        PlayerInventory inventory,
        Game.Core.Items.ItemRegistry items,
        Game.Core.Tiles.TileRegistry tiles,
        in BuildingPlacementRequest request,
        in BuildingPlacementValidationResult validation)
    {
        World = world;
        PlayerInventory = inventory;
        Items = items;
        Tiles = tiles;
        Request = request;
        Target = validation.Target;
        ItemId = validation.ItemId;
        TileId = validation.TileId;
        ExpectedTile = validation.ExpectedTile;
        InventoryVersion = inventory.Hotbar.Version;
        InventorySecondaryVersion = inventory.Main.Version;
    }

    internal Game.Core.World.World World { get; }

    internal InventoryModel? Inventory { get; }

    internal PlayerInventory? PlayerInventory { get; }

    internal Game.Core.Items.ItemRegistry Items { get; }

    internal Game.Core.Tiles.TileRegistry Tiles { get; }

    internal BuildingPlacementRequest Request { get; }

    public TilePos Target { get; }

    public string ItemId { get; }

    public ushort TileId { get; }

    public TileInstance ExpectedTile { get; }

    public long InventoryVersion { get; }

    public long InventorySecondaryVersion { get; }

    internal bool IsInventoryUnchanged()
    {
        return Inventory is not null
            ? Inventory.Version == InventoryVersion
            : PlayerInventory is not null &&
              PlayerInventory.Hotbar.Version == InventoryVersion &&
              PlayerInventory.Main.Version == InventorySecondaryVersion;
    }

    internal int CountItem()
    {
        return Inventory?.CountItem(ItemId) ?? PlayerInventory!.CountItem(ItemId);
    }

    internal InventoryTransactionResult RemoveItem()
    {
        return Inventory?.RemoveTransaction(ItemId, 1) ??
               PlayerInventory!.RemoveTransaction(ItemId, 1);
    }

    internal InventoryTransactionResult RestoreItem()
    {
        var stack = new ItemStack(ItemId, 1);
        return Inventory?.AddTransaction(stack) ?? PlayerInventory!.AddTransaction(stack);
    }
}

public readonly record struct BuildingPlacementPrepareResult(
    bool Success,
    BuildingPlacementFailure Failure,
    BuildingPlacementPlan? Plan);

public readonly record struct BuildingPlacementCommitResult(
    bool Success,
    BuildingPlacementFailure Failure,
    TilePos Target,
    string ItemId,
    ushort TileId);
