namespace Game.Core.World.Generation;

public sealed class CaveWallCleanupStep : IWorldGenerationStep
{
    public string Name => "cave_wall_cleanup";

    public int Order => 25;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var passes = Math.Max(0, context.Profile.CaveWallCleanupPasses);
        var minWallNeighbors = Math.Clamp(context.Profile.CaveWallCleanupMinNeighbors, 0, 8);
        var openNeighborThreshold = Math.Clamp(context.Profile.CaveWallCoreOpenNeighborThreshold, 0, 8);

        for (var pass = 0; pass < passes; pass++)
        {
            var toClear = new List<TilePos>();
            for (var x = 0; x < world.WidthTiles; x++)
            {
                var startY = Math.Clamp(
                    context.SurfaceHeights[x] + Math.Max(0, context.Profile.UndergroundWallStartDepthOffset),
                    0,
                    world.HeightTiles);

                for (var y = startY; y < world.HeightTiles; y++)
                {
                    var tile = world.GetTile(x, y);
                    if (tile.IsSolid || tile.WallId == 0)
                    {
                        continue;
                    }

                    CountNeighbors(world, x, y, out var wallNeighbors, out var openNeighbors);
                    if (wallNeighbors < minWallNeighbors || openNeighbors >= openNeighborThreshold)
                    {
                        toClear.Add(new TilePos(x, y));
                    }
                }
            }

            if (toClear.Count == 0)
            {
                break;
            }

            foreach (var position in toClear)
            {
                WorldGenerationTileMutations.SetWall(world, position.X, position.Y, 0);
            }
        }
    }

    private static void CountNeighbors(World world, int centerX, int centerY, out int wallNeighbors, out int openNeighbors)
    {
        wallNeighbors = 0;
        openNeighbors = 0;

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if ((offsetX == 0 && offsetY == 0) || !world.IsInBounds(centerX + offsetX, centerY + offsetY))
                {
                    continue;
                }

                var neighbor = world.GetTile(centerX + offsetX, centerY + offsetY);
                if (neighbor.WallId != 0)
                {
                    wallNeighbors++;
                }

                if (!neighbor.IsSolid)
                {
                    openNeighbors++;
                }
            }
        }
    }
}
