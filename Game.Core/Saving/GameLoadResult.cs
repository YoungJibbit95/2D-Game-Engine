using Game.Core.Entities;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.World.TileEntities;

namespace Game.Core.Saving;

public sealed record GameLoadResult(
    string SaveDirectory,
    DateTimeOffset LoadedAtUtc,
    World.World World,
    PlayerEntity Player,
    PlayerInventory Inventory,
    EntityManager Entities,
    TileEntityManager TileEntities,
    bool PlayerLoaded,
    bool RuntimeEntitiesLoaded,
    bool TileEntitiesLoaded,
    bool FarmPlotsLoaded,
    int RuntimeEntityCount,
    int TileEntityCount,
    FarmPlotManager FarmPlots,
    int FarmPlotCount);
