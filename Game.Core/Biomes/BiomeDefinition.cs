namespace Game.Core.Biomes;

public sealed record BiomeDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string SurfaceTile { get; init; }

    public required string UndergroundTile { get; init; }

    public string? TreeType { get; init; }

    public BiomeTreeMaterialProfile TreeMaterial { get; init; } = new();

    public string? EnemySpawnTable { get; init; }

    public bool IsRegionalBiome { get; init; } = true;

    public int SelectionWeight { get; init; } = 1;

    public BiomeClimateProfile Climate { get; init; } = new();

    public BiomeTerrainProfile Terrain { get; init; } = new();

    public BiomeWeatherProfile Weather { get; init; } = new();

    public BiomeAmbientProfile Ambient { get; init; } = new();

    public BiomeLightingProfile Lighting { get; init; } = new();

    public BiomeSpawnProfile Spawning { get; init; } = new();

    public BiomeResourceProfile Resources { get; init; } = new();

    public BiomePresentationProfile Presentation { get; init; } = new();

    public IReadOnlyList<SubBiomeDefinition> SubBiomes { get; init; } = Array.Empty<SubBiomeDefinition>();
}

public sealed record BiomeTreeMaterialProfile
{
    public string TrunkTile { get; init; } = "wood";

    public string CanopyTile { get; init; } = "leaves";
}

public sealed record BiomeClimateProfile
{
    public float Temperature { get; init; } = 0.5f;

    public float Humidity { get; init; } = 0.5f;
}

public sealed record BiomeTerrainProfile
{
    public float ElevationMultiplier { get; init; } = 1f;

    public float SoilDepthMultiplier { get; init; } = 1f;

    public float CaveDensityMultiplier { get; init; } = 1f;

    public float FeatureDensityMultiplier { get; init; } = 1f;
}

public sealed record BiomeWeatherProfile
{
    public int ClearWeight { get; init; } = 60;

    public int RainWeight { get; init; } = 25;

    public int StormWeight { get; init; } = 5;

    public int FogWeight { get; init; } = 10;

    public int MinDurationTicks { get; init; } = 1_800;

    public int MaxDurationTicks { get; init; } = 5_400;

    public int TransitionDurationTicks { get; init; } = 300;
}

public sealed record BiomeAmbientProfile
{
    public string SurfaceSoundscapeId { get; init; } = "surface";

    public string CaveSoundscapeId { get; init; } = "cave";

    public float BaseLight { get; init; } = 1f;

    public float BaseVisibility { get; init; } = 1f;
}

public sealed record BiomeLightingProfile
{
    public string ColorGradeId { get; init; } = "neutral";

    public float SkyLightMultiplier { get; init; } = 1f;

    public float EmissiveLightMultiplier { get; init; } = 1f;

    public float FogDensity { get; init; }
}

public sealed record BiomeSpawnProfile
{
    public string? SurfaceDayTableId { get; init; }

    public string? SurfaceNightTableId { get; init; }

    public string? CaveTableId { get; init; }

    public float DensityMultiplier { get; init; } = 1f;

    public IReadOnlyList<string> HabitatTags { get; init; } = Array.Empty<string>();
}

public sealed record BiomeResourceProfile
{
    public float OreDensityMultiplier { get; init; } = 1f;

    public float VegetationDensityMultiplier { get; init; } = 1f;

    public float ForageDensityMultiplier { get; init; } = 1f;

    public IReadOnlyList<string> ResourceTableIds { get; init; } = Array.Empty<string>();
}

public sealed record BiomePresentationProfile
{
    public string? BackgroundSpriteId { get; init; }

    public string? AmbientParticleSpriteId { get; init; }

    public string? AmbientCritterSpriteId { get; init; }

    public string? BiomeIconSpriteId { get; init; }

    public string? EliteSpriteId { get; init; }

    public float AmbientParticleDensity { get; init; } = 0.25f;

    public float CaveReverb { get; init; }

    public float SurfaceReflectionStrength { get; init; } = 0.25f;

    public float WindResponse { get; init; } = 1f;
}

public sealed record SubBiomeDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public int SelectionWeight { get; init; } = 1;

    public float TemperatureOffset { get; init; }

    public float HumidityOffset { get; init; }

    public float ElevationMultiplier { get; init; } = 1f;

    public float CaveDensityMultiplier { get; init; } = 1f;

    public string? SurfaceFeatureSetId { get; init; }

    public string? CaveProfileId { get; init; }

    public string? SoundscapeId { get; init; }
}

public readonly record struct BiomeRuntimeProfileSnapshot(
    string BiomeId,
    string? SubBiomeId,
    string LayerId,
    bool IsCave,
    string? CaveId,
    BiomeAmbientProfile Ambient,
    BiomeLightingProfile Lighting,
    BiomeSpawnProfile Spawning,
    BiomeResourceProfile Resources,
    BiomePresentationProfile Presentation);
