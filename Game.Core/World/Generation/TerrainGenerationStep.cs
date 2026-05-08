namespace Game.Core.World.Generation;

public sealed class TerrainGenerationStep : IWorldGenerationStep
{
    public string Name => "terrain";

    public int Order => 0;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var baseSurfaceY = Math.Clamp(world.HeightTiles / 3, 6, world.HeightTiles - 10);
        var amplitude = Math.Max(3, world.HeightTiles / 10);

        for (var x = 0; x < world.WidthTiles; x++)
        {
            var surfaceNoise = context.Noise.GetNoise(x, 0);
            var surfaceY = baseSurfaceY + (int)MathF.Round(surfaceNoise * amplitude);
            surfaceY = Math.Clamp(surfaceY, Math.Max(3, world.HeightTiles / 6), Math.Max(4, world.HeightTiles / 2));
            context.SurfaceHeights[x] = surfaceY;

            var dirtDepth = 4 + Math.Abs(StableHash(context.Seed, x) % 5);
            for (var y = surfaceY; y < world.HeightTiles; y++)
            {
                var tileId = y == surfaceY
                    ? KnownTileIds.Grass
                    : y < surfaceY + dirtDepth
                        ? KnownTileIds.Dirt
                        : KnownTileIds.Stone;

                world.SetTile(x, y, TileInstance.FromTileId(tileId, TileFlags.IsNatural));
            }
        }
    }

    private static int StableHash(int seed, int x)
    {
        unchecked
        {
            var hash = seed;
            hash = (hash * 397) ^ x;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            return hash;
        }
    }
}
