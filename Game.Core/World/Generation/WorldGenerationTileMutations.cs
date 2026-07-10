namespace Game.Core.World.Generation;

internal static class WorldGenerationTileMutations
{
    public static void SetLiquid(World world, int x, int y, byte amount = byte.MaxValue)
    {
        var current = world.GetTile(x, y);
        var liquid = TileInstance.Liquid(amount);
        liquid.WallId = current.WallId;
        if (liquid.WallId != 0)
        {
            liquid.Flags |= TileFlags.HasWall;
        }

        world.SetTile(x, y, liquid);
    }

    public static void SetWall(World world, int x, int y, ushort wallId)
    {
        var tile = world.GetTile(x, y);
        tile.WallId = wallId;
        if (wallId == 0)
        {
            tile.Flags &= ~TileFlags.HasWall;
        }
        else
        {
            tile.Flags |= TileFlags.HasWall;
        }

        world.SetTile(x, y, tile);
    }

    public static void SetNaturalTile(World world, int x, int y, ushort tileId)
    {
        var current = world.GetTile(x, y);
        var tile = TileInstance.FromTileId(tileId, TileFlags.IsNatural);
        tile.WallId = current.WallId;
        if (tile.WallId != 0)
        {
            tile.Flags |= TileFlags.HasWall;
        }

        world.SetTile(x, y, tile);
    }
}
