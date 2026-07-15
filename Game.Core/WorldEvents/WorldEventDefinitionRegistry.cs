namespace Game.Core.WorldEvents;

public sealed class WorldEventDefinitionRegistry
{
    private readonly Dictionary<string, WorldEventDefinition> _byId;
    private readonly WorldEventDefinition[] _ordered;

    private WorldEventDefinitionRegistry(
        Dictionary<string, WorldEventDefinition> byId,
        WorldEventDefinition[] ordered)
    {
        _byId = byId;
        _ordered = ordered;
    }

    public IReadOnlyList<WorldEventDefinition> Definitions => _ordered;

    public static WorldEventDefinitionRegistry Create(IEnumerable<WorldEventDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var byId = new Dictionary<string, WorldEventDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            WorldEventDefinition.Validate(definition);
            if (!byId.TryAdd(definition.Id, definition))
            {
                throw new InvalidDataException($"Duplicate world event id '{definition.Id}'.");
            }
        }

        return new WorldEventDefinitionRegistry(
            byId,
            byId.Values.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public bool TryGetById(string id, out WorldEventDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null!;
            return false;
        }

        return _byId.TryGetValue(id, out definition!);
    }

    public WorldEventDefinition GetById(string id)
    {
        return TryGetById(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown world event '{id}'.");
    }
}
