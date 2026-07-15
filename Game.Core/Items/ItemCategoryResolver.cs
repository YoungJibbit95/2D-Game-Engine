namespace Game.Core.Items;

public static class ItemCategoryResolver
{
    public static ItemCategory Resolve(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Category != ItemCategory.Automatic
            ? item.Category
            : Resolve(item.Type);
    }

    public static ItemCategory Resolve(ItemType type)
    {
        return type switch
        {
            ItemType.Material => ItemCategory.Material,
            ItemType.PlaceableTile => ItemCategory.Block,
            ItemType.WeaponMelee or ItemType.WeaponRanged or ItemType.WeaponMagic => ItemCategory.Weapon,
            ItemType.ToolPickaxe or ItemType.ToolAxe or ItemType.ToolHoe or ItemType.ToolWateringCan => ItemCategory.Tool,
            ItemType.Consumable or ItemType.ManaPotion => ItemCategory.Consumable,
            ItemType.Armor or ItemType.Accessory => ItemCategory.Equipment,
            ItemType.Ammo => ItemCategory.Ammo,
            ItemType.Seed => ItemCategory.Farming,
            ItemType.QuestItem => ItemCategory.Quest,
            _ => ItemCategory.Miscellaneous
        };
    }
}
