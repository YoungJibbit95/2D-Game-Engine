namespace Game.Core.Assets;

public sealed record SpriteFrameDefinition
{
    public string? Id { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int OriginX { get; init; }

    public int OriginY { get; init; }

    public int DurationMs { get; init; }

    public int? AutoTileMask { get; init; }
}
