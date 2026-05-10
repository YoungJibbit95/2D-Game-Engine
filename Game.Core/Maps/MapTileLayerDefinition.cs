namespace Game.Core.Maps;

public sealed record MapTileLayerDefinition
{
    public required string Id { get; init; }

    public MapLayerKind Kind { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int ZIndex { get; init; }

    public bool IsVisible { get; init; } = true;

    public bool BlocksMovement { get; init; }

    public IReadOnlyList<int> Tiles { get; init; } = Array.Empty<int>();

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public int GetTileId(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            return 0;
        }

        return Tiles[y * Width + x];
    }

    public bool BlocksAt(int x, int y)
    {
        return BlocksMovement && GetTileId(x, y) != 0;
    }
}
