namespace Game.Core.WorldEvents;

public readonly record struct WorldEventModifiedValues(
    float SpawnWeight,
    float SkyLight,
    float AmbientLight,
    float WeatherIntensity,
    float LootQuantity,
    float RareLootChance,
    float PresentationIntensity,
    string? ParticleSpriteId,
    string? ColorGradeId,
    string? SoundscapeId);

public static class WorldEventModifierApplier
{
    public static WorldEventModifiedValues Apply(
        in WorldEventModifierSet modifiers,
        float baseSpawnWeight,
        float baseSkyLight,
        float baseAmbientLight,
        float baseWeatherIntensity,
        float baseLootQuantity,
        float baseRareLootChance)
    {
        return new WorldEventModifiedValues(
            Math.Max(0f, baseSpawnWeight * modifiers.SpawnWeightMultiplier),
            Math.Clamp(baseSkyLight * modifiers.SkyLightMultiplier, 0f, 4f),
            Math.Clamp(baseAmbientLight + modifiers.AmbientLightAdd, 0f, 1f),
            Math.Clamp(baseWeatherIntensity * modifiers.WeatherIntensityMultiplier, 0f, 1f),
            Math.Max(0f, baseLootQuantity * modifiers.LootQuantityMultiplier),
            Math.Clamp(baseRareLootChance * modifiers.RareLootChanceMultiplier, 0f, 1f),
            modifiers.PresentationIntensity,
            modifiers.ParticleSpriteId,
            modifiers.ColorGradeId,
            modifiers.SoundscapeId);
    }
}
