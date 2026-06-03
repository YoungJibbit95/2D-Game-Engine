using Game.Core.Inventory;

namespace Game.Core.Shops;

public sealed record ShopTransactionResult(
    bool Success,
    ShopTransactionKind Kind,
    string ShopId,
    ItemStack ItemStack,
    ItemStack CurrencyStack,
    string? FailureReason)
{
    public static ShopTransactionResult Failed(
        ShopTransactionKind kind,
        string shopId,
        string itemId,
        int itemCount,
        string reason)
    {
        return new ShopTransactionResult(false, kind, shopId, new ItemStack(itemId, itemCount), ItemStack.Empty, reason);
    }

    public static ShopTransactionResult Succeeded(
        ShopTransactionKind kind,
        string shopId,
        ItemStack itemStack,
        ItemStack currencyStack)
    {
        return new ShopTransactionResult(true, kind, shopId, itemStack, currencyStack, null);
    }
}
