using System.Numerics;
using Game.Core.Inventory;

namespace Game.Core.Events;

public sealed record LootDroppedEvent(
    int VictimEntityId,
    ItemStack Stack,
    Vector2 WorldPosition) : IGameEvent;
