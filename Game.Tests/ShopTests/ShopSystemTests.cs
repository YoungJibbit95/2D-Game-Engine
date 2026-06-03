using Game.Core.Data;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Shops;
using Xunit;

namespace Game.Tests.ShopTests;

public sealed class ShopSystemTests
{
    [Fact]
    public void Loader_ReadsShopDefinitionJson()
    {
        var shop = new ShopDefinitionJsonLoader().LoadDefinitionFromJson("""
        {
          "id": "seed_shop",
          "displayName": "Seed Shop",
          "currencyItemId": "copper_coin",
          "tags": ["farm"],
          "stock": [
            { "itemId": "parsnip_seeds", "count": 2, "price": 5, "sortOrder": 10 }
          ],
          "sellPrices": [
            { "itemId": "parsnip", "price": 8 }
          ]
        }
        """);

        Assert.Equal("seed_shop", shop.Id);
        Assert.True(shop.HasTag("farm"));
        Assert.True(shop.TryGetStock("parsnip_seeds", out var stock));
        Assert.Equal(2, stock.Count);
        Assert.True(shop.TryGetSellPrice("parsnip", out var sell));
        Assert.Equal(8, sell.Price);
    }

    [Fact]
    public void Registry_RejectsDuplicateStockItems()
    {
        var shop = new ShopDefinition
        {
            Id = "bad",
            DisplayName = "Bad",
            Stock = new[]
            {
                new ShopStockEntry { ItemId = "seed", Count = 1, Price = 1 },
                new ShopStockEntry { ItemId = "seed", Count = 1, Price = 2 }
            }
        };

        Assert.Throws<RegistryValidationException>(() => ShopRegistry.Create(new[] { shop }));
    }

    [Fact]
    public void Buy_RemovesCurrencyAndAddsStock()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("copper_coin", 20));
        var shop = CreateShop();

        var result = new ShopTransactionService().Buy(shop, inventory, "parsnip_seeds", quantity: 2);

        Assert.True(result.Success);
        Assert.Equal(new ItemStack("parsnip_seeds", 2), result.ItemStack);
        Assert.Equal(new ItemStack("copper_coin", 10), result.CurrencyStack);
        Assert.Equal(10, inventory.CountItem("copper_coin"));
        Assert.Equal(2, inventory.CountItem("parsnip_seeds"));
    }

    [Fact]
    public void Buy_FailsWithoutEnoughCurrency()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("copper_coin", 4));
        var shop = CreateShop();

        var result = new ShopTransactionService().Buy(shop, inventory, "parsnip_seeds");

        Assert.False(result.Success);
        Assert.Equal("not_enough_currency", result.FailureReason);
        Assert.Equal(4, inventory.CountItem("copper_coin"));
        Assert.Equal(0, inventory.CountItem("parsnip_seeds"));
    }

    [Fact]
    public void Sell_RemovesItemAndAddsCurrency()
    {
        var items = CreateItems();
        var inventory = new PlayerInventory(items);
        inventory.AddItem(new ItemStack("parsnip", 3));
        var shop = CreateShop();

        var result = new ShopTransactionService().Sell(shop, inventory, "parsnip", quantity: 2);

        Assert.True(result.Success);
        Assert.Equal(new ItemStack("parsnip", 2), result.ItemStack);
        Assert.Equal(new ItemStack("copper_coin", 16), result.CurrencyStack);
        Assert.Equal(1, inventory.CountItem("parsnip"));
        Assert.Equal(16, inventory.CountItem("copper_coin"));
    }

    [Fact]
    public void Sell_CanUseFreedSlotForCurrency()
    {
        var items = CreateItems();
        var fullHotbar = new Inventory(PlayerInventory.HotbarSlotCount, items);
        var fullMain = new Inventory(PlayerInventory.MainSlotCount, items);
        for (var i = 0; i < fullHotbar.Slots.Count; i++)
        {
            fullHotbar.Slots[i].SetStack(new ItemStack("stone", 999));
        }

        for (var i = 0; i < fullMain.Slots.Count; i++)
        {
            fullMain.Slots[i].SetStack(new ItemStack("stone", 999));
        }

        fullHotbar.Slots[0].SetStack(new ItemStack("parsnip", 1));
        var inventory = new PlayerInventory(fullHotbar, fullMain, items);

        var result = new ShopTransactionService().Sell(CreateShop(), inventory, "parsnip");

        Assert.True(result.Success);
        Assert.Equal(0, inventory.CountItem("parsnip"));
        Assert.Equal(8, inventory.CountItem("copper_coin"));
    }

    private static ShopDefinition CreateShop()
    {
        return new ShopDefinition
        {
            Id = "seed_shop",
            DisplayName = "Seed Shop",
            CurrencyItemId = "copper_coin",
            Stock = new[]
            {
                new ShopStockEntry { ItemId = "parsnip_seeds", Count = 1, Price = 5 }
            },
            SellPrices = new[]
            {
                new ShopSellEntry { ItemId = "parsnip", Price = 8 }
            }
        };
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            CreateItem("copper_coin", 999),
            CreateItem("parsnip_seeds", 99),
            CreateItem("parsnip", 99),
            CreateItem("stone", 999)
        });
    }

    private static ItemDefinition CreateItem(string id, int maxStack)
    {
        return new ItemDefinition
        {
            Id = id,
            DisplayName = id,
            Type = ItemType.Material,
            TexturePath = $"items/{id}",
            MaxStack = maxStack
        };
    }
}
