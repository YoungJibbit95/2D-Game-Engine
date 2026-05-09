using Game.Core.Inventory;

namespace Game.Core.Saving;

public sealed record TileEntitySaveData
{
    public int RuntimeId { get; init; }

    public required string TypeId { get; init; }

    public int TileX { get; init; }

    public int TileY { get; init; }

    public IReadOnlyList<ItemStack> InventorySlots { get; init; } = Array.Empty<ItemStack>();
}
