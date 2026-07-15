namespace Game.Core.Spawning;

public sealed record SpawnRuleDefinition
{
    public required string Id { get; init; }

    public required string EntityId { get; init; }

    public string? BiomeId { get; init; }

    public bool? RequiresNight { get; init; }

    public SpawnTimeCondition Time { get; init; }

    public IReadOnlyList<SpawnHabitat> Habitats { get; init; } = Array.Empty<SpawnHabitat>();

    public int? MinTileY { get; init; }

    public int? MaxTileY { get; init; }

    public float Chance { get; init; } = 1f;

    public float Weight { get; init; } = 1f;

    public float DayWeight { get; init; } = 1f;

    public float NightWeight { get; init; } = 1f;

    public Dictionary<string, float> BiomeWeights { get; init; } =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, float> VerticalLayerWeights { get; init; } =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, float> WeatherWeights { get; init; } =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, float> WorldEventWeights { get; init; } =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, float> HabitatWeights { get; init; } =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public int MaxActive { get; init; } = 5;

    public string? PopulationGroup { get; init; }

    public int? MaxActiveInGroup { get; init; }

    public int PopulationRegionSizeTiles { get; init; } = 64;

    public int? MaxActiveInRegion { get; init; }

    public int? MaxActiveInHabitat { get; init; }

    public int LocalPopulationRadiusTiles { get; init; } = 24;

    public int? MaxActiveInLocalArea { get; init; }

    public float CooldownSeconds { get; init; }
}
