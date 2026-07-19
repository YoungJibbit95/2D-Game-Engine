using Game.Core.Data;

namespace Game.Core.Combat;

public sealed class AttackSequenceRegistry
{
    private readonly Dictionary<string, AttackSequenceDefinition> _byId;

    private AttackSequenceRegistry(IEnumerable<AttackSequenceDefinition> definitions)
    {
        _byId = new Dictionary<string, AttackSequenceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            try
            {
                definition.Validate();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                throw new RegistryValidationException(
                    $"Attack sequence '{definition.Id}' is invalid: {exception.Message}");
            }

            if (!_byId.TryAdd(definition.Id, definition))
            {
                throw new RegistryValidationException($"Duplicate attack sequence id '{definition.Id}'.");
            }
        }
    }

    public IReadOnlyCollection<AttackSequenceDefinition> Definitions => _byId.Values;

    public static AttackSequenceRegistry Create(IEnumerable<AttackSequenceDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return new AttackSequenceRegistry(definitions);
    }

    public AttackSequenceDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown attack sequence '{id}'.");
    }

    public bool TryGetById(string id, out AttackSequenceDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }
}
