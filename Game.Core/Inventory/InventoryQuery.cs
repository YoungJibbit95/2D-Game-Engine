using Game.Core.Items;

namespace Game.Core.Inventory;

public sealed record InventoryQuery
{
    public string? SearchText { get; init; }

    public ItemCategory? Category { get; init; }

    public ItemRarity? MinimumRarity { get; init; }

    public bool FavoritesOnly { get; init; }
}
