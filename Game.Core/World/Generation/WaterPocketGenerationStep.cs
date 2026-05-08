namespace Game.Core.World.Generation;

public sealed class WaterPocketGenerationStep : IWorldGenerationStep
{
    public string Name => "water";

    public int Order => 25;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var pocketCount = Math.Max(2, world.WidthTiles / 64);

        for (var pocket = 0; pocket < pocketCount; pocket++)
        {
            var x = context.Random.Next(4, Math.Max(5, world.WidthTiles - 4));
            var minY = Math.Min(world.HeightTiles - 6, context.SurfaceHeights[x] + 12);
            if (minY >= world.HeightTiles - 6)
            {
                continue;
            }

            var y = context.Random.Next(minY, world.HeightTiles - 5);
            var radiusX = context.Random.Next(3, 8);
            var radiusY = context.Random.Next(2, 5);
            FillPocket(world, context, x, y, radiusX, radiusY);
        }
    }

    private static void FillPocket(World world, WorldGenerationContext context, int centerX, int centerY, int radiusX, int radiusY)
    {
        for (var y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            for (var x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (!world.IsInBounds(x, y) || y <= context.SurfaceHeights[Math.Clamp(x, 0, world.WidthTiles - 1)] + 6)
                {
                    continue;
                }

                var normalizedX = (x - centerX) / (float)radiusX;
                var normalizedY = (y - centerY) / (float)radiusY;
                if (normalizedX * normalizedX + normalizedY * normalizedY > 1)
                {
                    continue;
                }

                if (world.GetTile(x, y).IsAir)
                {
                    world.SetTile(x, y, TileInstance.Liquid(255));
                }
            }
        }
    }
}
