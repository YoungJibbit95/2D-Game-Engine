namespace Game.Core.World.Generation;

public static class StructureTemplateMaterializer
{
    public static bool TryResolveTile(
        PlannedStructure structure,
        long tileX,
        int tileY,
        int originTileY,
        out string tileId)
    {
        ArgumentNullException.ThrowIfNull(structure);

        tileId = string.Empty;
        if (!structure.HasMaterializedTemplate)
        {
            return false;
        }

        var localX = tileX - structure.TileX;
        var localY = (long)tileY - originTileY;
        if (localX < 0 || localX >= structure.WidthTiles ||
            localY < 0 || localY >= structure.HeightTiles)
        {
            return false;
        }

        var symbol = structure.Rows[(int)localY][(int)localX];
        if (symbol == structure.TransparentSymbol)
        {
            return false;
        }

        return structure.Legend.TryGetValue(symbol.ToString(), out tileId!);
    }
}
