using Game.Core.Inventory;
using Game.Core.Items;
using Xunit;

namespace Game.Tests.InventoryTests;

public sealed class InventoryQueryServiceTests
{
    [Fact]
    public void Query_FiltersByCategoryRaritySearchAndFavoriteState()
    {
        var items = CreateItems();
        var inventory = new Inventory(4, items);
        inventory.Slots[0].SetStack(new ItemStack("gel", 5));
        inventory.Slots[1].SetStack(new ItemStack("star_blade", 1));
        inventory.SetFavorite(1, true);
        inventory.Slots[2].SetStack(new ItemStack("dirt", 20));
        var service = new InventoryQueryService(items);

        var result = service.Query(inventory, new InventoryQuery
        {
            Category = ItemCategory.Weapon,
            MinimumRarity = ItemRarity.Rare,
            SearchText = "celestial",
            FavoritesOnly = true
        });

        var entry = Assert.Single(result);
        Assert.Equal(1, entry.SlotIndex);
        Assert.Equal("star_blade", entry.Definition.Id);
        Assert.True(entry.IsFavorite);
    }

    [Fact]
    public void GetStatistics_AggregatesSlotsCountsValueCategoriesAndRarities()
    {
        var items = CreateItems();
        var inventory = new Inventory(4, items);
        inventory.Slots[0].SetStack(new ItemStack("gel", 5));
        inventory.Slots[1].SetStack(new ItemStack("star_blade", 1));
        inventory.SetFavorite(1, true);
        inventory.Slots[2].SetStack(new ItemStack("dirt", 20));

        var stats = new InventoryQueryService(items).GetStatistics(inventory);

        Assert.Equal(4, stats.TotalSlots);
        Assert.Equal(3, stats.UsedSlots);
        Assert.Equal(1, stats.AvailableSlots);
        Assert.Equal(26, stats.TotalItems);
        Assert.Equal(3, stats.UniqueItemTypes);
        Assert.Equal(1, stats.FavoriteSlots);
        Assert.Equal(545, stats.TotalValue);
        Assert.Equal(20, stats.ItemsByCategory[ItemCategory.Block]);
        Assert.Equal(1, stats.ItemsByRarity[ItemRarity.Legendary]);
    }

    [Fact]
    public void GetStatistics_CombinesHotbarAndMainInventory()
    {
        var items = CreateItems();
        var player = new PlayerInventory(items);
        player.Hotbar.Slots[0].SetStack(new ItemStack("gel", 2));
        player.Main.Slots[0].SetStack(new ItemStack("dirt", 3));

        var stats = new InventoryQueryService(items).GetStatistics(player);

        Assert.Equal(PlayerInventory.HotbarSlotCount + PlayerInventory.MainSlotCount, stats.TotalSlots);
        Assert.Equal(5, stats.TotalItems);
        Assert.Equal(2, stats.UniqueItemTypes);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "gel",
                DisplayName = "Gel",
                Description = "Sticky monster material.",
                Type = ItemType.Material,
                Value = 1,
                TexturePath = "items/gel",
                MaxStack = 99,
                Tags = new[] { "monster" }
            },
            new ItemDefinition
            {
                Id = "star_blade",
                DisplayName = "Star Blade",
                Description = "A celestial forged sword.",
                Type = ItemType.WeaponMelee,
                Rarity = ItemRarity.Legendary,
                Value = 500,
                TexturePath = "items/star_blade",
                MaxStack = 1,
                Tags = new[] { "celestial" }
            },
            new ItemDefinition
            {
                Id = "dirt",
                DisplayName = "Dirt Block",
                Type = ItemType.PlaceableTile,
                Value = 2,
                TexturePath = "items/dirt",
                MaxStack = 999,
                PlacesTileId = "dirt"
            }
        });
    }
}
