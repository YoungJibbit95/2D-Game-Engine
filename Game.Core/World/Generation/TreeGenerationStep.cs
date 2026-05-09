namespace Game.Core.World.Generation;

public sealed class TreeGenerationStep : IWorldGenerationStep
{
    public string Name => "trees";

    public int Order => 30;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var treeChance = Math.Clamp(context.Profile.TreeAttemptChance, 0f, 1f);

        for (var attempt = 0; attempt < Math.Max(0, context.Profile.TreeAttempts); attempt++)
        {
            if (world.WidthTiles <= 6 || context.Random.NextDouble() > treeChance)
            {
                continue;
            }

            var x = context.Random.Next(3, world.WidthTiles - 3);
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

        var minHeight = Math.Max(1, Math.Min(context.Profile.TreeMinHeight, context.Profile.TreeMaxHeight));
        var maxHeight = Math.Max(minHeight, Math.Max(context.Profile.TreeMinHeight, context.Profile.TreeMaxHeight));
        var height = context.Random.Next(minHeight, maxHeight + 1);
        for (var y = surfaceY - height; y < surfaceY; y++)
        {
            if (!world.IsInBounds(x, y) || !world.GetTile(x, y).IsAir)
            {
                return;
            }
        }

        for (var y = surfaceY - height; y < surfaceY; y++)
        {
            world.SetTile(x, y, TileInstance.FromTileId(KnownTileIds.Wood, TileFlags.IsNatural, isSolid: false));
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
                    world.SetTile(x, y, TileInstance.FromTileId(KnownTileIds.Leaves, TileFlags.IsNatural, isSolid: false));
                }
            }
        }
    }
}
