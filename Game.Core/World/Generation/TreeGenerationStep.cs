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
        var tiles = context.Tiles;
        var surfaceY = context.SurfaceHeights[x];

        if (surfaceY <= 5 || tiles.GetTile(x, surfaceY).TileId != KnownTileIds.Grass)
        {
            return;
        }

        var minHeight = Math.Max(1, Math.Min(context.Profile.TreeMinHeight, context.Profile.TreeMaxHeight));
        var maxHeight = Math.Max(minHeight, Math.Max(context.Profile.TreeMinHeight, context.Profile.TreeMaxHeight));
        var height = context.Random.Next(minHeight, maxHeight + 1);
        var variation = context.Random.Next();
        for (var y = surfaceY - height; y < surfaceY; y++)
        {
            if (!tiles.IsInBounds(x, y) || !tiles.GetTile(x, y).IsAir)
            {
                return;
            }
        }

        var topY = surfaceY - height;
        for (var dy = -TreeSilhouettePlanner.TopPadding; dy <= height - 1; dy++)
        {
            for (var dx = -TreeSilhouettePlanner.MaximumHalfWidth;
                 dx <= TreeSilhouettePlanner.MaximumHalfWidth;
                 dx++)
            {
                var treeX = x + dx;
                var treeY = topY + dy;
                if (!tiles.IsInBounds(treeX, treeY))
                {
                    continue;
                }

                var cell = TreeSilhouettePlanner.Classify(
                    dx,
                    dy,
                    height,
                    variation,
                    context.World.Metadata.GenerationVersion);
                if (cell == TreeSilhouetteCell.Empty)
                {
                    continue;
                }

                var existing = tiles.GetTile(treeX, treeY);
                if (!existing.IsAir && !(cell == TreeSilhouetteCell.Trunk && KnownTileIds.IsFoliage(existing.TileId)))
                {
                    continue;
                }

                var tileId = cell == TreeSilhouetteCell.Trunk ? KnownTileIds.Wood : KnownTileIds.Leaves;
                tiles.SetTile(
                    treeX,
                    treeY,
                    TileInstance.FromTileId(tileId, TileFlags.IsNatural, isSolid: false));
            }
        }
    }
}
