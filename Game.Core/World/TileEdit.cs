namespace Game.Core.World;

public readonly record struct TileEdit(TilePos Position, TileInstance Tile)
{
    public TileEdit(int x, int y, TileInstance tile)
        : this(new TilePos(x, y), tile)
    {
    }

    public static TileEdit Set(int x, int y, ushort tileId)
    {
        return new TileEdit(x, y, TileInstance.FromTileId(tileId));
    }

    public static TileEdit Set(TilePos position, ushort tileId)
    {
        return new TileEdit(position, TileInstance.FromTileId(tileId));
    }

    public static TileEdit Remove(int x, int y)
    {
        return new TileEdit(x, y, TileInstance.Air);
    }

    public static TileEdit Remove(TilePos position)
    {
        return new TileEdit(position, TileInstance.Air);
    }
}
