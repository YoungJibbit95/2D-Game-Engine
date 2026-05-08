using Game.Core.Inventory;

namespace Game.Core.Events;

public sealed record ItemPickedUpEvent(int EntityId, ItemStack Stack) : IGameEvent;
