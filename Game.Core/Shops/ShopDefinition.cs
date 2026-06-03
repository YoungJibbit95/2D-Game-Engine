using Game.Core.Data;

namespace Game.Core.Shops;

public sealed record ShopDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string CurrencyItemId { get; init; } = "copper_coin";

    public IReadOnlyList<ShopStockEntry> Stock { get; init; } = Array.Empty<ShopStockEntry>();

    public IReadOnlyList<ShopSellEntry> SellPrices { get; init; } = Array.Empty<ShopSellEntry>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool TryGetStock(string itemId, out ShopStockEntry stock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        stock = Stock.FirstOrDefault(item => string.Equals(item.ItemId, itemId, StringComparison.OrdinalIgnoreCase))!;
        return stock is not null;
    }

    public bool TryGetSellPrice(string itemId, out ShopSellEntry sellPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        sellPrice = SellPrices.FirstOrDefault(item => string.Equals(item.ItemId, itemId, StringComparison.OrdinalIgnoreCase))!;
        return sellPrice is not null;
    }

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
