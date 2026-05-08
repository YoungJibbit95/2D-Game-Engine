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

    private readonly StructurePlacer _placer = new();

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
        _placer.TryPlace(world, origin, SurfaceShelter, StructurePlacementMode.ReplaceAirOnly);
    }
}
