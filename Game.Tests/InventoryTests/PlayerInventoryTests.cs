using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class PlayerInventoryTests
{
    [Fact]
    public void AddItem_FillsHotbarBeforeMainInventory()
    {
        var inventory = new PlayerInventory(CreateItems());

        var added = inventory.AddItem(new ItemStack("gel", 12));

        Assert.True(added);
        Assert.Equal(new ItemStack("gel", 12), inventory.Hotbar.Slots[0].Stack);
        Assert.True(inventory.Main.Slots[0].IsEmpty);
    }

    [Fact]
    public void AddItem_CanSplitAcrossHotbarAndMainInventory()
    {
        var inventory = new PlayerInventory(CreateItems());
        for (var slot = 0; slot < PlayerInventory.HotbarSlotCount; slot++)
        {
            inventory.Hotbar.Slots[slot].SetStack(new ItemStack("gel", 99));
        }

        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 90));
        var added = inventory.AddItem(new ItemStack("gel", 15));

        Assert.True(added);
        Assert.Equal(99, inventory.Hotbar.Slots[0].Stack.Count);
        Assert.Equal(new ItemStack("gel", 6), inventory.Main.Slots[0].Stack);
    }

    [Fact]
    public void HotbarSelection_WrapsWithScroll()
    {
        var inventory = new PlayerInventory(CreateItems());

        inventory.ScrollHotbar(-1);
        Assert.Equal(9, inventory.SelectedHotbarSlot);

        inventory.ScrollHotbar(2);
        Assert.Equal(1, inventory.SelectedHotbarSlot);
    }

    [Fact]
    public void QuickMoveToMain_MovesSelectedStackOutOfHotbar()
    {
        var inventory = new PlayerInventory(CreateItems());
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 3));

        var moved = inventory.QuickMoveToMain(0);

        Assert.True(moved);
        Assert.True(inventory.Hotbar.Slots[0].IsEmpty);
        Assert.Equal(new ItemStack("gel", 3), inventory.Main.Slots[0].Stack);
    }

    [Fact]
    public void AddTransaction_IsAtomicAcrossHotbarAndMain()
    {
        var inventory = new PlayerInventory(CreateItems());
        foreach (var slot in inventory.Hotbar.Slots.Concat(inventory.Main.Slots))
        {
            slot.SetStack(new ItemStack("gel", 99));
        }

        inventory.Main.Slots[^1].SetStack(new ItemStack("gel", 98));
        var before = inventory.CountItem("gel");

        var result = inventory.AddTransaction(new ItemStack("gel", 2));

        Assert.Equal(InventoryTransactionStatus.NoSpace, result.Status);
        Assert.Equal(before, inventory.CountItem("gel"));
    }

    [Fact]
    public void QuickMoveToMain_LeavesFavoriteHotbarStackInPlace()
    {
        var inventory = new PlayerInventory(CreateItems());
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 3));
        inventory.Hotbar.SetFavorite(0, true);

        var result = inventory.QuickMoveToMainTransaction(0);

        Assert.Equal(InventoryTransactionStatus.Protected, result.Status);
        Assert.Equal(3, inventory.Hotbar.Slots[0].Stack.Count);
        Assert.True(inventory.Main.Slots[0].IsEmpty);
    }

    [Fact]
    public void AddSlotStateTransaction_PreservesFavoriteStateWithoutMergingIntoRegularStack()
    {
        var inventory = new PlayerInventory(CreateItems());
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 40));

        var result = inventory.AddSlotStateTransaction(
            new InventorySlotState(new ItemStack("gel", 12), IsFavorite: true));

        Assert.True(result.Completed);
        Assert.Equal(40, inventory.Hotbar.Slots[0].Stack.Count);
        Assert.Equal(new ItemStack("gel", 12), inventory.Hotbar.Slots[1].Stack);
        Assert.True(inventory.Hotbar.Slots[1].IsFavorite);
    }

    [Fact]
    public void AddSlotStateTransaction_NoSpaceDoesNotPartiallyMutateInventory()
    {
        var inventory = new PlayerInventory(CreateItems());
        foreach (var slot in inventory.Hotbar.Slots.Concat(inventory.Main.Slots))
        {
            slot.SetStack(new ItemStack("gel", 99));
        }

        inventory.Main.Slots[^1].SetStack(new ItemStack("gel", 98));
        var before = inventory.CountItem("gel");

        var result = inventory.AddSlotStateTransaction(
            new InventorySlotState(new ItemStack("gel", 2), IsFavorite: false));

        Assert.Equal(InventoryTransactionStatus.NoSpace, result.Status);
        Assert.Equal(before, inventory.CountItem("gel"));
    }

    [Fact]
    public void InventoryVersion_ChangesOnlyWhenSlotStateChanges()
    {
        var inventory = new PlayerInventory(CreateItems());
        var initial = inventory.Hotbar.Version;

        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 4));
        var afterStack = inventory.Hotbar.Version;
        inventory.Hotbar.Slots[0].SetStack(new ItemStack("gel", 4));
        inventory.Hotbar.SetFavorite(0, true);

        Assert.True(afterStack > initial);
        Assert.Equal(afterStack + 1, inventory.Hotbar.Version);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "gel",
                DisplayName = "Gel",
                Type = ItemType.Material,
                TexturePath = "items/gel",
                MaxStack = 99
            }
        });
    }
}
