namespace Game.Core.Items;

public static class ItemActionResolver
{
    public static ItemActionDefinition GetPrimaryAction(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Actions.Count > 0
            ? item.Actions[0]
            : InferFromLegacyType(item.Type);
    }

    public static ItemActionDefinition InferFromLegacyType(ItemType type)
    {
        return type switch
        {
            ItemType.PlaceableTile => ItemActionDefinition.Create(ItemActionKind.Place),
            ItemType.ToolPickaxe => ItemActionDefinition.Create(ItemActionKind.Mine),
            ItemType.WeaponMelee or ItemType.ToolAxe => ItemActionDefinition.Create(ItemActionKind.Melee),
            ItemType.WeaponRanged => ItemActionDefinition.Create(ItemActionKind.Shoot),
            ItemType.WeaponMagic => ItemActionDefinition.Create(ItemActionKind.Cast),
            ItemType.Consumable or ItemType.ManaPotion => ItemActionDefinition.Create(ItemActionKind.Consume),
            ItemType.Seed => ItemActionDefinition.Create(ItemActionKind.Plant),
            ItemType.ToolHoe => ItemActionDefinition.Create(ItemActionKind.Till),
            ItemType.ToolWateringCan => ItemActionDefinition.Create(ItemActionKind.Water),
            _ => ItemActionDefinition.Create(ItemActionKind.None)
        };
    }
}
