namespace Game.Core.Effects;

public readonly record struct AppliedStatusEffectResult(
    string EffectId,
    bool Refreshed,
    float DurationSeconds);
