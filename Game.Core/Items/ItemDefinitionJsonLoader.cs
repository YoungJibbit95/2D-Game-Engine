using Game.Core.Equipment;
using Game.Core.Data;
using Game.Core.Combat;
using Game.Core.Effects;
using Game.Core.Movement;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Core.Items;

public sealed class ItemDefinitionJsonLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ItemRegistry LoadRegistryFromDirectory(string directoryPath)
    {
        return ItemRegistry.Create(LoadDefinitionsFromDirectory(directoryPath));
    }

    public IReadOnlyList<ItemDefinition> LoadDefinitionsFromDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<ItemDefinition>();
        }

        return Directory
            .EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinitionFromFile)
            .ToArray();
    }

    public ItemDefinition LoadDefinitionFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = File.OpenRead(filePath);
        return LoadDefinition(stream, filePath);
    }

    public ItemDefinition LoadDefinitionFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return LoadDefinition(stream, "inline json");
    }

    private static ItemDefinition LoadDefinition(Stream stream, string source)
    {
        var definition = JsonSerializer.Deserialize<ItemDefinitionDto>(stream, Options);
        if (definition is null)
        {
            throw new JsonException($"Item definition was empty: {source}");
        }

        return definition.ToDefinition();
    }

    private sealed record ItemDefinitionDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? Description { get; init; }

        public ItemType Type { get; init; }

        public ItemRarity Rarity { get; init; }

        public int Value { get; init; }

        public ItemCategory Category { get; init; } = ItemCategory.Automatic;

        public int SortPriority { get; init; }

        public bool CanFavorite { get; init; } = true;

        public bool CanTrash { get; init; } = true;

        public string? TexturePath { get; init; }

        [JsonPropertyName("texture")]
        public string? Texture { get; init; }

        public int MaxStack { get; init; } = 1;

        public float UseTime { get; init; }

        public int Damage { get; init; }

        public int ToolPower { get; init; }

        public float Knockback { get; init; }

        public string? PlacesTileId { get; init; }

        [JsonPropertyName("placesTile")]
        public string? PlacesTile { get; init; }

        public PlacementSupportRule PlacementSupport { get; init; }

        public EquipmentSlotType? EquipmentSlot { get; init; }

        public int Defense { get; init; }

        public int MaxHealthBonus { get; init; }

        public float MovementSpeedBonus { get; init; }

        public float MeleeDamageBonus { get; init; }

        public float RangedDamageBonus { get; init; }

        public float MiningSpeedBonus { get; init; }

        public int MaxManaBonus { get; init; }

        public float MagicDamageBonus { get; init; }

        public float ManaCostReduction { get; init; }

        public float ManaRegenBonus { get; init; }

        public MobilityAbilityDefinition? Mobility { get; init; }

        public bool CanDoubleJump { get; init; }

        public bool CanWallJump { get; init; }

        public bool CanFly { get; init; }

        public bool CanGlide { get; init; }

        public int ManaCost { get; init; }

        public int ManaRestore { get; init; }

        public int HealthRestore { get; init; }

        public List<ItemActionDefinitionDto> Actions { get; init; } = new();

        public AttackShapeDefinition? AttackShape { get; init; }

        public List<StatusEffectApplicationDto> OnHitEffects { get; init; } = new();

        public List<StatusEffectApplicationDto> StatusEffectApplications { get; init; } = new();

        public string[] Tags { get; init; } = Array.Empty<string>();

        public ItemDefinition ToDefinition()
        {
            return new ItemDefinition
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                Description = Description?.Trim() ?? string.Empty,
                Type = Type,
                Rarity = Rarity,
                Value = Value,
                Category = Category,
                SortPriority = SortPriority,
                CanFavorite = CanFavorite,
                CanTrash = CanTrash,
                TexturePath = TexturePath ?? Texture ?? string.Empty,
                MaxStack = MaxStack,
                UseTime = UseTime,
                Damage = Damage,
                ToolPower = ToolPower,
                Knockback = Knockback,
                PlacesTileId = PlacesTileId ?? PlacesTile,
                PlacementSupport = PlacementSupport,
                EquipmentSlot = EquipmentSlot,
                Defense = Defense,
                MaxHealthBonus = MaxHealthBonus,
                MovementSpeedBonus = MovementSpeedBonus,
                MeleeDamageBonus = MeleeDamageBonus,
                RangedDamageBonus = RangedDamageBonus,
                MiningSpeedBonus = MiningSpeedBonus,
                MaxManaBonus = MaxManaBonus,
                MagicDamageBonus = MagicDamageBonus,
                ManaCostReduction = ManaCostReduction,
                ManaRegenBonus = ManaRegenBonus,
                Mobility = Mobility,
                CanDoubleJump = CanDoubleJump || Mobility?.HasDoubleJump == true,
                CanWallJump = CanWallJump || Mobility?.CanWallJump == true,
                CanFly = CanFly || Mobility?.HasFlight == true,
                CanGlide = CanGlide || Mobility?.HasGlide == true,
                ManaCost = ManaCost,
                ManaRestore = ManaRestore,
                HealthRestore = HealthRestore,
                Actions = Actions.Count > 0
                    ? Actions.Select(action => action.ToDefinition()).ToArray()
                    : new[] { ItemActionResolver.InferFromLegacyType(Type) }.Where(action => action.Kind != ItemActionKind.None).ToArray(),
                AttackShape = AttackShape,
                OnHitEffects = OnHitEffects.Select(effect => effect.ToDefinition()).ToArray(),
                StatusEffectApplications = StatusEffectApplications.Select(effect => effect.ToDefinition()).ToArray(),
                Tags = DefinitionTags.Normalize(Tags)
            };
        }
    }

    private sealed record ItemActionDefinitionDto
    {
        public ItemActionKind Kind { get; init; }

        public string? AttackSequenceId { get; init; }

        [JsonPropertyName("attackSequence")]
        public string? AttackSequence { get; init; }

        public string? ProjectileId { get; init; }

        [JsonPropertyName("projectile")]
        public string? Projectile { get; init; }

        public string? AmmoItemId { get; init; }

        [JsonPropertyName("ammo")]
        public string? Ammo { get; init; }

        public int AmmoCost { get; init; } = 1;

        public float ProjectileSpeedMultiplier { get; init; } = 1f;

        public float ReachPixels { get; init; }

        public float? ManaRegenerationDelaySeconds { get; init; }

        public ManaRefundPolicy ManaRefundPolicy { get; init; } = ManaRefundPolicy.BeforeEffect;

        public ItemActionDefinition ToDefinition()
        {
            return new ItemActionDefinition
            {
                Kind = Kind,
                AttackSequenceId = AttackSequenceId ?? AttackSequence,
                ProjectileId = ProjectileId ?? Projectile,
                AmmoItemId = AmmoItemId ?? Ammo,
                AmmoCost = AmmoCost,
                ProjectileSpeedMultiplier = ProjectileSpeedMultiplier,
                ReachPixels = ReachPixels,
                ManaRegenerationDelaySeconds = ManaRegenerationDelaySeconds,
                ManaRefundPolicy = ManaRefundPolicy
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
