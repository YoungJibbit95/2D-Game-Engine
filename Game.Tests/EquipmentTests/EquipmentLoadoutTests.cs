using Game.Core.Equipment;
using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.EquipmentTests;

public sealed class EquipmentLoadoutTests
{
    [Fact]
    public void TryEquip_EquipsArmorOnlyInConfiguredSlot()
    {
        var items = CreateItems();
        var loadout = new EquipmentLoadout();

        var wrongSlot = loadout.TryEquip(new ItemStack("copper_helmet", 1), items, EquipmentSlotType.Body);
        var rightSlot = loadout.TryEquip(new ItemStack("copper_helmet", 1), items, EquipmentSlotType.Head);

        Assert.False(wrongSlot.Success);
        Assert.True(rightSlot.Success);
        Assert.Equal("copper_helmet", loadout.GetStack(EquipmentSlotType.Head).ItemId);
    }

    [Fact]
    public void TryEquipFirstAvailable_UsesEmptyAccessorySlot()
    {
        var items = CreateItems();
        var loadout = new EquipmentLoadout();

        var result = loadout.TryEquipFirstAvailable(new ItemStack("swift_charm", 1), items);

        Assert.True(result.Success);
        Assert.Equal("swift_charm", loadout.GetStack(EquipmentSlotType.Accessory1).ItemId);
    }

    [Fact]
    public void Calculate_AddsEquipmentStatsToBaseStats()
    {
        var items = CreateItems();
        var loadout = new EquipmentLoadout();
        loadout.TryEquip(new ItemStack("copper_helmet", 1), items, EquipmentSlotType.Head);
        loadout.TryEquip(new ItemStack("swift_charm", 1), items, EquipmentSlotType.Accessory1);

        var stats = new EquipmentStatCalculator().Calculate(PlayerStatBlock.Base, loadout, items);

        Assert.Equal(120, stats.MaxHealth);
        Assert.Equal(2, stats.Defense);
        Assert.Equal(1.15f, stats.MovementSpeedMultiplier, precision: 3);
        Assert.Equal(1.10f, stats.MiningSpeedMultiplier, precision: 3);
    }

    [Fact]
    public void ItemDefinitionJsonLoader_LoadsEquipmentFields()
    {
        var definition = new ItemDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "copper_helmet",
          "displayName": "Copper Helmet",
          "type": "Armor",
          "texture": "items/copper_helmet",
          "equipmentSlot": "Head",
          "defense": 2,
          "maxHealthBonus": 20
        }
        """);

        Assert.Equal(EquipmentSlotType.Head, definition.EquipmentSlot);
        Assert.Equal(2, definition.Defense);
        Assert.Equal(20, definition.MaxHealthBonus);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(
        [
            new ItemDefinition
            {
                Id = "copper_helmet",
                DisplayName = "Copper Helmet",
                Type = ItemType.Armor,
                TexturePath = "items/copper_helmet",
                MaxStack = 1,
                EquipmentSlot = EquipmentSlotType.Head,
                Defense = 2,
                MaxHealthBonus = 20
            },
            new ItemDefinition
            {
                Id = "swift_charm",
                DisplayName = "Swift Charm",
                Type = ItemType.Accessory,
                TexturePath = "items/swift_charm",
                MaxStack = 1,
                MovementSpeedBonus = 0.15f,
                MiningSpeedBonus = 0.10f
            }
        ]);
    }
}
