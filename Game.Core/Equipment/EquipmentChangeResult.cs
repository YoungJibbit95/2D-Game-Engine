using Game.Core.Inventory;

namespace Game.Core.Equipment;

public readonly record struct EquipmentChangeResult(bool Success, ItemStack ReplacedStack, string? Error)
{
    public static EquipmentChangeResult Equipped(ItemStack replacedStack)
    {
        return new EquipmentChangeResult(true, replacedStack, null);
    }

    public static EquipmentChangeResult Failed(string error)
    {
        return new EquipmentChangeResult(false, ItemStack.Empty, error);
    }
}
