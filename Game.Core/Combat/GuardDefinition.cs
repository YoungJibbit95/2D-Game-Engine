namespace Game.Core.Combat;

public sealed record GuardDefinition
{
    public float MaxStamina { get; init; } = 100;

    public float StaminaRegenerationPerSecond { get; init; } = 20;

    public float ShieldArcRadians { get; init; } = MathF.PI;

    public float BlockWindowStartSeconds { get; init; }

    public float? BlockWindowEndSeconds { get; init; }

    public float ParryWindowStartSeconds { get; init; }

    public float ParryWindowSeconds { get; init; } = 0.12f;

    public float BlockDamageReduction { get; init; } = 0.75f;

    public float BlockKnockbackMultiplier { get; init; } = 0.35f;

    public float GuardStaminaCostPerDamage { get; init; } = 1;

    public float MinimumGuardStaminaCost { get; init; } = 1;

    public float GuardBreakDurationSeconds { get; init; } = 0.8f;

    public void Validate()
    {
        ValidatePositive(MaxStamina, nameof(MaxStamina));
        ValidateNonNegative(StaminaRegenerationPerSecond, nameof(StaminaRegenerationPerSecond));
        if (!float.IsFinite(ShieldArcRadians) || ShieldArcRadians <= 0 || ShieldArcRadians > MathF.Tau)
        {
            throw new ArgumentOutOfRangeException(nameof(ShieldArcRadians));
        }

        ValidateNonNegative(BlockWindowStartSeconds, nameof(BlockWindowStartSeconds));
        if (BlockWindowEndSeconds is { } blockWindowEnd)
        {
            ValidateNonNegative(blockWindowEnd, nameof(BlockWindowEndSeconds));
            if (blockWindowEnd < BlockWindowStartSeconds)
            {
                throw new InvalidOperationException("Block window end must not precede its start.");
            }
        }

        ValidateNonNegative(ParryWindowStartSeconds, nameof(ParryWindowStartSeconds));
        ValidateNonNegative(ParryWindowSeconds, nameof(ParryWindowSeconds));
        ValidateFraction(BlockDamageReduction, nameof(BlockDamageReduction));
        ValidateFraction(BlockKnockbackMultiplier, nameof(BlockKnockbackMultiplier));
        ValidateNonNegative(GuardStaminaCostPerDamage, nameof(GuardStaminaCostPerDamage));
        ValidateNonNegative(MinimumGuardStaminaCost, nameof(MinimumGuardStaminaCost));
        ValidateNonNegative(GuardBreakDurationSeconds, nameof(GuardBreakDurationSeconds));
    }

    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateFraction(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
