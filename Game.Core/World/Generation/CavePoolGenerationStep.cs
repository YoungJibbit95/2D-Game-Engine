namespace Game.Core.World.Generation;

public sealed class CavePoolGenerationStep : IWorldGenerationStep
{
    private readonly int? _attemptsOverride;

    public CavePoolGenerationStep()
    {
    }

    internal CavePoolGenerationStep(int attemptsOverride)
    {
        _attemptsOverride = attemptsOverride;
    }

    public string Name => "cave_pools";

    public int Order => 16;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var profile = context.Profile;
        var configuredAttempts = profile.CavePoolAttempts > 0 || profile.SurfaceLakeAttempts > 0
            ? profile.CavePoolAttempts
            : profile.WaterPocketAttempts;
        var poolAttempts = Math.Max(0, _attemptsOverride ?? configuredAttempts);
        if (profile.WaterPocketAttempts <= 0 || poolAttempts <= 0 ||
            world.WidthTiles < 7 || world.HeightTiles < 10)
        {
            return;
        }

        var minWidth = Math.Max(5, Math.Min(profile.CavePoolMinWidth, profile.CavePoolMaxWidth));
        var maxWidth = Math.Max(minWidth, Math.Max(profile.CavePoolMinWidth, profile.CavePoolMaxWidth));
        maxWidth = Math.Min(maxWidth, world.WidthTiles - 4);
        minWidth = Math.Min(minWidth, maxWidth);
        var minDepth = Math.Max(1, Math.Min(profile.CavePoolMinDepth, profile.CavePoolMaxDepth));
        var maxDepth = Math.Max(minDepth, Math.Max(profile.CavePoolMinDepth, profile.CavePoolMaxDepth));
        var candidates = FindFloorCandidates(context, maxDepth);
        if (candidates.Count == 0)
        {
            return;
        }

        var placedRanges = new List<(int Start, int End, int Y)>();
        for (var pool = 0; pool < poolAttempts && candidates.Count > 0; pool++)
        {
            var candidateIndex = context.Random.Next(candidates.Count);
            var candidate = candidates[candidateIndex];
            candidates.RemoveAt(candidateIndex);
            var width = NextInclusive(context.Random, minWidth, maxWidth);
            var depth = NextInclusive(context.Random, minDepth, maxDepth);
            var startX = Math.Max(1, candidate.X - width / 2);
            var endX = Math.Min(world.WidthTiles - 2, startX + width - 1);
            startX = Math.Max(1, endX - width + 1);
            if (placedRanges.Any(range => Math.Abs(range.Y - candidate.FloorY) <= maxDepth && startX <= range.End + 3 && endX >= range.Start - 3))
            {
                pool--;
                continue;
            }

            CarvePool(context, startX, endX, candidate.FloorY, depth);
            placedRanges.Add((startX, endX, candidate.FloorY));
        }
    }

    private static List<PoolCandidate> FindFloorCandidates(WorldGenerationContext context, int maxDepth)
    {
        var world = context.World;
        var candidates = new List<PoolCandidate>();
        var depthOffset = Math.Max(0, context.Profile.CavePoolMinDepthOffset);
        var searchDistance = Math.Max(4, maxDepth * 3);

        for (var x = 2; x < world.WidthTiles - 2; x += 2)
        {
            var minY = Math.Clamp(context.SurfaceHeights[x] + depthOffset, 1, world.HeightTiles - 2);
            for (var y = minY; y < world.HeightTiles - 2; y++)
            {
                if (!world.GetTile(x, y).IsAir)
                {
                    continue;
                }

                var floorY = FindFloor(world, x, y, searchDistance);
                if (floorY > y + 1 && floorY < world.HeightTiles - 1)
                {
                    candidates.Add(new PoolCandidate(x, floorY));
                    y = floorY;
                }
            }
        }

        return candidates;
    }

    private static int FindFloor(World world, int x, int startY, int maxDistance)
    {
        var endY = Math.Min(world.HeightTiles - 1, startY + maxDistance);
        for (var y = startY + 1; y <= endY; y++)
        {
            if (world.GetTile(x, y).IsSolid)
            {
                return y;
            }
        }

        return -1;
    }

    private static void CarvePool(WorldGenerationContext context, int startX, int endX, int floorY, int maxDepth)
    {
        var world = context.World;
        var exponent = Math.Max(0.1f, context.Profile.CavePoolBasinExponent);
        var irregularity = Math.Clamp(context.Profile.CavePoolBottomIrregularity, 0f, 1f);
        var waterLine = Math.Max(1, floorY - maxDepth);
        var width = Math.Max(1, endX - startX);
        var noiseOffset = context.Random.Next(-100_000, 100_001);

        for (var x = startX + 1; x < endX; x++)
        {
            var progress = (x - startX) / (float)width;
            var basinShape = MathF.Pow(MathF.Sin(MathF.PI * progress), exponent);
            var noise = context.Noise.GetNoise((x - noiseOffset) * 2.7f, floorY * 2.2f);
            var depthScale = Math.Clamp(basinShape * (1f + noise * irregularity), 0f, 1.25f);
            var columnDepth = Math.Max(1, (int)MathF.Round(maxDepth * depthScale));
            var localFloorY = FindFloor(world, x, waterLine - 1, maxDepth * 4 + 8);
            if (localFloorY < 0)
            {
                continue;
            }

            var bottomY = Math.Min(world.HeightTiles - 1, waterLine + columnDepth);
            if (!world.GetTile(x, bottomY).IsSolid)
            {
                WorldGenerationTileMutations.SetNaturalTile(world, x, bottomY, KnownTileIds.Stone);
            }

            for (var y = waterLine; y < bottomY; y++)
            {
                if (world.IsInBounds(x, y) && y > context.SurfaceHeights[x] + 2)
                {
                    WorldGenerationTileMutations.SetLiquid(world, x, y);
                }
            }
        }
    }

    private static int NextInclusive(Random random, int min, int max)
    {
        return min == max ? min : random.Next(min, max + 1);
    }

    private readonly record struct PoolCandidate(int X, int FloorY);
}
