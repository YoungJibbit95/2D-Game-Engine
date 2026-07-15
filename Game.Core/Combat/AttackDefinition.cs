namespace Game.Core.Combat;

public enum AttackPhase
{
    Idle,
    Windup,
    Active,
    Recovery
}

public sealed record AttackDefinition
{
    public required string Id { get; init; }

    public float WindupSeconds { get; init; }

    public required float ActiveSeconds { get; init; }

    public float RecoverySeconds { get; init; }

    public float CooldownSeconds { get; init; }

    public float? ComboWindowStartSeconds { get; init; }

    public float? ComboWindowEndSeconds { get; init; }

    public MeleeSweepDefinition? MeleeSweep { get; init; }

    public AttackResourceCost ResourceCost { get; init; }

    public int MaxTargetsPerSwing { get; init; } = 32;

    public float TotalDurationSeconds => WindupSeconds + ActiveSeconds + RecoverySeconds;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ValidateNonNegative(WindupSeconds, nameof(WindupSeconds));
        ValidatePositive(ActiveSeconds, nameof(ActiveSeconds));
        ValidateNonNegative(RecoverySeconds, nameof(RecoverySeconds));
        ValidateNonNegative(CooldownSeconds, nameof(CooldownSeconds));
        ResourceCost.Validate();
        if (MaxTargetsPerSwing is <= 0 or > AttackSequenceDefinition.MaximumHitCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxTargetsPerSwing));
        }

        if (ComboWindowStartSeconds.HasValue != ComboWindowEndSeconds.HasValue)
        {
            throw new InvalidOperationException("Combo window start and end must either both be set or both be omitted.");
        }

        if (ComboWindowStartSeconds is not { } start || ComboWindowEndSeconds is not { } end)
        {
            MeleeSweep?.Validate();
            return;
        }

        ValidateNonNegative(start, nameof(ComboWindowStartSeconds));
        ValidateNonNegative(end, nameof(ComboWindowEndSeconds));
        if (end < start || end > TotalDurationSeconds)
        {
            throw new InvalidOperationException("Combo window must be ordered and contained in the attack timeline.");
        }

        MeleeSweep?.Validate();
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
