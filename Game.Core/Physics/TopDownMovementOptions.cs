namespace Game.Core.Physics;

public sealed record TopDownMovementOptions
{
    public float MoveSpeedPixelsPerSecond { get; init; } = 96f;

    public bool AllowDiagonalMovement { get; init; } = true;

    public bool NormalizeDiagonalSpeed { get; init; } = true;
}
