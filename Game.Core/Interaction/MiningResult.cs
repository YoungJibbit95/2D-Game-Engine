using Game.Core.Inventory;
using Game.Core.World;

namespace Game.Core.Interaction;

public readonly record struct MiningResult(bool Completed, TilePos TilePosition, ItemStack DroppedItem)
{
    public static MiningResult None { get; } = new(false, TilePos.Zero, ItemStack.Empty);
}
