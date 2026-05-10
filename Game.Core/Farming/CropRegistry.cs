using Game.Core.Data;

namespace Game.Core.Farming;

public sealed class CropRegistry
{
    private readonly Dictionary<string, CropDefinition> _byId;
    private readonly Dictionary<string, CropDefinition> _bySeedItemId;

    private CropRegistry(IEnumerable<CropDefinition> definitions)
    {
        _byId = new Dictionary<string, CropDefinition>(StringComparer.OrdinalIgnoreCase);
        _bySeedItemId = new Dictionary<string, CropDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<CropDefinition> Definitions => _byId.Values;

    public static CropRegistry Create(IEnumerable<CropDefinition> definitions)
    {
        return new CropRegistry(definitions);
    }

    public bool TryGetById(string id, out CropDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    public CropDefinition GetById(string id)
    {
        return TryGetById(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Crop definition '{id}' was not registered.");
    }

    public bool TryGetBySeedItemId(string seedItemId, out CropDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedItemId);
        return _bySeedItemId.TryGetValue(seedItemId, out definition!);
    }

    private void AddValidated(CropDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate crop id '{definition.Id}'.");
        }

        if (_bySeedItemId.ContainsKey(definition.SeedItemId))
        {
            throw new RegistryValidationException($"Duplicate crop seed item id '{definition.SeedItemId}'.");
        }

        _byId.Add(definition.Id, definition);
        _bySeedItemId.Add(definition.SeedItemId, definition);
    }

    private static void Validate(CropDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.TexturePath, nameof(definition.TexturePath));
        RequireText(definition.SeedItemId, nameof(definition.SeedItemId));
        RequireText(definition.HarvestItemId, nameof(definition.HarvestItemId));

        if (definition.BaseYield <= 0)
        {
            throw new RegistryValidationException($"Crop '{definition.Id}' must have a positive base yield.");
        }

        if (definition.ExtraYieldChancePercent is < 0 or > 100)
        {
            throw new RegistryValidationException($"Crop '{definition.Id}' has invalid extra yield chance.");
        }

        if (definition.RegrowDays < 0)
        {
            throw new RegistryValidationException($"Crop '{definition.Id}' has negative regrow days.");
        }

        if (definition.GrowthStageDays.Count == 0 || definition.GrowthStageDays.Any(day => day <= 0))
        {
            throw new RegistryValidationException($"Crop '{definition.Id}' must define positive growth stage days.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Crop definition field '{name}' is required.");
        }
    }
}
