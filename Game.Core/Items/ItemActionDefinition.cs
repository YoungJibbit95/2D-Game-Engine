using Game.Core.Combat;

namespace Game.Core.Items;

public sealed record ItemActionDefinition
{
    public ItemActionKind Kind { get; init; }

    public string? AttackSequenceId { get; init; }

    public string? ProjectileId { get; init; }

    public string? AmmoItemId { get; init; }

    public int AmmoCost { get; init; } = 1;

    public float ProjectileSpeedMultiplier { get; init; } = 1f;

    public float ReachPixels { get; init; }

    public float? ManaRegenerationDelaySeconds { get; init; }

    public ManaRefundPolicy ManaRefundPolicy { get; init; } = ManaRefundPolicy.BeforeEffect;

    public static ItemActionDefinition Create(ItemActionKind kind)
    {
        return new ItemActionDefinition { Kind = kind };
    }
}