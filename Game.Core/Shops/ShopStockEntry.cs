namespace Game.Core.Shops;

public sealed record ShopStockEntry
{
    public required string ItemId { get; init; }

    public int Count { get; init; } = 1;

    public int Price { get; init; }

    public string? CurrencyItemId { get; init; }

    public int SortOrder { get; init; }

    public IReadOnlyDictionary<string, string> Conditions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
