using Game.Core.World.Structures;

namespace Game.Core.World.Generation;

public sealed class StructureGenerationStep : IWorldGenerationStep
{
    private static readonly StructureTemplate SurfaceShelter = StructureTemplate.FromRows(
        new[]
        {
            "WWWWW",
            "W...W",
            "W...W",
            "WWWWW"
        },
        new Dictionary<char, ushort>
        {
            ['W'] = KnownTileIds.Wood
        });

    public string Name => "structures";

    public int Order => 35;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        if (world.WidthTiles < 80 || world.HeightTiles < 32)
        {
            return;
        }

        var targetX = Math.Clamp(world.WidthTiles / 4 + Math.Abs(context.Seed % 13), 4, world.WidthTiles - SurfaceShelter.Width - 4);
        var surfaceY = context.SurfaceHeights[targetX];
        var origin = new TilePos(targetX, Math.Max(1, surfaceY - SurfaceShelter.Height));
        TryPlace(context.Tiles, origin, SurfaceShelter, StructurePlacementMode.ReplaceAirOnly);
    }

    private static bool TryPlace(
        WorldGenerationWorkspace tiles,
        TilePos origin,
        StructureTemplate template,
        StructurePlacementMode mode)
    {
        if (origin.Y < 0 || origin.Y + template.Height > tiles.World.HeightTiles ||
            origin.X < 0 || origin.X + template.Width > tiles.World.WidthTiles)
        {
            return false;
        }

        if (mode != StructurePlacementMode.ReplaceAny)
        {
            for (var y = 0; y < template.Height; y++)
            {
                for (var x = 0; x < template.Width; x++)
                {
                    if (template.GetTile(x, y) is not null &&
                        !tiles.GetTile(origin.X + x, origin.Y + y).IsAir)
                    {
                        return false;
                    }
                }
            }
        }

        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
            {
                var tileId = template.GetTile(x, y);
                if (tileId is not null)
                {
                    tiles.SetTile(
                        origin.X + x,
                        origin.Y + y,
                        TileInstance.FromTileId(tileId.Value, TileFlags.IsNatural));
                }
            }
        }

        return true;
    }
}
