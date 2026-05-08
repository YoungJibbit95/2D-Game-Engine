using Game.Core.Inventory;

namespace Game.Core.Saving;

public sealed record PlayerSaveData
{
    public int FormatVersion { get; init; } = 1;

    public required string PlayerId { get; init; }

    public required string DisplayName { get; init; }

    public required float PositionX { get; init; }

    public required float PositionY { get; init; }

    public required int Health { get; init; }

    public required int MaxHealth { get; init; }

    public int Mana { get; init; }

    public required int SelectedHotbarSlot { get; init; }

    public required IReadOnlyList<ItemStack> InventorySlots { get; init; }
}
