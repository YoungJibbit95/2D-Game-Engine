using Game.Core.Saving;
using Game.Core.Settings;

namespace Game.Core.Sessions;

public sealed record GameSessionBootstrapRequest(
    string ProjectRootOrContentRoot,
    string SaveDirectory,
    int Seed,
    string WorldName,
    GameSettings Settings)
{
    public bool LoadExistingSave { get; init; } = true;

    public WorldChunkStorageMode ChunkStorageMode { get; init; } = WorldChunkStorageMode.RegionFiles;
}
