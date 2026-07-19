using Game.Core.Data;

namespace Game.Core.Spawning;

public sealed class EncounterDefinitionRegistry
{
    private readonly Dictionary<string, EncounterDefinition> _byId;
    private readonly List<EncounterDefinition> _definitions;

    private EncounterDefinitionRegistry(IEnumerable<EncounterDefinition> definitions)
    {
        _byId = new Dictionary<string, EncounterDefinition>(StringComparer.OrdinalIgnoreCase);
        _definitions = new List<EncounterDefinition>();

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyList<EncounterDefinition> Definitions => _definitions;

    public static EncounterDefinitionRegistry Create(IEnumerable<EncounterDefinition> definitions)
    {
        return new EncounterDefinitionRegistry(definitions);
    }

    public bool TryGetById(string id, out EncounterDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(EncounterDefinition definition)
    {
        Validate(definition);
        if (!_byId.TryAdd(definition.Id, definition))
        {
            throw new RegistryValidationException($"Duplicate encounter id '{definition.Id}'.");
        }

        _definitions.Add(definition);
    }

    private static void Validate(EncounterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new RegistryValidationException("Encounter id is required.");
        }

        if (!float.IsFinite(definition.Weight) || definition.Weight <= 0)
        {
            throw new RegistryValidationException($"Encounter '{definition.Id}' must have positive weight.");
        }

        if (definition.MinDistanceTiles < 0 || definition.MaxDistanceTiles < definition.MinDistanceTiles)
        {
            throw new RegistryValidationException($"Encounter '{definition.Id}' has an invalid distance range.");
        }

        if (!float.IsFinite(definition.CooldownSeconds) || definition.CooldownSeconds < 0)
        {
            throw new RegistryValidationException($"Encounter '{definition.Id}' has an invalid cooldown.");
        }

        if (definition.MaxActiveGlobal <= 0 ||
            definition.PopulationRegionSizeTiles <= 0 ||
            definition.MaxActiveInRegion <= 0 ||
            definition.MaxActiveInRegion > definition.MaxActiveGlobal)
        {
            throw new RegistryValidationException($"Encounter '{definition.Id}' has invalid population caps.");
        }

        if (definition.Roles.Count == 0 ||
            definition.MinRoleSelections <= 0 ||
            definition.MaxRoleSelections < definition.MinRoleSelections ||
            definition.MaxRoleSelections > definition.Roles.Count)
        {
            throw new RegistryValidationException($"Encounter '{definition.Id}' has invalid role selection counts.");
        }

        ValidateIds(definition.Id, "biome", definition.BiomeIds);
        ValidateIds(definition.Id, "vertical layer", definition.VerticalLayerIds);

        var roleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in definition.Roles)
        {
            if (string.IsNullOrWhiteSpace(role.Id) || string.IsNullOrWhiteSpace(role.SpawnRuleId))
            {
                throw new RegistryValidationException($"Encounter '{definition.Id}' has a role without an id or spawn rule.");
            }

            if (!roleIds.Add(role.Id))
            {
                throw new RegistryValidationException($"Encounter '{definition.Id}' has duplicate role id '{role.Id}'.");
            }

            if (!float.IsFinite(role.Weight) || role.Weight <= 0)
            {
                throw new RegistryValidationException($"Encounter '{definition.Id}' role '{role.Id}' must have positive weight.");
            }

            if (role.MinCount <= 0 || role.MaxCount < role.MinCount)
            {
                throw new RegistryValidationException($"Encounter '{definition.Id}' role '{role.Id}' has invalid counts.");
            }
        }
    }

    private static void ValidateIds(string encounterId, string category, IReadOnlyList<string> ids)
    {
        if (ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Count)
        {
            throw new RegistryValidationException($"Encounter '{encounterId}' has invalid or duplicate {category} ids.");
        }
    }
}
