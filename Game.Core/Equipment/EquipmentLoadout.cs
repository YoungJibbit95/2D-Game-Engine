using Game.Core.Inventory;
using Game.Core.Items;

namespace Game.Core.Equipment;

public sealed class EquipmentLoadout
{
    private static readonly EquipmentSlotType[] AccessorySlots =
    [
        EquipmentSlotType.Accessory1,
        EquipmentSlotType.Accessory2,
        EquipmentSlotType.Accessory3
    ];

    private readonly Dictionary<EquipmentSlotType, ItemStack> _slots;

    public EquipmentLoadout()
    {
        _slots = Enum.GetValues<EquipmentSlotType>()
            .ToDictionary(slot => slot, _ => ItemStack.Empty);
    }

    public IReadOnlyDictionary<EquipmentSlotType, ItemStack> Slots => _slots;

    public ItemStack GetStack(EquipmentSlotType slot)
    {
        return _slots[slot];
    }

    public EquipmentChangeResult TryEquip(ItemStack stack, IItemDefinitionProvider items, EquipmentSlotType slot)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (stack.IsEmpty)
        {
            return EquipmentChangeResult.Failed("Cannot equip an empty stack.");
        }

        var definition = items.GetById(stack.ItemId);
        if (!CanEquip(definition, slot))
        {
            return EquipmentChangeResult.Failed($"Item '{stack.ItemId}' cannot be equipped in slot '{slot}'.");
        }

        var replaced = _slots[slot];
        _slots[slot] = new ItemStack(stack.ItemId, 1);
        return EquipmentChangeResult.Equipped(replaced);
    }

    public EquipmentChangeResult TryEquipFirstAvailable(ItemStack stack, IItemDefinitionProvider items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (stack.IsEmpty)
        {
            return EquipmentChangeResult.Failed("Cannot equip an empty stack.");
        }

        var definition = items.GetById(stack.ItemId);
        var preferredSlots = GetPreferredSlots(definition);
        foreach (var slot in preferredSlots)
        {
            if (!_slots[slot].IsEmpty)
            {
                continue;
            }

            return TryEquip(stack, items, slot);
        }

        foreach (var slot in preferredSlots)
        {
            return TryEquip(stack, items, slot);
        }

        return EquipmentChangeResult.Failed($"Item '{stack.ItemId}' is not equippable.");
    }

    public ItemStack Unequip(EquipmentSlotType slot)
    {
        var stack = _slots[slot];
        _slots[slot] = ItemStack.Empty;
        return stack;
    }

    public bool CanEquip(ItemDefinition definition, EquipmentSlotType slot)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Type == ItemType.Armor)
        {
            return definition.EquipmentSlot == slot && !IsAccessorySlot(slot);
        }

        if (definition.Type == ItemType.Accessory)
        {
            return IsAccessorySlot(slot) &&
                   (definition.EquipmentSlot is null || definition.EquipmentSlot == slot);
        }

        return false;
    }

    private static IEnumerable<EquipmentSlotType> GetPreferredSlots(ItemDefinition definition)
    {
        if (definition.Type == ItemType.Armor && definition.EquipmentSlot is { } armorSlot)
        {
            yield return armorSlot;
            yield break;
        }

        if (definition.Type == ItemType.Accessory)
        {
            if (definition.EquipmentSlot is { } accessorySlot)
            {
                yield return accessorySlot;
                yield break;
            }

            foreach (var slot in AccessorySlots)
            {
                yield return slot;
            }
        }
    }

    private static bool IsAccessorySlot(EquipmentSlotType slot)
    {
        return slot is EquipmentSlotType.Accessory1 or EquipmentSlotType.Accessory2 or EquipmentSlotType.Accessory3;
    }
}
