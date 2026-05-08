using Game.Core.Data;

namespace Game.Core.Items;

public sealed class ItemRegistry : IItemDefinitionProvider
{
    private readonly Dictionary<string, ItemDefinition> _byId;

    private ItemRegistry(IEnumerable<ItemDefinition> definitions, ItemDefinition fallback)
    {
        Fallback = fallback;
        _byId = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }

        if (!_byId.ContainsKey(Fallback.Id))
        {
            AddValidated(Fallback);
        }
    }

    public ItemDefinition Fallback { get; }

    public IReadOnlyCollection<ItemDefinition> Definitions => _byId.Values;

    public static ItemRegistry Create(IEnumerable<ItemDefinition> definitions)
    {
        return new ItemRegistry(definitions, CreateFallbackDefinition());
    }

    public ItemDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.GetValueOrDefault(id, Fallback);
    }

    public bool TryGetById(string id, out ItemDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(ItemDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate item id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(ItemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.TexturePath, nameof(definition.TexturePath));

        if (definition.MaxStack <= 0)
        {
            throw new RegistryValidationException($"Item '{definition.Id}' must have a positive max stack.");
        }

        if (definition.UseTime < 0)
        {
            throw new RegistryValidationException($"Item '{definition.Id}' has negative use time.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Item definition field '{name}' is required.");
        }
    }

    private static ItemDefinition CreateFallbackDefinition()
    {
        return new ItemDefinition
        {
            Id = "missing_item",
            DisplayName = "Missing Item",
            Type = ItemType.QuestItem,
            TexturePath = "items/missing",
            MaxStack = 1
        };
    }
}
