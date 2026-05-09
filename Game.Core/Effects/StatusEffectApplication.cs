namespace Game.Core.Effects;

public sealed record StatusEffectApplication
{
    public required string EffectId { get; init; }

    public float Chance { get; init; } = 1f;

    public float? DurationSeconds { get; init; }
}
