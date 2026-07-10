namespace Game.Core.Saving;

public sealed record ActiveStatusEffectSaveData
{
    public string EffectId { get; init; } = string.Empty;

    public float RemainingDurationSeconds { get; init; }
}
