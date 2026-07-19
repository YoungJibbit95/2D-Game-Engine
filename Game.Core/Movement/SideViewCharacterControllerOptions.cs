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

    public float JumpSpeed { get; init; } = 365f;

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
    public float JumpReleaseVelocityMultiplier { get; init; } = 0.45f;
}
