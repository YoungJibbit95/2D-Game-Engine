namespace Game.Core.World.Generation;

public sealed class CaveGenerationStep : IWorldGenerationStep
{
    public string Name => "caves";

    public int Order => 10;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var walkCount = Math.Max(4, world.WidthTiles / 32);
        var stepsPerWalk = Math.Max(50, world.HeightTiles * 2);

        for (var walk = 0; walk < walkCount; walk++)
        {
            var x = context.Random.Next(4, Math.Max(5, world.WidthTiles - 4));
            var minY = Math.Min(world.HeightTiles - 4, context.SurfaceHeights[x] + 8);
            if (minY >= world.HeightTiles - 4)
            {
                continue;
            }

            var y = context.Random.Next(minY, world.HeightTiles - 3);
            var radius = context.Random.Next(1, 3);

            for (var step = 0; step < stepsPerWalk; step++)
            {
                CarveCircle(world, x, y, radius);
                x = Math.Clamp(x + context.Random.Next(-1, 2), 2, world.WidthTiles - 3);
                y = Math.Clamp(y + context.Random.Next(-1, 2), context.SurfaceHeights[x] + 5, world.HeightTiles - 3);

                if (context.Random.NextDouble() < 0.08)
                {
                    radius = context.Random.Next(1, 3);
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
