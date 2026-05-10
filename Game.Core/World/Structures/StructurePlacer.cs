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

        var edits = new List<TileEdit>(template.Width * template.Height);
        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
            {
                var tileId = template.GetTile(x, y);
                if (tileId is null)
                {
                    continue;
                }

                edits.Add(new TileEdit(origin.X + x, origin.Y + y, TileInstance.FromTileId(tileId.Value, flags)));
            }
        }

        var result = world.ApplyTileEdits(edits);
        return new StructurePlacementResult(true, result.ChangedTiles, result.ChangedBounds, result.DirtyChunks);
    }

    public bool CanPlace(World world, TilePos origin, StructureTemplate template, StructurePlacementMode mode)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(template);

        if (origin.Y < 0 || origin.Y + template.Height > world.HeightTiles)
        {
            return false;
        }

        if (!world.IsHorizontallyInfinite && (origin.X < 0 || origin.X + template.Width > world.WidthTiles))
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

                var target = new TilePos(origin.X + x, origin.Y + y);
                if (!world.IsInBounds(target.X, target.Y) || !world.GetTile(target.X, target.Y).IsAir)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
