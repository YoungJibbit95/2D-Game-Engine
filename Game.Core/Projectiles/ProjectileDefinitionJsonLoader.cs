using Game.Core.Effects;
using Game.Core.Combat;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Projectiles;

public sealed class ProjectileDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProjectileRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return ProjectileRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<ProjectileDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<ProjectileDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public ProjectileDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public ProjectileDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static ProjectileDefinition LoadDefinition(Stream stream, string source)
    {
        var dto = JsonSerializer.Deserialize<ProjectileDefinitionDto>(stream, Options);
        if (dto is null)
        {
            throw new JsonException($"Projectile definition was empty: {source}");
        }

        return dto.ToDefinition();
    }

    private sealed record ProjectileDefinitionDto
    {
        public string? Id { get; init; }

        public string? TexturePath { get; init; }

        [JsonPropertyName("texture")]
        public string? Texture { get; init; }

        public float Speed { get; init; }

        public int Damage { get; init; }

        public DamageType DamageType { get; init; } = DamageType.Ranged;

        public float Gravity { get; init; }

        public float DragPerSecond { get; init; }

        public float HomingTurnRateRadiansPerSecond { get; init; }

        public float HomingRange { get; init; }

        public int Pierce { get; init; }

        public int BounceCount { get; init; }

        public float BounceRestitution { get; init; } = 1;

        public float CollisionRadius { get; init; } = 2;

        public ProjectileTileCollisionBehavior TileCollisionBehavior { get; init; } =
            ProjectileTileCollisionBehavior.Destroy;

        public ProjectileEntityCollisionBehavior EntityCollisionBehavior { get; init; } =
            ProjectileEntityCollisionBehavior.Damage;

        public bool FriendlyFire { get; init; }

        public bool HitOncePerTarget { get; init; } = true;

        public float Knockback { get; init; } = 1;

        public float CriticalChance { get; init; }

        public float CriticalMultiplier { get; init; } = 2;

        public float Lifetime { get; init; }

        public List<StatusEffectApplicationDto> OnHitEffects { get; init; } = new();

        public ProjectileDefinition ToDefinition()
        {
            return new ProjectileDefinition
            {
                Id = Id ?? string.Empty,
                TexturePath = TexturePath ?? Texture ?? string.Empty,
                Speed = Speed,
                Damage = Damage,
                DamageType = DamageType,
                Gravity = Gravity,
                DragPerSecond = DragPerSecond,
                HomingTurnRateRadiansPerSecond = HomingTurnRateRadiansPerSecond,
                HomingRange = HomingRange,
                Pierce = Pierce,
                BounceCount = BounceCount,
                BounceRestitution = BounceRestitution,
                CollisionRadius = CollisionRadius,
                TileCollisionBehavior = TileCollisionBehavior,
                EntityCollisionBehavior = EntityCollisionBehavior,
                FriendlyFire = FriendlyFire,
                HitOncePerTarget = HitOncePerTarget,
                Knockback = Knockback,
                CriticalChance = CriticalChance,
                CriticalMultiplier = CriticalMultiplier,
                Lifetime = Lifetime,
                OnHitEffects = OnHitEffects.Select(effect => effect.ToDefinition()).ToArray()
            };
        }
    }

    private sealed record StatusEffectApplicationDto
    {
        public string? EffectId { get; init; }

        [JsonPropertyName("effect")]
        public string? Effect { get; init; }

        public float Chance { get; init; } = 1f;

        public float? DurationSeconds { get; init; }

        public StatusEffectApplication ToDefinition()
        {
            return new StatusEffectApplication
            {
                EffectId = EffectId ?? Effect ?? string.Empty,
                Chance = Chance,
                DurationSeconds = DurationSeconds
            };
        }
    }
}
