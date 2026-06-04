using Game.Core.Inventory;

namespace Game.Core.Startup;

public sealed record StarterInventoryAppliedItem(
    ItemStack Stack,
    StarterInventoryTarget Target,
    int? Slot);
