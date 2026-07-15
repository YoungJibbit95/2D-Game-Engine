using Game.Core.Biomes;

namespace Game.Core.World.Generation;

public sealed record WorldRegionPlan(
    long RegionIndex,
    long StartTileX,
    long EndTileXInclusive,
    BiomeDefinition Biome,
    SubBiomeDefinition? SubBiome,
    IReadOnlyList<CaveRegionPlan> Caves,
    IReadOnlyList<PlannedWorldFeature> Features,
    IReadOnlyList<PlannedStructure> Structures)
{
    public bool ContainsTileX(long tileX)
    {
        return tileX >= StartTileX && tileX <= EndTileXInclusive;
    }
}

public sealed record CaveRegionPlan(
    string Id,
    string ProfileId,
    long CenterTileX,
    int CenterTileY,
    int RadiusX,
    int RadiusY,
    float Humidity,
    float AmbientLight)
{
    public bool Contains(long tileX, int tileY)
    {
        var normalizedX = ((long)tileX - CenterTileX) / (double)RadiusX;
        var normalizedY = ((long)tileY - CenterTileY) / (double)RadiusY;
        return normalizedX * normalizedX + normalizedY * normalizedY <= 1d;
    }
}

public sealed record PlannedWorldFeature(
    string DefinitionId,
    string Kind,
    long TileX,
    int TileY,
    string BiomeId,
    string? SubBiomeId);

public sealed record PlannedStructure(
    string DefinitionId,
    string TemplateId,
    string Placement,
    long TileX,
    int TileY,
    int WidthTiles,
    int HeightTiles)
{
    public IReadOnlyList<string> Rows { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Legend { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public char TransparentSymbol { get; init; } = '.';

    public bool HasMaterializedTemplate => Rows.Count > 0;
}

public readonly record struct WorldBiomeResolution(
    long RegionIndex,
    long TileX,
    int TileY,
    string SurfaceBiomeId,
    BiomeDefinition Biome,
    SubBiomeDefinition? SubBiome,
    string LayerId,
    bool IsCave,
    string? CaveId)
{
    public BiomeRuntimeProfileSnapshot ToRuntimeProfileSnapshot()
    {
        return new BiomeRuntimeProfileSnapshot(
            Biome.Id,
            SubBiome?.Id,
            LayerId,
            IsCave,
            CaveId,
            Biome.Ambient,
            Biome.Lighting,
            Biome.Spawning,
            Biome.Resources,
            Biome.Presentation);
    }
}
