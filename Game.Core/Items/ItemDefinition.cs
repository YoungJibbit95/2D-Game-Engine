using Game.Core.Equipment;
using Game.Core.Data;
using Game.Core.Combat;
using Game.Core.Effects;
using Game.Core.Movement;

namespace Game.Core.Items;

public sealed record ItemDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Description { get; init; } = string.Empty;

    public ItemType Type { get; init; }

    public ItemRarity Rarity { get; init; }

    public int Value { get; init; }

    public ItemCategory Category { get; init; } = ItemCategory.Automatic;

    public int SortPriority { get; init; }

    public bool CanFavorite { get; init; } = true;

    public bool CanTrash { get; init; } = true;

    public required string TexturePath { get; init; }

    public int MaxStack { get; init; } = 1;

    public float UseTime { get; init; }

    public int Damage { get; init; }

    public int ToolPower { get; init; }

    public float Knockback { get; init; }

    public string? PlacesTileId { get; init; }

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

    public IReadOnlyList<ItemActionDefinition> Actions { get; init; } = Array.Empty<ItemActionDefinition>();

    public AttackShapeDefinition? AttackShape { get; init; }

    public IReadOnlyList<StatusEffectApplication> OnHitEffects { get; init; } = Array.Empty<StatusEffectApplication>();

    public IReadOnlyList<StatusEffectApplication> StatusEffectApplications { get; init; } = Array.Empty<StatusEffectApplication>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }

    public ItemCategory ResolvedCategory => ItemCategoryResolver.Resolve(this);
}
