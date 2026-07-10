using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Characters;
using Game.Core.Equipment;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Inventory;
using Game.Core.Projects;
using Game.Core.Startup;
using Game.Core.Saving;
using Game.Core.Time;
using Game.Core.World.Generation;
using Game.Core.World.TileEntities;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Sessions;

public sealed record LoadedGameSession(
    GameContentDatabase Content,
    GameWorld World,
    PlayerEntity Player,
    PlayerInventory Inventory,
    EntityManager Entities,
    GameEventBus Events,
    WorldTime WorldTime,
    WorldGenerationProfile? WorldGenerationProfile = null,
    string? WorldSaveDirectory = null,
    TileEntityManager? TileEntities = null,
    FarmPlotManager? FarmPlots = null,
    GameProjectManifest? Manifest = null,
    GameProjectPaths? ProjectPaths = null,
    GameStartupDefinition? Startup = null,
    StarterInventoryResult? StartupInventory = null,
    bool LoadedFromSave = false,
    EquipmentLoadout? EquipmentLoadout = null,
    CharacterAppearance? CharacterAppearance = null,
    IReadOnlyList<PlayerLoadWarning>? PlayerLoadWarnings = null);
