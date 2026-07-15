namespace Game.Core.Inventory;

public sealed class CursorItemState
{
    public ItemStack HeldStack { get; private set; } = ItemStack.Empty;

    public bool IsHoldingItem => !HeldStack.IsEmpty;

    public bool IsFavorite { get; private set; }

    public void Set(ItemStack stack, bool isFavorite = false)
    {
        HeldStack = stack.IsEmpty ? ItemStack.Empty : stack;
        IsFavorite = !HeldStack.IsEmpty && isFavorite;
    }

    public ItemStack Clear()
    {
        var stack = HeldStack;
        HeldStack = ItemStack.Empty;
        IsFavorite = false;
        return stack;
    }
}
