namespace Game.Core.World.Structures;

public sealed class StructurePlacer
{
    public StructurePlacementResult TryPlace(
        World world,
        TilePos origin,
        StructureTemplate template,
        StructurePlacementMode mode = StructurePlacementMode.ReplaceAny,
        TileFlags flags = TileFlags.IsNatural)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(template);

        if (!CanPlace(world, origin, template, mode))
        {
            return StructurePlacementResult.Failed;
        }

        var written = 0;
        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
            {
                var tileId = template.GetTile(x, y);
                if (tileId is null)
                {
                    continue;
                }

                world.SetTile(origin.X + x, origin.Y + y, TileInstance.FromTileId(tileId.Value, flags));
                written++;
            }
        }

        return new StructurePlacementResult(true, written);
    }

    public bool CanPlace(World world, TilePos origin, StructureTemplate template, StructurePlacementMode mode)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(template);

        if (origin.X < 0 || origin.Y < 0 || origin.X + template.Width > world.WidthTiles || origin.Y + template.Height > world.HeightTiles)
        {
            return false;
        }

        if (mode == StructurePlacementMode.ReplaceAny)
        {
            return true;
        }

        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
            {
                if (template.GetTile(x, y) is null)
                {
                    continue;
                }

                if (!world.GetTile(origin.X + x, origin.Y + y).IsAir)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
