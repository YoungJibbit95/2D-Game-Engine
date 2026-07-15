using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Entities.AI;

namespace Game.Core.Entities;

public sealed class EntityDefinitionRegistry
{
    private readonly Dictionary<string, EntityDefinition> _byId;

    private EntityDefinitionRegistry(IEnumerable<EntityDefinition> definitions)
    {
        _byId = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<EntityDefinition> Definitions => _byId.Values;

    public static EntityDefinitionRegistry Create(IEnumerable<EntityDefinition> definitions)
    {
        return new EntityDefinitionRegistry(definitions);
    }

    public EntityDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!_byId.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Entity definition '{id}' was not registered.");
        }

        return definition;
    }

    public bool TryGetById(string id, out EntityDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(EntityDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate entity definition id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(EntityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.DisplayName, nameof(definition.DisplayName));
        RequireText(definition.TexturePath, nameof(definition.TexturePath));

        if (definition.MaxHealth <= 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' must have positive max health.");
        }

        if (definition.Width <= 0 || definition.Height <= 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' must have positive size.");
        }

        if (definition.ContactDamage < 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' has negative contact damage.");
        }

        if (definition.ContactKnockback < 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' has negative contact knockback.");
        }

        if (definition.AttackDamage is < 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' has negative attack damage.");
        }

        if (definition.AttackKnockback is < 0 || definition.AttackKnockback is { } attackKnockback && !float.IsFinite(attackKnockback))
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' has invalid attack knockback.");
        }

        if (!float.IsFinite(definition.Despawn.SpawnProtectionSeconds) ||
            !float.IsFinite(definition.Despawn.DamageProtectionSeconds) ||
            definition.Despawn.SpawnProtectionSeconds < 0 ||
            definition.Despawn.DamageProtectionSeconds < 0)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' has invalid despawn protection timing.");
        }

        if (definition.Tags.Any(string.IsNullOrWhiteSpace))
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' has an empty tag.");
        }

        if (definition.Tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != definition.Tags.Count)
        {
            throw new RegistryValidationException($"Entity '{definition.Id}' has duplicate tags.");
        }

        if (definition.Ai is not null)
        {
            AiProfileDefinition.Validate(definition.Id, definition.Ai);
        }

        ValidateEffects(definition.Id, definition.OnContactEffects);
    }

    private static void ValidateEffects(string entityId, IEnumerable<StatusEffectApplication> effects)
    {
        foreach (var effect in effects)
        {
            if (string.IsNullOrWhiteSpace(effect.EffectId))
            {
                throw new RegistryValidationException($"Entity '{entityId}' has a status effect application without an effect id.");
            }

            if (effect.Chance < 0)
            {
                throw new RegistryValidationException($"Entity '{entityId}' has a status effect application with negative chance.");
            }

            if (effect.DurationSeconds is <= 0)
            {
                throw new RegistryValidationException($"Entity '{entityId}' has a status effect application with non-positive duration override.");
            }
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Entity definition field '{name}' is required.");
        }
    }
}
