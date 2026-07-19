namespace Game.Core.Movement;

public sealed record TopDownMovementOptions
{
    public static TopDownMovementOptions Default { get; } = new();

    public float MoveSpeedPixelsPerSecond { get; init; } = 96f;

    public bool AllowDiagonalMovement { get; init; } = true;

    public bool NormalizeDiagonalSpeed { get; init; } = true;
}
