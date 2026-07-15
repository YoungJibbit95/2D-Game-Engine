namespace Game.Core.Inventory;

public sealed class InventorySlot
{
    private readonly Action? _changed;

    public InventorySlot(Action? changed = null)
    {
        _changed = changed;
    }

    public ItemStack Stack { get; private set; } = ItemStack.Empty;

    public bool IsEmpty => Stack.IsEmpty;

    public bool IsFavorite { get; private set; }

    public bool CanAccept(ItemStack stack)
    {
        return stack.IsEmpty || IsEmpty || string.Equals(Stack.ItemId, stack.ItemId, StringComparison.OrdinalIgnoreCase);
    }

    public void SetStack(ItemStack stack)
    {
        var previous = GetState();
        var normalized = stack.IsEmpty ? ItemStack.Empty : stack;
        if (normalized.IsEmpty || !string.Equals(Stack.ItemId, normalized.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            IsFavorite = false;
        }

        Stack = normalized;
        NotifyIfChanged(previous);
    }

    public void SetState(InventorySlotState state)
    {
        var previous = GetState();
        Stack = state.Stack.IsEmpty ? ItemStack.Empty : state.Stack;
        IsFavorite = !Stack.IsEmpty && state.IsFavorite;
        NotifyIfChanged(previous);
    }

    public InventorySlotState GetState()
    {
        return new InventorySlotState(Stack, IsFavorite);
    }

    public bool SetFavorite(bool favorite)
    {
        var previous = GetState();
        if (IsEmpty)
        {
            IsFavorite = false;
            NotifyIfChanged(previous);
            return !favorite;
        }

        IsFavorite = favorite;
        NotifyIfChanged(previous);
        return true;
    }

    public ItemStack Clear()
    {
        var previous = GetState();
        var stack = Stack;
        Stack = ItemStack.Empty;
        IsFavorite = false;
        NotifyIfChanged(previous);
        return stack;
    }

    private void NotifyIfChanged(InventorySlotState previous)
    {
        if (previous != GetState())
        {
            _changed?.Invoke();
        }
    }
}
