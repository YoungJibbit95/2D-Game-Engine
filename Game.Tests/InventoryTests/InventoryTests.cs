using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class InventoryTests
{
    [Fact]
    public void AddItem_MergesExistingStacksBeforeUsingEmptySlots()
    {
        var inventory = CreateInventory(slotCount: 3);
        inventory.AddItem(new ItemStack("dirt_block", 900));

        var addedAll = inventory.AddItem(new ItemStack("dirt_block", 150));

        Assert.True(addedAll);
        Assert.Equal(999, inventory.Slots[0].Stack.Count);
        Assert.Equal(51, inventory.Slots[1].Stack.Count);
    }

    [Fact]
    public void AddItem_ReturnsFalseWhenInventoryRunsOutOfSpace()
    {
        var inventory = CreateInventory(slotCount: 1);

        var addedAll = inventory.AddItem(new ItemStack("dirt_block", 1500));

        Assert.False(addedAll);
        Assert.Equal(999, inventory.Slots[0].Stack.Count);
    }

    [Fact]
    public void RemoveItem_IsAtomicWhenNotEnoughItemsExist()
    {
        var inventory = CreateInventory(slotCount: 2);
        inventory.AddItem(new ItemStack("dirt_block", 5));

        var removed = inventory.RemoveItem("dirt_block", 6);

        Assert.False(removed);
        Assert.Equal(5, inventory.CountItem("dirt_block"));
    }

    [Fact]
    public void SwapSlots_ExchangesStacks()
    {
        var inventory = CreateInventory(slotCount: 2);
        inventory.AddItem(new ItemStack("dirt_block", 5));
        inventory.AddItem(new ItemStack("copper_pickaxe", 1));

        inventory.SwapSlots(0, 1);

        Assert.Equal("copper_pickaxe", inventory.Slots[0].Stack.ItemId);
        Assert.Equal("dirt_block", inventory.Slots[1].Stack.ItemId);
    }

    [Fact]
    public void SplitStack_ReturnsHalfAndLeavesRemainderInSlot()
    {
        var inventory = CreateInventory(slotCount: 1);
        inventory.AddItem(new ItemStack("dirt_block", 11));

        var split = inventory.SplitStack(0);

        Assert.Equal(new ItemStack("dirt_block", 5), split);
        Assert.Equal(6, inventory.Slots[0].Stack.Count);
    }

    [Fact]
    public void MergeStack_MovesSourceIntoCompatibleTarget()
    {
        var inventory = CreateInventory(slotCount: 2);
        inventory.AddItem(new ItemStack("dirt_block", 999));
        inventory.Slots[0].SetStack(new ItemStack("dirt_block", 900));
        inventory.Slots[1].SetStack(new ItemStack("dirt_block", 150));

        var fullyMerged = inventory.MergeStack(1, 0);

        Assert.False(fullyMerged);
        Assert.Equal(999, inventory.Slots[0].Stack.Count);
        Assert.Equal(51, inventory.Slots[1].Stack.Count);
    }

    private static Inventory CreateInventory(int slotCount)
    {
        return new Inventory(slotCount, CreateRegistry());
    }

    private static ItemRegistry CreateRegistry()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "dirt_block",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                TexturePath = "items/dirt_block",
                MaxStack = 999,
                PlacesTileId = "dirt"
            },
            new ItemDefinition
            {
                Id = "copper_pickaxe",
                DisplayName = "Copper Pickaxe",
                Type = ItemType.ToolPickaxe,
                TexturePath = "items/copper_pickaxe",
                MaxStack = 1,
                ToolPower = 35
            }
        });
    }
}
