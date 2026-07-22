using Game.Core.Data;
using Game.Core.Effects;

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

        if (definition.Value < 0)
        {
            throw new RegistryValidationException($"Item '{definition.Id}' has a negative value.");
        }

        if (definition.HealthRestore < 0 || definition.ManaRestore < 0)
        {
            throw new RegistryValidationException($"Item '{definition.Id}' has a negative resource restore value.");
        }

        if (definition.ManaCost < 0)
        {
            throw new RegistryValidationException($"Item '{definition.Id}' has a negative mana cost.");
        }

        if (definition.Damage < 0 || !float.IsFinite(definition.Knockback) || definition.Knockback < 0)
        {
            throw new RegistryValidationException($"Item '{definition.Id}' has invalid combat values.");
        }

        if (definition.Mobility is not null)
        {
            try
            {
                definition.Mobility.Validate();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new RegistryValidationException(
                    $"Item '{definition.Id}' has invalid mobility tuning: {exception.ParamName}.");
            }
        }

        foreach (var action in definition.Actions)
        {
            ValidateAction(definition, action);
        }

        ValidateEffects(definition.Id, definition.OnHitEffects);
        ValidateEffects(definition.Id, definition.StatusEffectApplications);
    }

    private static void ValidateEffects(string itemId, IEnumerable<StatusEffectApplication> effects)
    {
        foreach (var effect in effects)
        {
            if (string.IsNullOrWhiteSpace(effect.EffectId))
            {
                throw new RegistryValidationException($"Item '{itemId}' has a status effect application without an effect id.");
            }

            if (!float.IsFinite(effect.Chance) || effect.Chance < 0 || effect.Chance > 1)
            {
                throw new RegistryValidationException($"Item '{itemId}' has a status effect application with negative chance.");
            }

            if (effect.DurationSeconds is { } duration && (!float.IsFinite(duration) || duration <= 0))
            {
                throw new RegistryValidationException($"Item '{itemId}' has a status effect application with non-positive duration override.");
            }
        }
    }

    private static void ValidateAction(ItemDefinition item, ItemActionDefinition action)
    {
        if (action.AttackSequenceId is not null && string.IsNullOrWhiteSpace(action.AttackSequenceId))
        {
            throw new RegistryValidationException(
                $"Item '{item.Id}' has an empty attack sequence id.");
        }

        if (!string.IsNullOrWhiteSpace(action.AttackSequenceId) &&
            action.Kind is not (ItemActionKind.Melee or ItemActionKind.Shoot or ItemActionKind.Cast))
        {
            throw new RegistryValidationException(
                $"Item '{item.Id}' uses an attack sequence on a non-combat action.");
        }

        if (action.Kind is ItemActionKind.Shoot or ItemActionKind.Cast && string.IsNullOrWhiteSpace(action.ProjectileId))
        {
            throw new RegistryValidationException($"Item '{item.Id}' has a projectile action without a projectile id.");
        }

        if (action.Kind == ItemActionKind.Place && string.IsNullOrWhiteSpace(item.PlacesTileId))
        {
            throw new RegistryValidationException($"Item '{item.Id}' has a place action without a placed tile id.");
        }

        if (action.AmmoCost <= 0)
        {
            throw new RegistryValidationException($"Item '{item.Id}' has an action with a non-positive ammo cost.");
        }

        if (!float.IsFinite(action.ProjectileSpeedMultiplier) || action.ProjectileSpeedMultiplier <= 0)
        {
            throw new RegistryValidationException($"Item '{item.Id}' has an action with a non-positive projectile speed multiplier.");
        }

        if (!float.IsFinite(action.ReachPixels) || action.ReachPixels < 0)
        {
            throw new RegistryValidationException($"Item '{item.Id}' has an action with a negative reach.");
        }

        if (action.ManaRegenerationDelaySeconds is { } manaDelay &&
            (!float.IsFinite(manaDelay) || manaDelay < 0))
        {
            throw new RegistryValidationException(
                $"Item '{item.Id}' has an invalid mana regeneration delay.");
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
            Description = "The original item definition could not be found.",
            Rarity = ItemRarity.Quest,
            Value = 0,
            Category = ItemCategory.Quest,
            CanFavorite = false,
            CanTrash = false,
            TexturePath = "items/missing",
            MaxStack = 1
        };
    }
}
