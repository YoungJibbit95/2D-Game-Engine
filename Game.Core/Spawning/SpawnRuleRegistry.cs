using Game.Core.Data;

namespace Game.Core.Spawning;

public sealed class SpawnRuleRegistry
{
    private readonly Dictionary<string, SpawnRuleDefinition> _byId;
    private readonly List<SpawnRuleDefinition> _definitions;

    private SpawnRuleRegistry(IEnumerable<SpawnRuleDefinition> definitions)
    {
        _byId = new Dictionary<string, SpawnRuleDefinition>(StringComparer.OrdinalIgnoreCase);
        _definitions = new List<SpawnRuleDefinition>();

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyList<SpawnRuleDefinition> Definitions => _definitions;

    public static SpawnRuleRegistry Create(IEnumerable<SpawnRuleDefinition> definitions)
    {
        return new SpawnRuleRegistry(definitions);
    }

    public bool TryGetById(string id, out SpawnRuleDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(SpawnRuleDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate spawn rule id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
        _definitions.Add(definition);
    }

    private static void Validate(SpawnRuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new RegistryValidationException("Spawn rule id is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.EntityId))
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' must specify an entity id.");
        }

        if (!float.IsFinite(definition.Chance) || definition.Chance < 0 || definition.Chance > 1)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' chance must be in the range 0..1.");
        }

        if (definition.MaxActive <= 0)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' must allow at least one active entity.");
        }

        if (definition.MaxActiveInGroup is <= 0)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' must allow at least one active entity in its population group.");
        }

        if (definition.MaxActiveInRegion is <= 0 ||
            definition.MaxActiveInHabitat is <= 0 ||
            definition.MaxActiveInLocalArea is <= 0)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has a non-positive contextual cap.");
        }

        if ((definition.MaxActiveInRegion is not null ||
             definition.MaxActiveInHabitat is not null ||
             definition.MaxActiveInLocalArea is not null) &&
            string.IsNullOrWhiteSpace(definition.PopulationGroup))
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has a contextual cap without a population group.");
        }

        if (definition.PopulationRegionSizeTiles <= 0)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has a non-positive population region size.");
        }

        if (definition.LocalPopulationRadiusTiles <= 0)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has a non-positive local population radius.");
        }

        if (definition.MaxActiveInGroup is not null && string.IsNullOrWhiteSpace(definition.PopulationGroup))
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has a group cap without a population group.");
        }

        if (definition.CooldownSeconds < 0 || !float.IsFinite(definition.CooldownSeconds))
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has an invalid cooldown.");
        }

        ValidateWeight(definition.Id, "base", definition.Weight);
        ValidateWeight(definition.Id, "day", definition.DayWeight);
        ValidateWeight(definition.Id, "night", definition.NightWeight);
        ValidateWeights(definition.Id, "biome", definition.BiomeWeights);
        ValidateWeights(definition.Id, "vertical layer", definition.VerticalLayerWeights);
        ValidateWeights(definition.Id, "weather", definition.WeatherWeights);
        ValidateWeights(definition.Id, "world event", definition.WorldEventWeights);
        ValidateWeights(definition.Id, "habitat", definition.HabitatWeights);

        if (definition.MinTileY is < 0 || definition.MaxTileY is < 0)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has a negative depth boundary.");
        }

        if (definition.Habitats.Count != definition.Habitats.Distinct().Count())
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has duplicate habitats.");
        }

        if (definition.MinTileY is not null && definition.MaxTileY is not null && definition.MinTileY > definition.MaxTileY)
        {
            throw new RegistryValidationException($"Spawn rule '{definition.Id}' has an invalid vertical range.");
        }
    }

    private static void ValidateWeights(
        string ruleId,
        string category,
        Dictionary<string, float> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        foreach (var pair in weights)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new RegistryValidationException($"Spawn rule '{ruleId}' has an empty {category} weight key.");
            }

            ValidateWeight(ruleId, $"{category} '{pair.Key}'", pair.Value);
        }
    }

    private static void ValidateWeight(string ruleId, string name, float weight)
    {
        if (!float.IsFinite(weight) || weight < 0)
        {
            throw new RegistryValidationException($"Spawn rule '{ruleId}' has an invalid {name} weight.");
        }
    }
}
