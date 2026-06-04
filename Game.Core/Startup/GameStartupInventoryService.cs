using Game.Core.Inventory;
using Game.Core.Items;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Startup;

public sealed class GameStartupInventoryService
{
    public StarterInventoryResult BuildPlayerInventory(IItemDefinitionProvider items, GameStartupDefinition? startup)
    {
        ArgumentNullException.ThrowIfNull(items);

        var inventory = new PlayerInventory(items);
        if (startup is null)
        {
            return new StarterInventoryResult(
                inventory,
                Array.Empty<StarterInventoryAppliedItem>(),
                Array.Empty<StarterInventoryFailedItem>());
        }

        inventory.SelectHotbarSlot(startup.SelectedHotbarSlot);
        return Apply(inventory, startup);
    }

    public StarterInventoryResult Apply(PlayerInventory inventory, GameStartupDefinition startup)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(startup);

        var applied = new List<StarterInventoryAppliedItem>();
        var failed = new List<StarterInventoryFailedItem>();

        foreach (var item in startup.StarterItems.OrderBy(item => item.SortOrder).ThenBy(item => item.ItemId, StringComparer.OrdinalIgnoreCase))
        {
            var stack = new ItemStack(item.ItemId, item.Count);
            if (!TryApplyItem(inventory, item, stack, applied, out var reason))
            {
                failed.Add(new StarterInventoryFailedItem(stack, item.Target, item.Slot, reason));
            }
        }

        return new StarterInventoryResult(inventory, applied, failed);
    }

    private static bool TryApplyItem(
        PlayerInventory inventory,
        StarterItemDefinition item,
        ItemStack stack,
        List<StarterInventoryAppliedItem> applied,
        out string failureReason)
    {
        if (item.Target != StarterInventoryTarget.Auto && item.Slot.HasValue)
        {
            var target = item.Target == StarterInventoryTarget.Hotbar ? inventory.Hotbar : inventory.Main;
            if (TryPlaceExact(inventory, target, item.Slot.Value, stack, out failureReason))
            {
                applied.Add(new StarterInventoryAppliedItem(stack, item.Target, item.Slot.Value));
                return true;
            }

            return false;
        }

        if (item.Target == StarterInventoryTarget.Hotbar && inventory.Hotbar.AddItem(stack))
        {
            applied.Add(new StarterInventoryAppliedItem(stack, item.Target, null));
            failureReason = string.Empty;
            return true;
        }

        if (item.Target == StarterInventoryTarget.Main && inventory.Main.AddItem(stack))
        {
            applied.Add(new StarterInventoryAppliedItem(stack, item.Target, null));
            failureReason = string.Empty;
            return true;
        }

        if (inventory.AddItem(stack))
        {
            applied.Add(new StarterInventoryAppliedItem(stack, StarterInventoryTarget.Auto, null));
            failureReason = string.Empty;
            return true;
        }

        failureReason = "inventory_full";
        return false;
    }

    private static bool TryPlaceExact(
        PlayerInventory playerInventory,
        InventoryModel target,
        int slotIndex,
        ItemStack stack,
        out string failureReason)
    {
        if (slotIndex < 0 || slotIndex >= target.Slots.Count)
        {
            failureReason = "slot_out_of_range";
            return false;
        }

        var slot = target.Slots[slotIndex];
        if (!slot.CanAccept(stack))
        {
            failureReason = "slot_contains_different_item";
            return false;
        }

        var maxStack = playerInventory.ItemDefinitions.GetById(stack.ItemId).MaxStack;
        if (slot.IsEmpty)
        {
            if (stack.Count > maxStack)
            {
                failureReason = "stack_exceeds_slot_limit";
                return false;
            }

            slot.SetStack(stack);
            failureReason = string.Empty;
            return true;
        }

        if (slot.Stack.Count + stack.Count > maxStack)
        {
            failureReason = "stack_exceeds_slot_limit";
            return false;
        }

        slot.SetStack(slot.Stack.WithCount(slot.Stack.Count + stack.Count));
        failureReason = string.Empty;
        return true;
    }
}
