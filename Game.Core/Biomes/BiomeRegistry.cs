using Game.Core.Data;

namespace Game.Core.Biomes;

public sealed class BiomeRegistry
{
    private readonly Dictionary<string, BiomeDefinition> _byId;

    private BiomeRegistry(IEnumerable<BiomeDefinition> definitions)
    {
        _byId = new Dictionary<string, BiomeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<BiomeDefinition> Definitions => _byId.Values;

    public static BiomeRegistry Create(IEnumerable<BiomeDefinition> definitions)
    {
        return new BiomeRegistry(definitions);
    }

    public BiomeDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_byId.TryGetValue(id, out var biome))
        {
            throw new KeyNotFoundException($"Biome '{id}' was not registered.");
        }

        return biome;
    }

    public bool TryGetById(string id, out BiomeDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(BiomeDefinition definition)
    {
        Validate(definition);
        if (!_byId.TryAdd(definition.Id, definition))
        {
            throw new RegistryValidationException($"Duplicate biome id '{definition.Id}'.");
        }
    }

    private static void Validate(BiomeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.SurfaceTile, nameof(definition.SurfaceTile));
        RequireText(definition.UndergroundTile, nameof(definition.UndergroundTile));
        if (!string.IsNullOrWhiteSpace(definition.TreeType))
        {
            RequireText(definition.TreeMaterial.TrunkTile, nameof(definition.TreeMaterial.TrunkTile));
            RequireText(definition.TreeMaterial.CanopyTile, nameof(definition.TreeMaterial.CanopyTile));
        }

        if (definition.SelectionWeight <= 0)
        {
            throw new RegistryValidationException($"Biome '{definition.Id}' must have a positive selection weight.");
        }

        ValidateUnitRange(definition.Climate.Temperature, definition.Id, "temperature");
        ValidateUnitRange(definition.Climate.Humidity, definition.Id, "humidity");
        ValidatePositive(definition.Terrain.ElevationMultiplier, definition.Id, "elevation multiplier");
        ValidatePositive(definition.Terrain.SoilDepthMultiplier, definition.Id, "soil depth multiplier");
        ValidatePositive(definition.Terrain.CaveDensityMultiplier, definition.Id, "cave density multiplier");
        ValidatePositive(definition.Terrain.FeatureDensityMultiplier, definition.Id, "feature density multiplier");
        ValidatePositive(definition.Ambient.BaseLight, definition.Id, "base light");
        ValidatePositive(definition.Ambient.BaseVisibility, definition.Id, "base visibility");
        RequireText(definition.Lighting.ColorGradeId, nameof(definition.Lighting.ColorGradeId));
        ValidateNonNegative(definition.Lighting.SkyLightMultiplier, definition.Id, "sky light multiplier");
        ValidateNonNegative(
            definition.Lighting.EmissiveLightMultiplier,
            definition.Id,
            "emissive light multiplier");
        ValidateUnitRange(definition.Lighting.FogDensity, definition.Id, "fog density");
        ValidateNonNegative(definition.Spawning.DensityMultiplier, definition.Id, "spawn density multiplier");
        ValidateNonNegative(definition.Resources.OreDensityMultiplier, definition.Id, "ore density multiplier");
        ValidateNonNegative(
            definition.Resources.VegetationDensityMultiplier,
            definition.Id,
            "vegetation density multiplier");
        ValidateNonNegative(
            definition.Resources.ForageDensityMultiplier,
            definition.Id,
            "forage density multiplier");
        ValidateDistinctTextValues(definition.Spawning.HabitatTags, definition.Id, "habitat tag");
        ValidateDistinctTextValues(definition.Resources.ResourceTableIds, definition.Id, "resource table id");
        ValidateOptionalText(definition.Presentation.BackgroundSpriteId, definition.Id, "background sprite id");
        ValidateOptionalText(definition.Presentation.AmbientParticleSpriteId, definition.Id, "ambient particle sprite id");
        ValidateOptionalText(definition.Presentation.AmbientCritterSpriteId, definition.Id, "ambient critter sprite id");
        ValidateOptionalText(definition.Presentation.BiomeIconSpriteId, definition.Id, "biome icon sprite id");
        ValidateOptionalText(definition.Presentation.EliteSpriteId, definition.Id, "elite sprite id");
        ValidateUnitRange(definition.Presentation.AmbientParticleDensity, definition.Id, "ambient particle density");
        ValidateUnitRange(definition.Presentation.CaveReverb, definition.Id, "cave reverb");
        ValidateUnitRange(
            definition.Presentation.SurfaceReflectionStrength,
            definition.Id,
            "surface reflection strength");
        ValidateNonNegative(definition.Presentation.WindResponse, definition.Id, "presentation wind response");

        var subBiomeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var subBiome in definition.SubBiomes)
        {
            RequireText(subBiome.Id, nameof(subBiome.Id));
            RequireText(subBiome.DisplayName, nameof(subBiome.DisplayName));
            if (!subBiomeIds.Add(subBiome.Id))
            {
                throw new RegistryValidationException(
                    $"Biome '{definition.Id}' contains duplicate sub-biome id '{subBiome.Id}'.");
            }

            if (subBiome.SelectionWeight <= 0)
            {
                throw new RegistryValidationException(
                    $"Sub-biome '{definition.Id}:{subBiome.Id}' must have a positive selection weight.");
            }

            ValidatePositive(
                subBiome.ElevationMultiplier,
                definition.Id,
                $"sub-biome '{subBiome.Id}' elevation multiplier");
            ValidatePositive(
                subBiome.CaveDensityMultiplier,
                definition.Id,
                $"sub-biome '{subBiome.Id}' cave density multiplier");
        }

        var weatherWeight = (long)definition.Weather.ClearWeight + definition.Weather.RainWeight +
            definition.Weather.StormWeight + definition.Weather.FogWeight;
        if (definition.Weather.ClearWeight < 0 || definition.Weather.RainWeight < 0 ||
            definition.Weather.StormWeight < 0 || definition.Weather.FogWeight < 0 || weatherWeight <= 0)
        {
            throw new RegistryValidationException(
                $"Biome '{definition.Id}' weather weights must be non-negative with a positive total.");
        }

        if (definition.Weather.MinDurationTicks <= 0 ||
            definition.Weather.MaxDurationTicks < definition.Weather.MinDurationTicks ||
            definition.Weather.TransitionDurationTicks < 0)
        {
            throw new RegistryValidationException($"Biome '{definition.Id}' weather duration range is invalid.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Biome definition field '{name}' is required.");
        }
    }

    private static void ValidateOptionalText(string? value, string biomeId, string name)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Biome '{biomeId}' {name} must be null or non-empty.");
        }
    }

    private static void ValidateUnitRange(float value, string biomeId, string name)
    {
        if (!float.IsFinite(value) || value is < 0f or > 1f)
        {
            throw new RegistryValidationException($"Biome '{biomeId}' {name} must be between 0 and 1.");
        }
    }

    private static void ValidatePositive(float value, string biomeId, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new RegistryValidationException($"Biome '{biomeId}' {name} must be finite and positive.");
        }
    }

    private static void ValidateNonNegative(float value, string biomeId, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new RegistryValidationException($"Biome '{biomeId}' {name} must be finite and non-negative.");
        }
    }

    private static void ValidateDistinctTextValues(
        IReadOnlyList<string> values,
        string biomeId,
        string name)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
            {
                throw new RegistryValidationException(
                    $"Biome '{biomeId}' contains an invalid or duplicate {name} '{value}'.");
            }
        }
    }
}
