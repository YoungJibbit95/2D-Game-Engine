namespace Game.Core.Inventory;

public readonly record struct ItemStack(string ItemId, int Count)
{
    public static ItemStack Empty { get; } = new(string.Empty, 0);

    public bool IsEmpty => Count <= 0 || string.IsNullOrWhiteSpace(ItemId);

    public ItemStack WithCount(int count)
    {
        return count <= 0 ? Empty : this with { Count = count };
    }
}
