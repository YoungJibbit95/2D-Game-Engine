namespace Game.Core.Animations;

public sealed record SpriteAnimationFrame
{
    public required string SpriteId { get; init; }

    public int FrameIndex { get; init; }

    public float DurationSeconds { get; init; } = 0.1f;

    public int OffsetX { get; init; }

    public int OffsetY { get; init; }

    public string? EventId { get; init; }
}
