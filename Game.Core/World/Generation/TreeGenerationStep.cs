namespace Game.Core.World.Generation;

public sealed class TreeGenerationStep : IWorldGenerationStep
{
    public string Name => "trees";

    public int Order => 30;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;

        for (var x = 3; x < world.WidthTiles - 3; x += context.Random.Next(5, 10))
        {
            if (context.Random.NextDouble() > 0.65)
            {
                continue;
            }

            TryPlaceTree(context, x);
        }
    }

    private static void TryPlaceTree(WorldGenerationContext context, int x)
    {
        var world = context.World;
        var surfaceY = context.SurfaceHeights[x];

        if (surfaceY <= 5 || world.GetTile(x, surfaceY).TileId != KnownTileIds.Grass)
        {
            return;
        }

        var height = context.Random.Next(4, 8);
        for (var y = surfaceY - height; y < surfaceY; y++)
        {
            if (!world.IsInBounds(x, y) || !world.GetTile(x, y).IsAir)
            {
                return;
            }
        }

        for (var y = surfaceY - height; y < surfaceY; y++)
        {
            world.SetTile(x, y, TileInstance.FromTileId(KnownTileIds.Wood, TileFlags.IsNatural));
        }

        PlaceLeaves(world, x, surfaceY - height);
    }

    private static void PlaceLeaves(World world, int centerX, int centerY)
    {
        for (var y = centerY - 2; y <= centerY + 1; y++)
        {
            for (var x = centerX - 2; x <= centerX + 2; x++)
            {
                if (!world.IsInBounds(x, y) || !world.GetTile(x, y).IsAir)
                {
                    continue;
                }

                var dx = Math.Abs(x - centerX);
                var dy = Math.Abs(y - centerY);
                if (dx + dy <= 3)
                {
                    world.SetTile(x, y, TileInstance.FromTileId(KnownTileIds.Leaves, TileFlags.IsNatural));
                }
            }
        }
    }
}
