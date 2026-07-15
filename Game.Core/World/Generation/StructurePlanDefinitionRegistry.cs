using Game.Core.Data;

namespace Game.Core.World.Generation;

public sealed class StructurePlanDefinitionRegistry
{
    private readonly Dictionary<string, StructurePlanDefinition> _byId;

    private StructurePlanDefinitionRegistry(IEnumerable<StructurePlanDefinition> definitions)
    {
        _byId = new Dictionary<string, StructurePlanDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (string.IsNullOrWhiteSpace(definition.Id) || !_byId.TryAdd(definition.Id, definition))
            {
                throw new RegistryValidationException(
                    $"Structure plan id '{definition.Id}' is missing or duplicated.");
            }
        }
    }

    public IReadOnlyCollection<StructurePlanDefinition> Definitions => _byId.Values;

    public static StructurePlanDefinitionRegistry Create(IEnumerable<StructurePlanDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return new StructurePlanDefinitionRegistry(definitions);
    }

    public bool TryGetById(string id, out StructurePlanDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }
}
