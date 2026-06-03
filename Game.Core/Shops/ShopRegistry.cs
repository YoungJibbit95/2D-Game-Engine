using Game.Core.Data;

namespace Game.Core.Shops;

public sealed class ShopRegistry
{
    private readonly Dictionary<string, ShopDefinition> _byId;

    private ShopRegistry(IEnumerable<ShopDefinition> definitions)
    {
        _byId = new Dictionary<string, ShopDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<ShopDefinition> Definitions => _byId.Values;

    public static ShopRegistry Create(IEnumerable<ShopDefinition> definitions)
    {
        return new ShopRegistry(definitions);
    }

    public bool TryGetById(string id, out ShopDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    public ShopDefinition GetById(string id)
    {
        return TryGetById(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Shop definition '{id}' was not registered.");
    }

    private void AddValidated(ShopDefinition definition)
    {
        Validate(definition);
        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate shop id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(ShopDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.CurrencyItemId, nameof(definition.CurrencyItemId));

        var stockItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stock in definition.Stock)
        {
            RequireText(stock.ItemId, nameof(stock.ItemId));
            if (stock.Count <= 0)
            {
                throw new RegistryValidationException($"Shop '{definition.Id}' stock '{stock.ItemId}' must have a positive count.");
            }

            if (stock.Price < 0)
            {
                throw new RegistryValidationException($"Shop '{definition.Id}' stock '{stock.ItemId}' has a negative price.");
            }

            if (!stockItems.Add(stock.ItemId))
            {
                throw new RegistryValidationException($"Shop '{definition.Id}' has duplicate stock item '{stock.ItemId}'.");
            }
        }

        var sellItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sellPrice in definition.SellPrices)
        {
            RequireText(sellPrice.ItemId, nameof(sellPrice.ItemId));
            if (sellPrice.Price < 0)
            {
                throw new RegistryValidationException($"Shop '{definition.Id}' sell item '{sellPrice.ItemId}' has a negative price.");
            }

            if (!sellItems.Add(sellPrice.ItemId))
            {
                throw new RegistryValidationException($"Shop '{definition.Id}' has duplicate sell item '{sellPrice.ItemId}'.");
            }
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Shop definition field '{name}' is required.");
        }
    }
}
