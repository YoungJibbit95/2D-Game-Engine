namespace Game.Core.Inventory;

public sealed class CursorItemState
{
    public ItemStack HeldStack { get; private set; } = ItemStack.Empty;

    public bool IsHoldingItem => !HeldStack.IsEmpty;

    public void Set(ItemStack stack)
    {
        HeldStack = stack.IsEmpty ? ItemStack.Empty : stack;
    }

    public ItemStack Clear()
    {
        var stack = HeldStack;
        HeldStack = ItemStack.Empty;
        return stack;
    }
}
