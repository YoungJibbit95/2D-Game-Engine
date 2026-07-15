namespace Game.Core.World.Generation;

using Game.Core.WorldEvents;

public sealed record RegionalGenerationProfile
{
    public required string Id { get; init; }

    public int RegionWidthTiles { get; init; } = 256;

    public int BiomeSpanRegions { get; init; } = 3;

    public int WorldHeightTiles { get; init; } = 160;

    public int SurfaceBaseY { get; init; } = 58;

    public int CaveRegionAttempts { get; init; } = 3;

    public int CaveMinDepth { get; init; } = 20;

    public int CaveMaxDepth { get; init; } = 140;

    public int CaveMinRadiusX { get; init; } = 8;

    public int CaveMaxRadiusX { get; init; } = 28;

    public int CaveMinRadiusY { get; init; } = 4;

    public int CaveMaxRadiusY { get; init; } = 12;

    public IReadOnlyList<RegionalFeatureDefinition> Features { get; init; } =
        Array.Empty<RegionalFeatureDefinition>();

    public IReadOnlyList<RegionalBiomeLayerDefinition> BiomeLayers { get; init; } =
        Array.Empty<RegionalBiomeLayerDefinition>();

    public IReadOnlyList<WorldEventDefinition> WorldEvents { get; init; } =
        Array.Empty<WorldEventDefinition>();
}

public sealed record RegionalBiomeLayerDefinition
{
    public required string Id { get; init; }

    public int MinTileY { get; init; }

    public int MaxTileYInclusive { get; init; } = int.MaxValue;

    public bool RequiresCave { get; init; }

    public int Priority { get; init; }

    public IReadOnlyList<string> BiomeIds { get; init; } = Array.Empty<string>();
}

public sealed record RegionalFeatureDefinition
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public float ChancePerRegion { get; init; } = 0.5f;

    public int MinCount { get; init; } = 1;

    public int MaxCount { get; init; } = 1;

    public int MinTileY { get; init; }

    public int MaxTileY { get; init; } = int.MaxValue;

    public int MinSpacingTiles { get; init; } = 8;

    public IReadOnlyList<string> AllowedBiomeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedSubBiomeIds { get; init; } = Array.Empty<string>();
}

public sealed record StructurePlanDefinition
{
    public required string Id { get; init; }

    public required string TemplateId { get; init; }

    public required string Placement { get; init; }

    public float ChancePerRegion { get; init; } = 0.25f;

    public int MinSpacingRegions { get; init; } = 1;

    public int WidthTiles { get; init; } = 1;

    public int HeightTiles { get; init; } = 1;

    public int MinTileY { get; init; }

    public int MaxTileY { get; init; } = int.MaxValue;

    public IReadOnlyList<string> AllowedBiomeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedSubBiomeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedLayerIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Rows { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Legend { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public string TransparentSymbol { get; init; } = ".";
}
