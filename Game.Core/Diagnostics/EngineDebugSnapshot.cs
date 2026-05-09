namespace Game.Core.Diagnostics;

public sealed record EngineDebugSnapshot(
    string WorldName,
    int Seed,
    int WidthTiles,
    int HeightTiles,
    int LoadedChunkCount,
    int DirtyChunkCount,
    int MeshRebuildChunkCount,
    int LightUpdateChunkCount,
    int EntityCount,
    int ActiveEntityCount,
    int LiquidTileCount,
    int SolidTileCount,
    int AirTileCount,
    int MinSurfaceY,
    int MaxSurfaceY,
    float AverageSurfaceY,
    int Day,
    double NormalizedTimeOfDay,
    bool IsNight,
    IReadOnlyDictionary<string, int> EntityCountsByType);
