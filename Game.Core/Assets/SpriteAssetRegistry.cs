using Game.Core.Data;

namespace Game.Core.Assets;

public sealed class SpriteAssetRegistry
{
    private readonly Dictionary<string, SpriteAssetDefinition> _byId;

    private SpriteAssetRegistry(IEnumerable<SpriteAssetDefinition> definitions, SpriteAssetDefinition fallback)
    {
        Fallback = fallback;
        _byId = new Dictionary<string, SpriteAssetDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            AddValidated(definition);
        }

        if (!_byId.ContainsKey(Fallback.Id))
        {
            AddValidated(Fallback);
        }

        ValidateSourceAliases();
    }

    public SpriteAssetDefinition Fallback { get; }

    public IReadOnlyCollection<SpriteAssetDefinition> Definitions => _byId.Values;

    public bool HasExplicitAssets => _byId.Keys.Any(id => !string.Equals(id, Fallback.Id, StringComparison.OrdinalIgnoreCase));

    public static SpriteAssetRegistry Create(IEnumerable<SpriteAssetDefinition> definitions)
    {
        return new SpriteAssetRegistry(definitions, CreateFallbackDefinition());
    }

    public SpriteAssetDefinition GetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.GetValueOrDefault(id, Fallback);
    }

    public bool TryGetById(string id, out SpriteAssetDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.TryGetValue(id, out definition!);
    }

    private void AddValidated(SpriteAssetDefinition definition)
    {
        Validate(definition);

        if (_byId.ContainsKey(definition.Id))
        {
            throw new RegistryValidationException($"Duplicate sprite asset id '{definition.Id}'.");
        }

        _byId.Add(definition.Id, definition);
    }

    private void ValidateSourceAliases()
    {
        foreach (var definition in _byId.Values)
        {
            if (string.IsNullOrWhiteSpace(definition.SourceAliasOf))
            {
                continue;
            }

            if (!_byId.TryGetValue(definition.SourceAliasOf, out var canonical))
            {
                throw new RegistryValidationException(
                    $"Sprite asset '{definition.Id}' aliases missing source asset '{definition.SourceAliasOf}'.");
            }

            if (ReferenceEquals(definition, canonical) ||
                string.Equals(definition.Id, canonical.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new RegistryValidationException($"Sprite asset '{definition.Id}' cannot alias itself.");
            }

            var aliasPath = definition.Path.Replace('\\', '/');
            var canonicalPath = canonical.Path.Replace('\\', '/');
            if (!string.Equals(aliasPath, canonicalPath, StringComparison.OrdinalIgnoreCase) ||
                definition.Width != canonical.Width ||
                definition.Height != canonical.Height)
            {
                throw new RegistryValidationException(
                    $"Sprite asset '{definition.Id}' must share path and dimensions with aliased source '{canonical.Id}'.");
            }
        }
    }

    private static void Validate(SpriteAssetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RequireText(definition.Id, nameof(definition.Id));
        RequireText(definition.Path, nameof(definition.Path));

        var normalizedPath = definition.Path.Replace('\\', '/');
        if (Path.IsPathRooted(definition.Path) ||
            normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            throw new RegistryValidationException(
                $"Sprite asset '{definition.Id}' must use a relative path contained by its source root.");
        }

        if (!string.IsNullOrWhiteSpace(definition.SourceRoot) && !Path.IsPathFullyQualified(definition.SourceRoot))
        {
            throw new RegistryValidationException(
                $"Sprite asset '{definition.Id}' source root must be an absolute resolved path.");
        }

        if (definition.Width <= 0 || definition.Height <= 0)
        {
            throw new RegistryValidationException($"Sprite asset '{definition.Id}' must have positive dimensions.");
        }

        if (definition.PixelsPerUnit <= 0)
        {
            throw new RegistryValidationException($"Sprite asset '{definition.Id}' must have a positive pixels-per-unit value.");
        }

        if (definition.OriginX.HasValue != definition.OriginY.HasValue)
        {
            throw new RegistryValidationException($"Sprite asset '{definition.Id}' must declare both origin coordinates or neither.");
        }

        if (definition.OriginX is { } originX && definition.OriginY is { } originY &&
            (originX < 0 || originX > definition.Width || originY < 0 || originY > definition.Height))
        {
            throw new RegistryValidationException($"Sprite asset '{definition.Id}' declares an origin outside its sprite bounds.");
        }

        foreach (var frame in definition.Frames)
        {
            if (frame.Width <= 0 || frame.Height <= 0)
            {
                throw new RegistryValidationException($"Sprite asset '{definition.Id}' contains a frame with invalid dimensions.");
            }

            if (frame.X < 0 || frame.Y < 0)
            {
                throw new RegistryValidationException($"Sprite asset '{definition.Id}' contains a frame with negative coordinates.");
            }

            if (frame.X + frame.Width > definition.Width || frame.Y + frame.Height > definition.Height)
            {
                throw new RegistryValidationException($"Sprite asset '{definition.Id}' contains a frame outside the declared sprite bounds.");
            }

            if (frame.AutoTileMask is { } mask && (mask < 0 || mask > 15))
            {
                throw new RegistryValidationException($"Sprite asset '{definition.Id}' contains an invalid autotile mask.");
            }
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RegistryValidationException($"Sprite asset field '{name}' is required.");
        }
    }

    private static SpriteAssetDefinition CreateFallbackDefinition()
    {
        return new SpriteAssetDefinition
        {
            Id = "missing_sprite",
            Path = "sprites/system/missing.png",
            Category = SpriteAssetCategory.Ui,
            Width = 16,
            Height = 16,
            Tags = new[] { "system", "fallback" }
        };
    }
}
