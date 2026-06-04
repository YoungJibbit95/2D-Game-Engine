using Game.Core.Data;
using Game.Core.Inventory;

namespace Game.Core.Startup;

public sealed class GameStartupRegistry
{
    private readonly Dictionary<string, GameStartupDefinition> _byId;

    private GameStartupRegistry(IEnumerable<GameStartupDefinition> definitions)
    {
        _byId = new Dictionary<string, GameStartupDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<GameStartupDefinition> Definitions => _byId.Values;

    public static GameStartupRegistry Create(IEnumerable<GameStartupDefinition> definitions)
    {
        return new GameStartupRegistry(definitions);
    }

    public bool TryGetById(string id, out GameStartupDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    public GameStartupDefinition GetById(string id)
    {
        return TryGetById(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Game startup definition '{id}' was not registered.");
    }

    public bool TryGetDefault(string? preferredId, out GameStartupDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(preferredId) && TryGetById(preferredId, out definition!))
        {
            return true;
        }

        if (TryGetById("default", out definition!))
        {
            return true;
        }

        definition = Definitions.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault()!;
        return definition is not null;
    }

    private void AddValidated(GameStartupDefinition definition)
    {
        Validate(definition);
        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate game startup id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(GameStartupDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));

        if (definition.SelectedHotbarSlot < 0 || definition.SelectedHotbarSlot >= PlayerInventory.HotbarSlotCount)
        {
            throw new RegistryValidationException(
                $"Game startup '{definition.Id}' selected hotbar slot must be inside 0..{PlayerInventory.HotbarSlotCount - 1}.");
        }

        var targetedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in definition.StarterItems)
        {
            RequireText(item.ItemId, nameof(item.ItemId));
            if (item.Count <= 0)
            {
                throw new RegistryValidationException($"Game startup '{definition.Id}' item '{item.ItemId}' must have a positive count.");
            }

            if (item.Slot is < 0)
            {
                throw new RegistryValidationException($"Game startup '{definition.Id}' item '{item.ItemId}' has a negative slot.");
            }

            if (item.Target == StarterInventoryTarget.Hotbar && item.Slot >= PlayerInventory.HotbarSlotCount)
            {
                throw new RegistryValidationException($"Game startup '{definition.Id}' item '{item.ItemId}' hotbar slot is outside the hotbar.");
            }

            if (item.Target == StarterInventoryTarget.Main && item.Slot >= PlayerInventory.MainSlotCount)
            {
                throw new RegistryValidationException($"Game startup '{definition.Id}' item '{item.ItemId}' main inventory slot is outside the main inventory.");
            }

            if (item.Target != StarterInventoryTarget.Auto && item.Slot.HasValue)
            {
                var key = $"{item.Target}:{item.Slot.Value}";
                if (!targetedSlots.Add(key))
                {
                    throw new RegistryValidationException($"Game startup '{definition.Id}' targets inventory slot '{key}' more than once.");
                }
            }
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Game startup definition field '{name}' is required.");
        }
    }
}
