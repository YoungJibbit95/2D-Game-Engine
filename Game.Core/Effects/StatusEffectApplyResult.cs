namespace Game.Core.Effects;

public sealed record StatusEffectApplyResult(IReadOnlyList<AppliedStatusEffectResult> AppliedEffects)
{
    public int AppliedCount => AppliedEffects.Count;

    public bool Changed => AppliedCount > 0;

    public static StatusEffectApplyResult None { get; } = new(Array.Empty<AppliedStatusEffectResult>());
}
