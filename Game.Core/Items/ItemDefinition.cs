using Game.Core.Equipment;
using Game.Core.Data;
using Game.Core.Combat;
using Game.Core.Effects;

namespace Game.Core.Items;

public sealed record ItemDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public ItemType Type { get; init; }

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

    public int ManaCost { get; init; }

    public int ManaRestore { get; init; }

    public IReadOnlyList<ItemActionDefinition> Actions { get; init; } = Array.Empty<ItemActionDefinition>();

    public AttackShapeDefinition? AttackShape { get; init; }

    public IReadOnlyList<StatusEffectApplication> OnHitEffects { get; init; } = Array.Empty<StatusEffectApplication>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
