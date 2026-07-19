namespace Game.Core.World.Generation;

internal static class WorldGenerationTileMutations
{
    public static void SetLiquid(WorldGenerationWorkspace tiles, int x, int y, byte amount = byte.MaxValue)
    {
        var current = tiles.GetTile(x, y);
        var liquid = TileInstance.Liquid(amount);
        liquid.WallId = current.WallId;
        if (liquid.WallId != 0)
        {
            liquid.Flags |= TileFlags.HasWall;
        }

        tiles.SetTile(x, y, liquid);
    }

    public static void SetWall(WorldGenerationWorkspace tiles, int x, int y, ushort wallId)
    {
        var tile = tiles.GetTile(x, y);
        tile.WallId = wallId;
        if (wallId == 0)
        {
            tile.Flags &= ~TileFlags.HasWall;
        }
        else
        {
            tile.Flags |= TileFlags.HasWall;
        }

        tiles.SetTile(x, y, tile);
    }

    public static void SetNaturalTile(WorldGenerationWorkspace tiles, int x, int y, ushort tileId)
    {
        var current = tiles.GetTile(x, y);
        var tile = TileInstance.FromTileId(tileId, TileFlags.IsNatural);
        tile.WallId = current.WallId;
        if (tile.WallId != 0)
        {
            tile.Flags |= TileFlags.HasWall;
        }

        tiles.SetTile(x, y, tile);
    }
}
