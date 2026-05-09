namespace Game.Core.World.Generation;

public sealed record WorldGenerationAnalysis(
    int WidthTiles,
    int HeightTiles,
    int AirTileCount,
    int SolidTileCount,
    int LiquidTileCount,
    int NaturalTileCount,
    int MinSurfaceY,
    int MaxSurfaceY,
    float AverageSurfaceY,
    IReadOnlyDictionary<ushort, int> TileCounts);
