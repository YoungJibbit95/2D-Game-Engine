using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class InventoryTransactionTests
{
    [Fact]
    public void AddTransaction_IsAtomicWhenInventoryHasInsufficientSpace()
    {
        var inventory = new Inventory(1, CreateItems());
        inventory.AddItem(new ItemStack("gel", 8));

        var result = inventory.AddTransaction(new ItemStack("gel", 5));

        Assert.Equal(InventoryTransactionStatus.NoSpace, result.Status);
        Assert.Equal(0, result.Moved);
        Assert.Equal(5, result.Remaining);
        Assert.Equal(8, inventory.CountItem("gel"));
    }

    [Fact]
    public void AddTransaction_PartialModeReportsMovedAndRemainingAmounts()
    {
        var inventory = new Inventory(1, CreateItems());
        inventory.AddItem(new ItemStack("gel", 8));

        var result = inventory.AddTransaction(
            new ItemStack("gel", 5),
            InventoryTransactionOptions.Partial);

        Assert.Equal(InventoryTransactionStatus.Partial, result.Status);
        Assert.Equal(2, result.Moved);
        Assert.Equal(3, result.Remaining);
        Assert.Equal(10, inventory.CountItem("gel"));
    }

    [Fact]
    public void AddTransaction_RejectsUnknownItemWithoutMutation()
    {
        var inventory = new Inventory(2, CreateItems());

        var result = inventory.AddTransaction(new ItemStack("unknown", 4));

        Assert.Equal(InventoryTransactionStatus.UnknownItem, result.Status);
        Assert.All(inventory.Slots, slot => Assert.True(slot.IsEmpty));
    }

    [Fact]
    public void RemoveTransaction_DoesNotConsumeFavoriteStacksToCompleteAtomicRequest()
    {
        var inventory = new Inventory(2, CreateItems());
        inventory.Slots[0].SetStack(new ItemStack("gel", 3));
        inventory.Slots[1].SetStack(new ItemStack("gel", 4));
        inventory.SetFavorite(1, true);

        var result = inventory.RemoveTransaction("gel", 5);

        Assert.Equal(InventoryTransactionStatus.Protected, result.Status);
        Assert.Equal(7, inventory.CountItem("gel"));
        Assert.True(inventory.Slots[1].IsFavorite);
    }

    [Fact]
    public void RemoveTransaction_PartialModeLeavesFavoriteStackUntouched()
    {
        var inventory = new Inventory(2, CreateItems());
        inventory.Slots[0].SetStack(new ItemStack("gel", 3));
        inventory.Slots[1].SetStack(new ItemStack("gel", 4));
        inventory.SetFavorite(1, true);

        var result = inventory.RemoveTransaction(
            "gel",
            5,
            InventoryTransactionOptions.Partial);

        Assert.Equal(3, result.Moved);
        Assert.Equal(2, result.Remaining);
        Assert.True(inventory.Slots[0].IsEmpty);
        Assert.Equal(4, inventory.Slots[1].Stack.Count);
    }

    [Fact]
    public void TransferTo_IsAtomicAcrossSourceAndTarget()
    {
        var items = CreateItems();
        var source = new Inventory(1, items);
        var target = new Inventory(1, items);
        source.AddItem(new ItemStack("gel", 5));
        target.AddItem(new ItemStack("gel", 8));

        var result = source.TransferTo(target, "gel", 5);

        Assert.Equal(InventoryTransactionStatus.NoSpace, result.Status);
        Assert.Equal(5, source.CountItem("gel"));
        Assert.Equal(8, target.CountItem("gel"));
    }

    [Fact]
    public void TransferTo_PartialModeNeverLosesItems()
    {
        var items = CreateItems();
        var source = new Inventory(1, items);
        var target = new Inventory(1, items);
        source.AddItem(new ItemStack("gel", 5));
        target.AddItem(new ItemStack("gel", 8));

        var result = source.TransferTo(
            target,
            "gel",
            5,
            InventoryTransactionOptions.Partial);

        Assert.Equal(InventoryTransactionStatus.Partial, result.Status);
        Assert.Equal(2, result.Moved);
        Assert.Equal(3, result.Remaining);
        Assert.Equal(3, source.CountItem("gel"));
        Assert.Equal(10, target.CountItem("gel"));
    }

    [Fact]
    public void TransferSlotTo_RejectsFavoriteStack()
    {
        var items = CreateItems();
        var source = new Inventory(1, items);
        var target = new Inventory(1, items);
        source.AddItem(new ItemStack("gel", 5));
        source.SetFavorite(0, true);

        var result = source.TransferSlotTo(target, 0);

        Assert.Equal(InventoryTransactionStatus.Protected, result.Status);
        Assert.Equal(5, source.CountItem("gel"));
        Assert.True(target.Slots[0].IsEmpty);
    }

    [Fact]
    public void TransferSlotTo_ExplicitFavoriteOverrideCarriesFavoriteState()
    {
        var items = CreateItems();
        var source = new Inventory(1, items);
        var target = new Inventory(1, items);
        source.AddItem(new ItemStack("gel", 5));
        source.SetFavorite(0, true);

        var result = source.TransferSlotTo(
            target,
            0,
            new InventoryTransactionOptions(IncludeFavorites: true));

        Assert.True(result.Completed);
        Assert.True(source.Slots[0].IsEmpty);
        Assert.Equal(5, target.Slots[0].Stack.Count);
        Assert.True(target.Slots[0].IsFavorite);
    }

    [Fact]
    public void TrashSlot_RespectsFavoriteAndDefinitionProtection()
    {
        var inventory = new Inventory(2, CreateItems());
        inventory.Slots[0].SetStack(new ItemStack("gel", 2));
        inventory.SetFavorite(0, true);
        inventory.Slots[1].SetStack(new ItemStack("relic", 1));

        var favoriteResult = inventory.TrashSlot(0);
        var relicResult = inventory.TrashSlot(1);

        Assert.Equal(InventoryTransactionStatus.Protected, favoriteResult.Status);
        Assert.Equal(InventoryTransactionStatus.Protected, relicResult.Status);
        Assert.Equal(2, inventory.Slots[0].Stack.Count);
        Assert.Equal("relic", inventory.Slots[1].Stack.ItemId);
    }

    [Fact]
    public void SetFavorite_RespectsDefinitionProtection()
    {
        var inventory = new Inventory(1, CreateItems());
        inventory.Slots[0].SetStack(new ItemStack("relic", 1));

        var changed = inventory.SetFavorite(0, true);

        Assert.False(changed);
        Assert.False(inventory.Slots[0].IsFavorite);
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
                MaxStack = 10
            },
            new ItemDefinition
            {
                Id = "relic",
                DisplayName = "Ancient Relic",
                Type = ItemType.QuestItem,
                TexturePath = "items/relic",
                MaxStack = 1,
                CanFavorite = false,
                CanTrash = false
            }
        });
    }
}
