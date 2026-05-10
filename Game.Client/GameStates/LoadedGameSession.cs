using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Inventory;
using Game.Core.Time;
using Game.Core.World.Generation;
using Game.Core.World.TileEntities;
using GameWorld = Game.Core.World.World;

namespace Game.Client.GameStates;

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
    TileEntityManager? TileEntities = null);
