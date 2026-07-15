namespace Game.Core.WorldEvents;

public sealed record WorldEventDefinition
{
    public required string Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public float ChancePerWindow { get; init; } = 0.1f;

    public int MinDurationTicks { get; init; } = 600;

    public int MaxDurationTicks { get; init; } = 1_800;

    public float Intensity { get; init; } = 1f;

    public int CooldownTicks { get; init; }

    public IReadOnlyList<string> AllowedBiomeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedSubBiomeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedWeatherIds { get; init; } = Array.Empty<string>();

    public bool RequiresNight { get; init; }

    public bool RequiresUnderground { get; init; }

    public float MinimumWeatherIntensity { get; init; }

    public IReadOnlyList<WorldEventPlayerActionKind> PlayerActionTriggers { get; init; } =
        Array.Empty<WorldEventPlayerActionKind>();

    public float PlayerActionTriggerChance { get; init; } = 1f;

    public WorldEventModifierSet Modifiers { get; init; } = WorldEventModifierSet.Identity;

    public IReadOnlyList<WorldEventPhaseDefinition> Phases { get; init; } =
        Array.Empty<WorldEventPhaseDefinition>();

    public static void Validate(WorldEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id) ||
            definition.ChancePerWindow is < 0f or > 1f ||
            definition.MinDurationTicks <= 0 ||
            definition.MaxDurationTicks < definition.MinDurationTicks ||
            definition.CooldownTicks < 0 ||
            !float.IsFinite(definition.Intensity) ||
            definition.Intensity < 0f ||
            !float.IsFinite(definition.MinimumWeatherIntensity) ||
            definition.MinimumWeatherIntensity is < 0f or > 1f ||
            !float.IsFinite(definition.PlayerActionTriggerChance) ||
            definition.PlayerActionTriggerChance is < 0f or > 1f ||
            definition.PlayerActionTriggers.Distinct().Count() != definition.PlayerActionTriggers.Count)
        {
            throw new InvalidDataException($"World event definition '{definition.Id}' is invalid.");
        }

        ValidateIds(definition.Id, definition.AllowedBiomeIds, "biome");
        ValidateIds(definition.Id, definition.AllowedSubBiomeIds, "sub-biome");
        ValidateIds(definition.Id, definition.AllowedWeatherIds, "weather");
        WorldEventModifierSet.Validate(definition.Modifiers, definition.Id);

        var phaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previousEnd = 0f;
        for (var index = 0; index < definition.Phases.Count; index++)
        {
            var phase = definition.Phases[index];
            if (string.IsNullOrWhiteSpace(phase.Id) || !phaseIds.Add(phase.Id) ||
                !float.IsFinite(phase.StartProgress) || !float.IsFinite(phase.EndProgress) ||
                phase.StartProgress < 0f || phase.EndProgress > 1f ||
                phase.EndProgress <= phase.StartProgress || phase.StartProgress < previousEnd)
            {
                throw new InvalidDataException(
                    $"World event '{definition.Id}' has an invalid phase at index {index}.");
            }

            WorldEventModifierSet.Validate(phase.Modifiers, $"{definition.Id}:{phase.Id}");
            previousEnd = phase.EndProgress;
        }
    }

    private static void ValidateIds(string eventId, IReadOnlyList<string> values, string kind)
    {
        if (values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
        {
            throw new InvalidDataException($"World event '{eventId}' has invalid {kind} ids.");
        }
    }
}

public sealed record WorldEventPhaseDefinition
{
    public required string Id { get; init; }

    public float StartProgress { get; init; }

    public float EndProgress { get; init; } = 1f;

    public WorldEventModifierSet Modifiers { get; init; } = WorldEventModifierSet.Identity;
}

public readonly record struct WorldEventModifierSet(
    float SpawnWeightMultiplier,
    float SkyLightMultiplier,
    float AmbientLightAdd,
    float WeatherIntensityMultiplier,
    float LootQuantityMultiplier,
    float RareLootChanceMultiplier,
    float PresentationIntensity,
    string? ParticleSpriteId,
    string? ColorGradeId,
    string? SoundscapeId)
{
    public static WorldEventModifierSet Identity { get; } = new(
        1f,
        1f,
        0f,
        1f,
        1f,
        1f,
        0f,
        null,
        null,
        null);

    public static WorldEventModifierSet Compose(
        in WorldEventModifierSet root,
        in WorldEventModifierSet phase,
        float intensity)
    {
        intensity = Math.Clamp(intensity, 0f, 1f);
        return new WorldEventModifierSet(
            Lerp(1f, root.SpawnWeightMultiplier * phase.SpawnWeightMultiplier, intensity),
            Lerp(1f, root.SkyLightMultiplier * phase.SkyLightMultiplier, intensity),
            (root.AmbientLightAdd + phase.AmbientLightAdd) * intensity,
            Lerp(1f, root.WeatherIntensityMultiplier * phase.WeatherIntensityMultiplier, intensity),
            Lerp(1f, root.LootQuantityMultiplier * phase.LootQuantityMultiplier, intensity),
            Lerp(1f, root.RareLootChanceMultiplier * phase.RareLootChanceMultiplier, intensity),
            Math.Clamp((root.PresentationIntensity + phase.PresentationIntensity) * intensity, 0f, 4f),
            phase.ParticleSpriteId ?? root.ParticleSpriteId,
            phase.ColorGradeId ?? root.ColorGradeId,
            phase.SoundscapeId ?? root.SoundscapeId);
    }

    public static void Validate(in WorldEventModifierSet modifiers, string ownerId)
    {
        if (!IsNonNegativeFinite(modifiers.SpawnWeightMultiplier) ||
            !IsNonNegativeFinite(modifiers.SkyLightMultiplier) ||
            !float.IsFinite(modifiers.AmbientLightAdd) ||
            modifiers.AmbientLightAdd is < -1f or > 1f ||
            !IsNonNegativeFinite(modifiers.WeatherIntensityMultiplier) ||
            !IsNonNegativeFinite(modifiers.LootQuantityMultiplier) ||
            !IsNonNegativeFinite(modifiers.RareLootChanceMultiplier) ||
            !IsNonNegativeFinite(modifiers.PresentationIntensity))
        {
            throw new InvalidDataException($"World event modifiers for '{ownerId}' are invalid.");
        }
    }

    private static bool IsNonNegativeFinite(float value)
    {
        return float.IsFinite(value) && value >= 0f;
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }
}

public readonly record struct WorldEventState(
    bool IsActive,
    string? EventId,
    long RegionIndex,
    long StartTick,
    long EndTickExclusive,
    float Progress,
    float Intensity)
{
    public static WorldEventState Inactive(long regionIndex, long tick)
    {
        return new WorldEventState(false, null, regionIndex, tick, tick, 0f, 0f);
    }
}

public readonly record struct WorldEventSystemSnapshot(
    int FormatVersion,
    long LastAdvancedTick,
    long RegionIndex,
    string BiomeId,
    string? SubBiomeId,
    WorldEventState State)
{
    public const int CurrentFormatVersion = 1;
}
