using Game.Core.Entities;
using Game.Core.Inventory;
using Game.Core.World.TileEntities;

namespace Game.Core.Saving;

public sealed record GameSaveRequest(
    World.World World,
    PlayerEntity Player,
    PlayerInventory Inventory,
    EntityManager Entities)
{
    public TileEntityManager? TileEntities { get; init; }
}
