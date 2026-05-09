namespace Game.Core.World.Generation;

public sealed class OreGenerationStep : IWorldGenerationStep
{
    public string Name => "ores";

    public int Order => 20;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;

        foreach (var ore in GetOreDefinitions(context.Profile))
        {
            if (!ore.CanGenerate)
            {
                continue;
            }

            PlaceOre(world, context, ore);
        }
    }

    private static IReadOnlyList<OreGenerationDefinition> GetOreDefinitions(WorldGenerationProfile profile)
    {
        if (profile.Ores.Count > 0)
        {
            return profile.Ores;
        }

        return new[]
        {
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.CopperOre,
                VeinCount = Math.Max(0, profile.CopperVeinCount),
                MinDepthOffset = 8,
                Radius = 2
            },
            new OreGenerationDefinition
            {
                TileId = KnownTileIds.IronOre,
                VeinCount = Math.Max(0, profile.IronVeinCount),
                MinDepthOffset = 16,
                Radius = 2
            }
        };
    }

    private static void PlaceOre(World world, WorldGenerationContext context, OreGenerationDefinition ore)
    {
        var radius = Math.Max(1, ore.Radius);
        var minDepthOffset = Math.Max(0, ore.MinDepthOffset);
        var minLength = Math.Max(1, Math.Min(ore.MinLength, ore.MaxLength));
        var maxLength = Math.Max(minLength, Math.Max(ore.MinLength, ore.MaxLength));

        for (var vein = 0; vein < Math.Max(0, ore.VeinCount); vein++)
        {
            var x = context.Random.Next(2, Math.Max(3, world.WidthTiles - 2));
            var surfaceY = context.SurfaceHeights[x];
            var minY = Math.Min(world.HeightTiles - 3, surfaceY + minDepthOffset);
            var maxY = ore.MaxDepthOffset > 0
                ? Math.Min(world.HeightTiles - 2, surfaceY + ore.MaxDepthOffset)
                : world.HeightTiles - 2;

            maxY = Math.Max(minY + 1, maxY);
            if (minY >= world.HeightTiles - 3)
            {
                continue;
            }

            var y = context.Random.Next(minY, maxY);
            var length = context.Random.Next(minLength, maxLength + 1);

            for (var step = 0; step < length; step++)
            {
                PlaceOreBlob(world, x, y, ore, radius);
                x = Math.Clamp(x + context.Random.Next(-1, 2), 1, world.WidthTiles - 2);
                var nextMinY = Math.Min(world.HeightTiles - 3, context.SurfaceHeights[x] + minDepthOffset);
                var nextMaxY = ore.MaxDepthOffset > 0
                    ? Math.Min(world.HeightTiles - 2, context.SurfaceHeights[x] + ore.MaxDepthOffset)
                    : world.HeightTiles - 2;
                y = Math.Clamp(y + context.Random.Next(-1, 2), nextMinY, Math.Max(nextMinY + 1, nextMaxY));
            }
        }
    }

    private static void PlaceOreBlob(World world, int centerX, int centerY, OreGenerationDefinition ore, int radius)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (!world.IsInBounds(x, y) || world.GetTile(x, y).TileId != ore.ReplaceTileId)
                {
                    continue;
                }

                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    world.SetTile(x, y, TileInstance.FromTileId(ore.TileId, TileFlags.IsNatural));
                }
            }
        }
    }
}
