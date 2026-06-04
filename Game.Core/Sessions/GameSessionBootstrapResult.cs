using Game.Core.Projects;
using Game.Core.Startup;
using Game.Core.World.Generation;

namespace Game.Core.Sessions;

public sealed record GameSessionBootstrapResult(
    LoadedGameSession Session,
    GameProjectContentLoadResult ProjectContent,
    WorldGenerationProfile WorldGenerationProfile,
    GameStartupDefinition? Startup,
    StarterInventoryResult? StarterInventory,
    bool LoadedExistingSave,
    int SpawnAreaChunksLoadedFromSave,
    int SpawnAreaChunksGenerated);
