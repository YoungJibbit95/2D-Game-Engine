using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class InventoryInteractionServiceTests
{
    [Fact]
    public void LeftClick_PicksUpAndPlacesStack()
    {
        var items = CreateItems();
        var slot = new InventorySlot();
        var cursor = new CursorItemState();
        var service = new InventoryInteractionService(items);
        slot.SetStack(new ItemStack("gel", 10));

        service.Click(slot, cursor, InventoryClickButton.Left);
        service.Click(slot, cursor, InventoryClickButton.Left);

        Assert.False(cursor.IsHoldingItem);
        Assert.Equal(new ItemStack("gel", 10), slot.Stack);
    }

    [Fact]
    public void RightClick_SplitsAndPlacesOne()
    {
        var items = CreateItems();
        var slot = new InventorySlot();
        var target = new InventorySlot();
        var cursor = new CursorItemState();
        var service = new InventoryInteractionService(items);
        slot.SetStack(new ItemStack("gel", 9));

        service.Click(slot, cursor, InventoryClickButton.Right);
        service.Click(target, cursor, InventoryClickButton.Right);

        Assert.Equal(4, slot.Stack.Count);
        Assert.Equal(1, target.Stack.Count);
        Assert.Equal(4, cursor.HeldStack.Count);
    }

    [Fact]
    public void LeftClick_MergesIntoSameStackRespectingMaxStack()
    {
        var items = CreateItems(maxStack: 12);
        var slot = new InventorySlot();
        var cursor = new CursorItemState();
        var service = new InventoryInteractionService(items);
        slot.SetStack(new ItemStack("gel", 10));
        cursor.Set(new ItemStack("gel", 5));

        service.Click(slot, cursor, InventoryClickButton.Left);

        Assert.Equal(12, slot.Stack.Count);
        Assert.Equal(3, cursor.HeldStack.Count);
    }

    [Fact]
    public void LeftClick_CarriesFavoriteStateWithMovedAndSwappedStacks()
    {
        var items = CreateItems();
        var source = new InventorySlot();
        var target = new InventorySlot();
        var cursor = new CursorItemState();
        var service = new InventoryInteractionService(items);
        source.SetStack(new ItemStack("gel", 5));
        source.SetFavorite(true);

        service.Click(source, cursor, InventoryClickButton.Left);
        service.Click(target, cursor, InventoryClickButton.Left);

        Assert.True(source.IsEmpty);
        Assert.False(cursor.IsHoldingItem);
        Assert.Equal(new ItemStack("gel", 5), target.Stack);
        Assert.True(target.IsFavorite);
    }

    private static ItemRegistry CreateItems(int maxStack = 999)
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "gel",
                DisplayName = "Gel",
                Type = ItemType.Material,
                TexturePath = "items/gel",
                MaxStack = maxStack
            }
        });
    }
}
