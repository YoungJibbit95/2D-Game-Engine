namespace Game.Core.World.Generation;

public sealed class SurfaceLakeGenerationStep : IWorldGenerationStep
{
    public string Name => "surface_lakes";

    public int Order => 5;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var profile = context.Profile;
        if (profile.WaterPocketAttempts <= 0 || profile.SurfaceLakeAttempts <= 0 ||
            world.WidthTiles < 9 || world.HeightTiles < 12)
        {
            return;
        }

        var minWidth = Math.Max(5, Math.Min(profile.SurfaceLakeMinWidth, profile.SurfaceLakeMaxWidth));
        var maxWidth = Math.Max(minWidth, Math.Max(profile.SurfaceLakeMinWidth, profile.SurfaceLakeMaxWidth));
        maxWidth = Math.Min(maxWidth, world.WidthTiles - 4);
        minWidth = Math.Min(minWidth, maxWidth);
        if (maxWidth < 5)
        {
            return;
        }

        var minDepth = Math.Max(1, Math.Min(profile.SurfaceLakeMinDepth, profile.SurfaceLakeMaxDepth));
        var maxDepth = Math.Max(minDepth, Math.Max(profile.SurfaceLakeMinDepth, profile.SurfaceLakeMaxDepth));
        var spacing = Math.Max(0, profile.SurfaceLakeMinSpacing);
        var shoreExponent = Math.Max(0.1f, profile.SurfaceLakeShoreExponent);
        var irregularity = Math.Clamp(profile.SurfaceLakeBottomIrregularity, 0f, 1f);
        var occupiedRanges = new List<(int Start, int End)>();

        for (var lake = 0; lake < profile.SurfaceLakeAttempts; lake++)
        {
            for (var placementAttempt = 0; placementAttempt < 12; placementAttempt++)
            {
                var width = NextInclusive(context.Random, minWidth, maxWidth);
                var maxStart = world.WidthTiles - width - 2;
                if (maxStart < 2)
                {
                    break;
                }

                var startX = NextInclusive(context.Random, 2, maxStart);
                var endX = startX + width - 1;
                if (occupiedRanges.Any(range => startX <= range.End + spacing && endX >= range.Start - spacing))
                {
                    continue;
                }

                var depth = NextInclusive(context.Random, minDepth, maxDepth);
                CarveLake(context, startX, endX, depth, shoreExponent, irregularity);
                occupiedRanges.Add((startX, endX));
                break;
            }
        }
    }

    private static void CarveLake(
        WorldGenerationContext context,
        int startX,
        int endX,
        int maxDepth,
        float shoreExponent,
        float irregularity)
    {
        var world = context.World;
        var tiles = context.Tiles;
        var centerX = (startX + endX) / 2;
        var waterLine = Math.Clamp(context.SurfaceHeights[centerX] + 1, 2, world.HeightTiles - 3);
        var width = endX - startX;
        var noiseOffset = context.Random.Next(-100_000, 100_001);

        for (var x = startX + 1; x < endX; x++)
        {
            var progress = (x - startX) / (float)width;
            var shoreShape = MathF.Pow(MathF.Sin(MathF.PI * progress), shoreExponent);
            var noise = context.Noise.GetNoise((x + noiseOffset) * 2.3f, waterLine * 1.7f);
            var depthScale = Math.Clamp(shoreShape * (1f + noise * irregularity), 0f, 1.25f);
            var columnDepth = Math.Max(1, (int)MathF.Round(maxDepth * depthScale));
            var bottomY = Math.Min(
                world.HeightTiles - 1,
                Math.Max(context.SurfaceHeights[x], waterLine + columnDepth));
            var carveTop = Math.Min(context.SurfaceHeights[x], waterLine - 1);

            for (var y = carveTop; y < bottomY; y++)
            {
                if (!tiles.IsInBounds(x, y))
                {
                    continue;
                }

                if (y < waterLine)
                {
                    tiles.RemoveTile(x, y);
                }
                else
                {
                    WorldGenerationTileMutations.SetLiquid(tiles, x, y);
                }
            }
        }
    }

    private static int NextInclusive(Random random, int min, int max)
    {
        return min == max ? min : random.Next(min, max + 1);
    }
}
