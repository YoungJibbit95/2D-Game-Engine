namespace Game.Core.Movement;

public sealed record MobilityAbilityDefinition
{
    public const int MaximumExtraJumpCount = 4;

    public int ExtraJumpCount { get; init; }

    public float AirJumpVelocityMultiplier { get; init; } = 0.9f;

    public bool CanWallJump { get; init; }

    public float FlightDurationSeconds { get; init; }

    public float FlightVerticalSpeedMultiplier { get; init; } = 1f;

    public float FlightAccelerationMultiplier { get; init; } = 1f;

    public bool GlideEnabled { get; init; }

    public float GlideGravityScale { get; init; } = 0.22f;

    public float GlideTerminalVelocity { get; init; } = 125f;

    public bool HasDoubleJump => ExtraJumpCount > 0;

    public bool HasFlight => FlightDurationSeconds > 0f;

    public bool HasGlide => GlideEnabled;

    public void Validate()
    {
        if (ExtraJumpCount is < 0 or > MaximumExtraJumpCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ExtraJumpCount));
        }

        ValidatePositive(AirJumpVelocityMultiplier, nameof(AirJumpVelocityMultiplier));
        ValidateNonNegative(FlightDurationSeconds, nameof(FlightDurationSeconds));
        ValidatePositive(FlightVerticalSpeedMultiplier, nameof(FlightVerticalSpeedMultiplier));
        ValidatePositive(FlightAccelerationMultiplier, nameof(FlightAccelerationMultiplier));
        if (GlideEnabled)
        {
            ValidateRange(GlideGravityScale, 0.01f, 1f, nameof(GlideGravityScale));
            ValidatePositive(GlideTerminalVelocity, nameof(GlideTerminalVelocity));
        }
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateRange(float value, float minimum, float maximum, string name)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public readonly record struct MobilityAbilityProfile(
    int ExtraJumpCount,
    float AirJumpVelocityMultiplier,
    bool CanWallJump,
    float FlightDurationSeconds,
    float FlightVerticalSpeedMultiplier,
    float FlightAccelerationMultiplier,
    bool GlideEnabled,
    float GlideGravityScale,
    float GlideTerminalVelocity)
{
    public static MobilityAbilityProfile Disabled => new(
        0,
        1f,
        false,
        0f,
        1f,
        1f,
        false,
        1f,
        0f);

    public bool HasDoubleJump => ExtraJumpCount > 0;

    public bool HasFlight => FlightDurationSeconds > 0f;

    public bool HasGlide => GlideEnabled;

    public void Validate()
    {
        if (ExtraJumpCount is < 0 or > MobilityAbilityDefinition.MaximumExtraJumpCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ExtraJumpCount));
        }

        ValidatePositive(AirJumpVelocityMultiplier, nameof(AirJumpVelocityMultiplier));
        ValidateNonNegative(FlightDurationSeconds, nameof(FlightDurationSeconds));
        ValidatePositive(FlightVerticalSpeedMultiplier, nameof(FlightVerticalSpeedMultiplier));
        ValidatePositive(FlightAccelerationMultiplier, nameof(FlightAccelerationMultiplier));
        if (GlideEnabled)
        {
            ValidateRange(GlideGravityScale, 0.01f, 1f, nameof(GlideGravityScale));
            ValidatePositive(GlideTerminalVelocity, nameof(GlideTerminalVelocity));
        }
    }

    public static MobilityAbilityProfile FromLegacy(
        SideViewCharacterInput input,
        SideViewCharacterControllerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new MobilityAbilityProfile(
            input.CanDoubleJump ? options.MaxAirJumps : 0,
            options.DoubleJumpVelocityMultiplier,
            input.CanWallJump,
            input.CanFly ? options.FlightDurationSeconds : 0f,
            1f,
            1f,
            input.CanGlide,
            options.GlideGravityScale,
            input.CanGlide ? options.GlideTerminalVelocity : 0f);
    }

    public static MobilityAbilityProfile FromDefinition(MobilityAbilityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        return new MobilityAbilityProfile(
            definition.ExtraJumpCount,
            definition.AirJumpVelocityMultiplier,
            definition.CanWallJump,
            definition.FlightDurationSeconds,
            definition.FlightVerticalSpeedMultiplier,
            definition.FlightAccelerationMultiplier,
            definition.GlideEnabled,
            definition.GlideGravityScale,
            definition.GlideEnabled ? definition.GlideTerminalVelocity : 0f);
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateRange(float value, float minimum, float maximum, string name)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}