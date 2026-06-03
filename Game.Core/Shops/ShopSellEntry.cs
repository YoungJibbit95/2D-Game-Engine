namespace Game.Core.Shops;

public sealed record ShopSellEntry
{
    public required string ItemId { get; init; }

    public int Price { get; init; }

    public string? CurrencyItemId { get; init; }
}
