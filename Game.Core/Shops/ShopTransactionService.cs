using Game.Core.Inventory;

namespace Game.Core.Shops;

public sealed class ShopTransactionService
{
    public ShopTransactionResult Buy(ShopDefinition shop, PlayerInventory inventory, string stockItemId, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stockItemId);

        if (quantity <= 0)
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Buy, shop.Id, stockItemId, quantity, "invalid_quantity");
        }

        if (!shop.TryGetStock(stockItemId, out var stock))
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Buy, shop.Id, stockItemId, quantity, "stock_missing");
        }

        var itemStack = new ItemStack(stock.ItemId, stock.Count * quantity);
        var currencyStack = new ItemStack(ResolveCurrency(shop, stock.CurrencyItemId), stock.Price * quantity);

        if (!inventory.CanAddItem(itemStack))
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Buy, shop.Id, itemStack.ItemId, itemStack.Count, "inventory_full");
        }

        if (!currencyStack.IsEmpty && inventory.CountItem(currencyStack.ItemId) < currencyStack.Count)
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Buy, shop.Id, itemStack.ItemId, itemStack.Count, "not_enough_currency");
        }

        if (!currencyStack.IsEmpty)
        {
            inventory.RemoveItem(currencyStack.ItemId, currencyStack.Count);
        }

        inventory.AddItem(itemStack);
        return ShopTransactionResult.Succeeded(ShopTransactionKind.Buy, shop.Id, itemStack, currencyStack);
    }

    public ShopTransactionResult Sell(ShopDefinition shop, PlayerInventory inventory, string itemId, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        if (quantity <= 0)
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Sell, shop.Id, itemId, quantity, "invalid_quantity");
        }

        if (!shop.TryGetSellPrice(itemId, out var sellPrice))
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Sell, shop.Id, itemId, quantity, "sell_price_missing");
        }

        var itemStack = new ItemStack(sellPrice.ItemId, quantity);
        var currencyStack = new ItemStack(ResolveCurrency(shop, sellPrice.CurrencyItemId), sellPrice.Price * quantity);

        if (inventory.CountItem(itemStack.ItemId) < itemStack.Count)
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Sell, shop.Id, itemStack.ItemId, itemStack.Count, "not_enough_items");
        }

        var simulated = new PlayerInventory(inventory.Hotbar.Clone(), inventory.Main.Clone(), inventory.ItemDefinitions);
        simulated.RemoveItem(itemStack.ItemId, itemStack.Count);
        if (!currencyStack.IsEmpty && !simulated.CanAddItem(currencyStack))
        {
            return ShopTransactionResult.Failed(ShopTransactionKind.Sell, shop.Id, itemStack.ItemId, itemStack.Count, "inventory_cannot_accept_currency");
        }

        inventory.RemoveItem(itemStack.ItemId, itemStack.Count);
        if (!currencyStack.IsEmpty)
        {
            inventory.AddItem(currencyStack);
        }

        return ShopTransactionResult.Succeeded(ShopTransactionKind.Sell, shop.Id, itemStack, currencyStack);
    }

    private static string ResolveCurrency(ShopDefinition shop, string? overrideCurrency)
    {
        return string.IsNullOrWhiteSpace(overrideCurrency) ? shop.CurrencyItemId : overrideCurrency;
    }
}
