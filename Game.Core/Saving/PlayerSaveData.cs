using Game.Core.Characters;
using Game.Core.Inventory;

namespace Game.Core.Saving;

public sealed record PlayerSaveData
{
    public const int CurrentFormatVersion = 3;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required string PlayerId { get; init; }

    public required string DisplayName { get; init; }

    public required float PositionX { get; init; }

    public required float PositionY { get; init; }

    public required int Health { get; init; }

    public required int MaxHealth { get; init; }

    public int Mana { get; init; }

    public required int SelectedHotbarSlot { get; init; }

    public required IReadOnlyList<ItemStack> InventorySlots { get; init; }

    public IReadOnlyList<InventorySlotState> InventorySlotStates { get; init; } =
        Array.Empty<InventorySlotState>();

    public EquipmentLoadoutSaveData? EquipmentLoadout { get; init; }

    public IReadOnlyList<ActiveStatusEffectSaveData> ActiveStatusEffects { get; init; } =
        Array.Empty<ActiveStatusEffectSaveData>();

    public CharacterAppearance? CharacterAppearance { get; init; }
}
