namespace Game.Core.Spawning;

public sealed record EncounterDefinition
{
    public required string Id { get; init; }

    public IReadOnlyList<string> BiomeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> VerticalLayerIds { get; init; } = Array.Empty<string>();

    public SpawnTimeCondition Time { get; init; }

    public float Weight { get; init; } = 1f;

    public int MinDistanceTiles { get; init; } = 18;

    public int MaxDistanceTiles { get; init; } = 46;

    public float CooldownSeconds { get; init; }

    public int MaxActiveGlobal { get; init; } = 12;

    public int PopulationRegionSizeTiles { get; init; } = 96;

    public int MaxActiveInRegion { get; init; } = 6;

    public int MinRoleSelections { get; init; } = 1;

    public int MaxRoleSelections { get; init; } = 1;

    public IReadOnlyList<EncounterRoleDefinition> Roles { get; init; } =
        Array.Empty<EncounterRoleDefinition>();
}

public sealed record EncounterRoleDefinition
{
    public required string Id { get; init; }

    public required string SpawnRuleId { get; init; }

    public float Weight { get; init; } = 1f;

    public int MinCount { get; init; } = 1;

    public int MaxCount { get; init; } = 1;
}
