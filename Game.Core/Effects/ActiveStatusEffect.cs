namespace Game.Core.Effects;

public sealed class ActiveStatusEffect
{
    internal ActiveStatusEffect(StatusEffectDefinition definition, float durationSeconds)
    {
        Definition = definition;
        RemainingSeconds = Math.Max(0, durationSeconds);
    }

    public StatusEffectDefinition Definition { get; }

    public float RemainingSeconds { get; private set; }

    internal float TickAccumulator { get; set; }

    internal void Refresh(float durationSeconds)
    {
        RemainingSeconds = Math.Max(RemainingSeconds, Math.Max(0, durationSeconds));
    }

    internal void Advance(float deltaSeconds)
    {
        RemainingSeconds = Math.Max(0, RemainingSeconds - Math.Max(0, deltaSeconds));
    }
}
