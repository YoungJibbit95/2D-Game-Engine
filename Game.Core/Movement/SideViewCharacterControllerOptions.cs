namespace Game.Core.Movement;

/// <summary>
/// Fixed-step side-view locomotion tuning. Physics owns gravity, forces,
/// integration and collision; these values only define control intent.
/// </summary>
public sealed record SideViewCharacterControllerOptions
{
    public static SideViewCharacterControllerOptions Default { get; } = new();

    public float GroundAcceleration { get; init; } = 2600f;

    public float AirAcceleration { get; init; } = 2600f;

    public float GroundFriction { get; init; } = 1800f;

    public float AirFriction { get; init; } = 1800f;

    public float MaxGroundSpeed { get; init; } = 145f;

    public float JumpSpeed { get; init; } = 390f;

    /// <summary>
    /// Grace period after leaving a ledge during which a newly pressed jump still starts.
    /// </summary>
    public float CoyoteTimeSeconds { get; init; } = 0.10f;

    /// <summary>
    /// Grace period before landing during which a newly pressed jump is remembered.
    /// </summary>
    public float JumpBufferSeconds { get; init; } = 0.12f;

    /// <summary>
    /// Multiplier applied to upward velocity when jump is released early.
    /// A value of one disables variable-height jumping.
    /// </summary>
    public float JumpReleaseVelocityMultiplier { get; init; } = 0.5f;

    /// <summary>
    /// Number of accessory-provided jumps available while airborne. Ground and
    /// coyote jumps do not consume this pool.
    /// </summary>
    public int MaxAirJumps { get; init; } = 1;

    public float DoubleJumpVelocityMultiplier { get; init; } = 0.9f;

    public float WallJumpHorizontalSpeed { get; init; } = 140f;

    public float WallJumpVerticalSpeed { get; init; } = 390f;

    public float GlideGravityScale { get; init; } = 0.22f;

    public float GlideTerminalVelocity { get; init; } = 125f;

    public float FlyGravityScale { get; init; } = 0.05f;

    /// <summary>
    /// Maximum continuous flight time before the character must touch ground.
    /// </summary>
    public float FlightDurationSeconds { get; init; } = 1.8f;

    public float FlyVerticalSpeed { get; init; } = 240f;

    public float FlyAscendAcceleration { get; init; } = 2200f;

    public float FlyDescentHoldSpeed { get; init; } = 55f;
}
