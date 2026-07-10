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
    IReadOnlyDictionary<ushort, int> TileCounts)
{
    public int UndergroundAirTileCount { get; init; }

    public int CavernTileCount { get; init; }

    public int CavernRegionCount { get; init; }

    public int LargestCavernTileCount { get; init; }

    public int LiquidBodyCount { get; init; }

    public int LargestLiquidBodyTileCount { get; init; }

    public int SurfaceLiquidTileCount { get; init; }

    public int CaveLiquidTileCount { get; init; }

    public int WallTileCount { get; init; }

    public int ExposedWallTileCount { get; init; }

    public IReadOnlyDictionary<ushort, int> WallCounts { get; init; } = new Dictionary<ushort, int>();
}
