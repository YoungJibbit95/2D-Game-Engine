namespace Game.Core.Inventory;

public sealed class InventorySlot
{
    public ItemStack Stack { get; private set; } = ItemStack.Empty;

    public bool IsEmpty => Stack.IsEmpty;

    public bool CanAccept(ItemStack stack)
    {
        return stack.IsEmpty || IsEmpty || string.Equals(Stack.ItemId, stack.ItemId, StringComparison.OrdinalIgnoreCase);
    }

    public void SetStack(ItemStack stack)
    {
        Stack = stack.IsEmpty ? ItemStack.Empty : stack;
    }

    public ItemStack Clear()
    {
        var stack = Stack;
        Stack = ItemStack.Empty;
        return stack;
    }
}
