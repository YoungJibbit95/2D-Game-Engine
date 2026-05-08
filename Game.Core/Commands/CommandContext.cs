using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Time;
using System.Numerics;
using InventoryModel = Game.Core.Inventory.Inventory;

namespace Game.Core.Commands;

public sealed class CommandContext
{
    public GameContentDatabase? Content { get; init; }

    public InventoryModel? PlayerInventory { get; init; }

    public WorldTime? WorldTime { get; init; }

    public EntityManager? EntityManager { get; init; }

    public EntityFactory? EntityFactory { get; init; }

    public Vector2? PlayerPosition { get; init; }

    public GameEventBus? Events { get; init; }
}
