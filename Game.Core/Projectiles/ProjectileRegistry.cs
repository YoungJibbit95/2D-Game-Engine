using Game.Core.Data;

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

        if (definition.Damage < 0)
        {
            throw new RegistryValidationException($"Projectile '{definition.Id}' has negative damage.");
        }

        if (definition.Lifetime <= 0)
        {
            throw new RegistryValidationException($"Projectile '{definition.Id}' must have positive lifetime.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Projectile definition field '{name}' is required.");
        }
    }
}
