using Game.Core.Inventory;

namespace Game.Core.Startup;

public sealed record StarterInventoryFailedItem(
    ItemStack Stack,
    StarterInventoryTarget Target,
    int? Slot,
    string Reason);
