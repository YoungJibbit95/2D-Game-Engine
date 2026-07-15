using Game.Core.Effects;
using Game.Core.Entities.AI;

namespace Game.Core.Entities;

public sealed record EntityDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string TexturePath { get; init; }

    public required int MaxHealth { get; init; }

    public float Width { get; init; } = 16;

    public float Height { get; init; } = 16;

    public string? AiBehavior { get; init; }

    public AiProfileDefinition? Ai { get; init; }

    public EntityFaction Faction { get; init; } = EntityFaction.Hostile;

    public EntityMovementMode MovementMode { get; init; } = EntityMovementMode.Ground;

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public string? LootTableId { get; init; }

    public int ContactDamage { get; init; } = 10;

    public float ContactKnockback { get; init; } = 180f;

    public int? AttackDamage { get; init; }

    public float? AttackKnockback { get; init; }

    public EntityDespawnPolicyDefinition Despawn { get; init; } = new();

    public IReadOnlyList<StatusEffectApplication> OnContactEffects { get; init; } = Array.Empty<StatusEffectApplication>();
}
