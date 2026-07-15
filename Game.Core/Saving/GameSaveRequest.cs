using Game.Core.Characters;
using Game.Core.Entities;
using Game.Core.Equipment;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.World.TileEntities;
using Game.Core.Time;
using Game.Core.Randomness;
using Game.Core.WorldEvents;

namespace Game.Core.Saving;

public sealed record GameSaveRequest(
    World.World World,
    PlayerEntity Player,
    PlayerInventory Inventory,
    EntityManager Entities)
{
    public TileEntityManager? TileEntities { get; init; }

    public FarmPlotManager? FarmPlots { get; init; }

    public EquipmentLoadout? EquipmentLoadout { get; init; }

    public CharacterAppearance? CharacterAppearance { get; init; }

    public WorldTime? WorldTime { get; init; }

    public SessionRandomRegistry? RandomStreams { get; init; }

    public WorldEventRuntimeStateSnapshot? WorldEventState { get; init; }
}
