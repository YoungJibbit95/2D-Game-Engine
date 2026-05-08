namespace Game.Core.World.Generation;

public sealed class OreGenerationStep : IWorldGenerationStep
{
    public string Name => "ores";

    public int Order => 20;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        PlaceOre(world, context, KnownTileIds.CopperOre, veinCount: Math.Max(8, world.WidthTiles / 8), radius: 2, minDepthOffset: 8);
        PlaceOre(world, context, KnownTileIds.IronOre, veinCount: Math.Max(5, world.WidthTiles / 12), radius: 2, minDepthOffset: 16);
    }

    private static void PlaceOre(World world, WorldGenerationContext context, ushort oreTileId, int veinCount, int radius, int minDepthOffset)
    {
        for (var vein = 0; vein < veinCount; vein++)
        {
            var x = context.Random.Next(2, Math.Max(3, world.WidthTiles - 2));
            var minY = Math.Min(world.HeightTiles - 3, context.SurfaceHeights[x] + minDepthOffset);
            if (minY >= world.HeightTiles - 3)
            {
                continue;
            }

            var y = context.Random.Next(minY, world.HeightTiles - 2);
            var length = context.Random.Next(5, 12);

            for (var step = 0; step < length; step++)
            {
                PlaceOreBlob(world, x, y, oreTileId, radius);
                x = Math.Clamp(x + context.Random.Next(-1, 2), 1, world.WidthTiles - 2);
                y = Math.Clamp(y + context.Random.Next(-1, 2), context.SurfaceHeights[x] + minDepthOffset, world.HeightTiles - 2);
            }
        }
    }

    private static void PlaceOreBlob(World world, int centerX, int centerY, ushort oreTileId, int radius)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (!world.IsInBounds(x, y) || world.GetTile(x, y).TileId != KnownTileIds.Stone)
                {
                    continue;
                }

                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    world.SetTile(x, y, TileInstance.FromTileId(oreTileId, TileFlags.IsNatural));
                }
            }
        }
    }
}
