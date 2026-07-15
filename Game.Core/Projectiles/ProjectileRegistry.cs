using Game.Core.Data;
using Game.Core.Effects;

namespace Game.Core.Projectiles;

public sealed class ProjectileRegistry
{
    private readonly Dictionary<string, ProjectileDefinition> _byId;

    private ProjectileRegistry(IEnumerable<ProjectileDefinition> definitions)
    {
        _byId = new Dictionary<string, ProjectileDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }
    }

    public IReadOnlyCollection<ProjectileDefinition> Definitions => _byId.Values;

    public static ProjectileRegistry Create(IEnumerable<ProjectileDefinition> definitions)
    {
        return new ProjectileRegistry(definitions);
    }

    public ProjectileDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!_byId.TryGetValue(id, out var definition))
        {
            throw new KeyNotFoundException($"Projectile '{id}' was not registered.");
        }

        return definition;
    }

    public bool TryGetById(string id, out ProjectileDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(ProjectileDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate projectile id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private static void Validate(ProjectileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.TexturePath, nameof(definition.TexturePath));

        if (definition.Speed < 0)
        {
            throw new RegistryValidationException($"Projectile '{definition.Id}' has negative speed.");
        }

        RequireFinite(definition.Speed, definition.Id, nameof(definition.Speed));

        if (definition.Damage < 0)
        {
            throw new RegistryValidationException($"Projectile '{definition.Id}' has negative damage.");
        }

        if (definition.Lifetime <= 0)
        {
            throw new RegistryValidationException($"Projectile '{definition.Id}' must have positive lifetime.");
        }

        RequireFinite(definition.Lifetime, definition.Id, nameof(definition.Lifetime));
        RequireNonNegative(definition.DragPerSecond, definition.Id, nameof(definition.DragPerSecond));
        RequireNonNegative(
            definition.HomingTurnRateRadiansPerSecond,
            definition.Id,
            nameof(definition.HomingTurnRateRadiansPerSecond));
        RequireNonNegative(definition.HomingRange, definition.Id, nameof(definition.HomingRange));
        RequireNonNegative(definition.Pierce, definition.Id, nameof(definition.Pierce));
        RequireNonNegative(definition.BounceCount, definition.Id, nameof(definition.BounceCount));
        RequireFraction(definition.BounceRestitution, definition.Id, nameof(definition.BounceRestitution));
        if (!float.IsFinite(definition.CollisionRadius) || definition.CollisionRadius <= 0)
        {
            throw new RegistryValidationException(
                $"Projectile '{definition.Id}' field '{nameof(definition.CollisionRadius)}' must be positive and finite.");
        }

        RequireNonNegative(definition.Knockback, definition.Id, nameof(definition.Knockback));
        RequireFraction(definition.CriticalChance, definition.Id, nameof(definition.CriticalChance));
        if (!float.IsFinite(definition.CriticalMultiplier) || definition.CriticalMultiplier < 1)
        {
            throw new RegistryValidationException(
                $"Projectile '{definition.Id}' field '{nameof(definition.CriticalMultiplier)}' must be at least one.");
        }

        RequireFinite(definition.Gravity, definition.Id, nameof(definition.Gravity));

        ValidateEffects(definition.Id, definition.OnHitEffects);
    }

    private static void ValidateEffects(string projectileId, IEnumerable<StatusEffectApplication> effects)
    {
        foreach (var effect in effects)
        {
            if (string.IsNullOrWhiteSpace(effect.EffectId))
            {
                throw new RegistryValidationException($"Projectile '{projectileId}' has a status effect application without an effect id.");
            }

            if (effect.Chance < 0)
            {
                throw new RegistryValidationException($"Projectile '{projectileId}' has a status effect application with negative chance.");
            }

            if (effect.DurationSeconds is <= 0)
            {
                throw new RegistryValidationException($"Projectile '{projectileId}' has a status effect application with non-positive duration override.");
            }
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Projectile definition field '{name}' is required.");
        }
    }

    private static void RequireNonNegative(float value, string projectileId, string name)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new RegistryValidationException(
                $"Projectile '{projectileId}' field '{name}' must be non-negative and finite.");
        }
    }

    private static void RequireNonNegative(int value, string projectileId, string name)
    {
        if (value < 0)
        {
            throw new RegistryValidationException(
                $"Projectile '{projectileId}' field '{name}' must be non-negative.");
        }
    }

    private static void RequireFraction(float value, string projectileId, string name)
    {
        if (!float.IsFinite(value) || value < 0 || value > 1)
        {
            throw new RegistryValidationException(
                $"Projectile '{projectileId}' field '{name}' must be between zero and one.");
        }
    }

    private static void RequireFinite(float value, string projectileId, string name)
    {
        if (!float.IsFinite(value))
        {
            throw new RegistryValidationException(
                $"Projectile '{projectileId}' field '{name}' must be finite.");
        }
    }
}
