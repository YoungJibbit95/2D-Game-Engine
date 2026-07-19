namespace Game.Core.World.Generation;

public sealed class TreeGenerationStep : IWorldGenerationStep
{
    internal const int MinimumTreeCenterSpacing = TreeSilhouettePlanner.MaximumHalfWidth * 2 + 1;

    public string Name => "trees";

    public int Order => 30;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var treeChance = Math.Clamp(context.Profile.TreeAttemptChance, 0f, 1f);

        for (var attempt = 0; attempt < Math.Max(0, context.Profile.TreeAttempts); attempt++)
        {
            if (world.WidthTiles <= TreeSilhouettePlanner.MaximumHalfWidth * 2 ||
                context.Random.NextDouble() > treeChance)
            {
                continue;
            }

            var x = context.Random.Next(
                TreeSilhouettePlanner.MaximumHalfWidth,
                world.WidthTiles - TreeSilhouettePlanner.MaximumHalfWidth);
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

        if (HasNearbyTreeCenter(context, x))
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
        if (!HasClearSilhouette(context, x, topY, height, variation))
        {
            return;
        }

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

                var tileId = cell == TreeSilhouetteCell.Trunk ? KnownTileIds.Wood : KnownTileIds.Leaves;
                tiles.SetTile(
                    treeX,
                    treeY,
                    TileInstance.FromTileId(tileId, TileFlags.IsNatural, isSolid: false));
            }
        }
    }

    private static bool HasNearbyTreeCenter(WorldGenerationContext context, int x)
    {
        var firstX = Math.Max(0, x - MinimumTreeCenterSpacing + 1);
        var lastX = Math.Min(context.World.WidthTiles - 1, x + MinimumTreeCenterSpacing - 1);
        for (var candidateX = firstX; candidateX <= lastX; candidateX++)
        {
            if (candidateX == x)
            {
                continue;
            }

            var candidateSurfaceY = context.SurfaceHeights[candidateX];
            if (candidateSurfaceY > 2 &&
                KnownTileIds.IsTreeTrunk(context.Tiles.GetTile(candidateX, candidateSurfaceY - 1).TileId) &&
                KnownTileIds.IsTreeTrunk(context.Tiles.GetTile(candidateX, candidateSurfaceY - 2).TileId) &&
                KnownTileIds.IsTreeTrunk(context.Tiles.GetTile(candidateX, candidateSurfaceY - 3).TileId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasClearSilhouette(
        WorldGenerationContext context,
        int centerX,
        int topY,
        int height,
        int variation)
    {
        for (var dy = -TreeSilhouettePlanner.TopPadding; dy <= height - 1; dy++)
        {
            for (var dx = -TreeSilhouettePlanner.MaximumHalfWidth;
                 dx <= TreeSilhouettePlanner.MaximumHalfWidth;
                 dx++)
            {
                if (TreeSilhouettePlanner.Classify(
                        dx,
                        dy,
                        height,
                        variation,
                        context.World.Metadata.GenerationVersion) == TreeSilhouetteCell.Empty)
                {
                    continue;
                }

                var treeX = centerX + dx;
                var treeY = topY + dy;
                if (!context.Tiles.IsInBounds(treeX, treeY) || !context.Tiles.GetTile(treeX, treeY).IsAir)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
