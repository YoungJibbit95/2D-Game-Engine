using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class InventoryOrganizationTests
{
    [Fact]
    public void CompactStacks_RespectsMaxStackAndLeavesFavoritesPinned()
    {
        var inventory = new Inventory(4, CreateItems());
        inventory.Slots[0].SetStack(new ItemStack("gel", 3));
        inventory.Slots[1].SetStack(new ItemStack("gel", 7));
        inventory.Slots[2].SetStack(new ItemStack("gel", 4));
        inventory.SetFavorite(2, true);
        inventory.Slots[3].SetStack(new ItemStack("gel", 4));

        var result = inventory.CompactStacks();

        Assert.True(result.Changed);
        Assert.Equal(1, result.FreedSlots);
        Assert.Equal(10, inventory.Slots[0].Stack.Count);
        Assert.Equal(4, inventory.Slots[1].Stack.Count);
        Assert.Equal(new ItemStack("gel", 4), inventory.Slots[2].Stack);
        Assert.True(inventory.Slots[2].IsFavorite);
        Assert.True(inventory.Slots[3].IsEmpty);
        Assert.Equal(18, inventory.CountItem("gel"));
    }

    [Fact]
    public void CompactStacks_DoesNotDropOversizedLegacyStackWhenNoSlotsAreAvailable()
    {
        var inventory = new Inventory(1, CreateItems());
        inventory.Slots[0].SetStack(new ItemStack("gel", 15));

        var result = inventory.CompactStacks();

        Assert.False(result.Changed);
        Assert.Equal(15, inventory.Slots[0].Stack.Count);
    }

    [Theory]
    [InlineData(InventorySortMode.ItemType, "gem")]
    [InlineData(InventorySortMode.Rarity, "gem")]
    [InlineData(InventorySortMode.Name, "dirt")]
    [InlineData(InventorySortMode.Value, "gem")]
    public void Sort_OrdersByRequestedMetadata(InventorySortMode mode, string expectedFirst)
    {
        var inventory = CreateUnsortedInventory();

        inventory.Sort(mode);

        Assert.Equal(expectedFirst, inventory.Slots[0].Stack.ItemId);
        Assert.All(
            inventory.Slots.SkipWhile(slot => !slot.IsEmpty).ToArray(),
            slot => Assert.True(slot.IsEmpty));
    }

    [Fact]
    public void Sort_DoesNotMoveFavoriteSlots()
    {
        var inventory = CreateUnsortedInventory();
        inventory.SetFavorite(1, true);
        var pinned = inventory.Slots[1].Stack;

        inventory.Sort(InventorySortMode.Value);

        Assert.Equal(pinned, inventory.Slots[1].Stack);
        Assert.True(inventory.Slots[1].IsFavorite);
    }

    [Fact]
    public void SortPriority_PrecedesSelectedSortField()
    {
        var items = ItemRegistry.Create(new[]
        {
            Create("alpha", "Alpha", ItemType.Material, ItemRarity.Common, 1, 1) with { SortPriority = 10 },
            Create("zulu", "Zulu", ItemType.Material, ItemRarity.Common, 1, 1) with { SortPriority = -10 }
        });
        var inventory = new Inventory(2, items);
        inventory.Slots[0].SetStack(new ItemStack("alpha", 1));
        inventory.Slots[1].SetStack(new ItemStack("zulu", 1));

        inventory.Sort(InventorySortMode.Name);

        Assert.Equal("zulu", inventory.Slots[0].Stack.ItemId);
    }

    [Fact]
    public void PlayerSortMain_DoesNotReorderHotbar()
    {
        var items = CreateItems();
        var player = new PlayerInventory(items);
        player.Hotbar.Slots[0].SetStack(new ItemStack("sword", 1));
        player.Hotbar.Slots[1].SetStack(new ItemStack("gem", 1));
        player.Main.Slots[0].SetStack(new ItemStack("sword", 1));
        player.Main.Slots[1].SetStack(new ItemStack("gem", 1));

        player.SortMain(InventorySortMode.Value);

        Assert.Equal("sword", player.Hotbar.Slots[0].Stack.ItemId);
        Assert.Equal("gem", player.Hotbar.Slots[1].Stack.ItemId);
        Assert.Equal("gem", player.Main.Slots[0].Stack.ItemId);
    }

    [Fact]
    public void PlayerSortHotbarExplicit_ReordersHotbar()
    {
        var items = CreateItems();
        var player = new PlayerInventory(items);
        player.Hotbar.Slots[0].SetStack(new ItemStack("sword", 1));
        player.Hotbar.Slots[1].SetStack(new ItemStack("gem", 1));

        player.SortHotbarExplicit(InventorySortMode.Value);

        Assert.Equal("gem", player.Hotbar.Slots[0].Stack.ItemId);
    }

    private static Inventory CreateUnsortedInventory()
    {
        var inventory = new Inventory(6, CreateItems());
        inventory.Slots[0].SetStack(new ItemStack("potion", 1));
        inventory.Slots[1].SetStack(new ItemStack("sword", 1));
        inventory.Slots[2].SetStack(new ItemStack("dirt", 1));
        inventory.Slots[3].SetStack(new ItemStack("gem", 1));
        return inventory;
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            Create("gel", "Gel", ItemType.Material, ItemRarity.Common, 1, 10),
            Create("dirt", "Dirt Block", ItemType.PlaceableTile, ItemRarity.Common, 1, 999, placesTileId: "dirt"),
            Create("sword", "Steel Sword", ItemType.WeaponMelee, ItemRarity.Rare, 100, 1),
            Create("potion", "Health Potion", ItemType.Consumable, ItemRarity.Uncommon, 25, 30),
            Create("gem", "Quartz Gem", ItemType.Material, ItemRarity.Epic, 200, 99)
        });
    }

    private static ItemDefinition Create(
        string id,
        string name,
        ItemType type,
        ItemRarity rarity,
        int value,
        int maxStack,
        string? placesTileId = null)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = name,
            Type = type,
            Rarity = rarity,
            Value = value,
            TexturePath = $"items/{id}",
            MaxStack = maxStack,
            PlacesTileId = placesTileId
        };
    }
}
