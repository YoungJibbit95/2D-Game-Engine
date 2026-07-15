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
    int FarmPlotCount)
{
    public EquipmentLoadout EquipmentLoadout { get; init; } = new();

    public CharacterAppearance CharacterAppearance { get; init; } = new();

    public IReadOnlyList<PlayerLoadWarning> PlayerWarnings { get; init; } = Array.Empty<PlayerLoadWarning>();

    public WorldTime WorldTime { get; init; } = new();

    public bool SimulationStateLoaded { get; init; }

    public SessionRandomRegistry RandomStreams { get; init; } = new(0);

    public bool RandomStateLoaded { get; init; }

    public RandomStateLoadSource RandomStateSource { get; init; } = RandomStateLoadSource.LegacyFallback;

    public string? RandomStateWarning { get; init; }

    public WorldEventRuntimeStateSnapshot? WorldEventState { get; init; }

    public bool WorldEventStateLoaded { get; init; }

    public WorldEventStateLoadSource WorldEventStateSource { get; init; } =
        WorldEventStateLoadSource.LegacyFallback;

    public string? WorldEventStateWarning { get; init; }
}
