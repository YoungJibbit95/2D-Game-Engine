namespace Game.Core.World.Generation;

public sealed class CaveGenerationStep : IWorldGenerationStep
{
    public string Name => "caves";

    public int Order => 10;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var walkCount = Math.Max(0, context.Profile.CaveWalkerCount);
        var stepsPerWalk = Math.Max(0, context.Profile.CaveWalkLength);
        var minDepthOffset = Math.Max(0, context.Profile.CaveMinDepthOffset);
        var clampDepthOffset = Math.Max(0, context.Profile.CaveClampDepthOffset);
        var minRadius = Math.Max(1, Math.Min(context.Profile.CaveMinRadius, context.Profile.CaveMaxRadius));
        var maxRadius = Math.Max(minRadius, Math.Max(context.Profile.CaveMinRadius, context.Profile.CaveMaxRadius));
        var radiusChangeChance = Math.Clamp(context.Profile.CaveRadiusChangeChance, 0f, 1f);

        for (var walk = 0; walk < walkCount; walk++)
        {
            var x = context.Random.Next(4, Math.Max(5, world.WidthTiles - 4));
            var minY = Math.Min(world.HeightTiles - 4, context.SurfaceHeights[x] + minDepthOffset);
            if (minY >= world.HeightTiles - 4)
            {
                continue;
            }

            var y = context.Random.Next(minY, world.HeightTiles - 3);
            var radius = context.Random.Next(minRadius, maxRadius + 1);

            for (var step = 0; step < stepsPerWalk; step++)
            {
                CarveCircle(world, x, y, radius);
                x = Math.Clamp(x + context.Random.Next(-1, 2), 2, world.WidthTiles - 3);
                y = Math.Clamp(y + context.Random.Next(-1, 2), context.SurfaceHeights[x] + clampDepthOffset, world.HeightTiles - 3);

                if (context.Random.NextDouble() < radiusChangeChance)
                {
                    radius = context.Random.Next(minRadius, maxRadius + 1);
                }
            }
        }
    }

    private static void CarveCircle(World world, int centerX, int centerY, int radius)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (!world.IsInBounds(x, y))
                {
                    continue;
                }

                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    world.RemoveTile(x, y);
                }
            }
        }
    }
}
