namespace Game.Core.World.Generation;

public sealed class TerrainGenerationStep : IWorldGenerationStep
{
    public string Name => "terrain";

    public int Order => 0;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var profile = context.Profile;
        var baseSurfaceY = Math.Clamp(profile.SurfaceBaseY, 6, world.HeightTiles - 10);
        var amplitude = Math.Max(1, profile.SurfaceAmplitude);
        var dirtDepthMin = Math.Max(1, Math.Min(profile.DirtDepthMin, profile.DirtDepthMax));
        var dirtDepthMax = Math.Max(dirtDepthMin, Math.Max(profile.DirtDepthMin, profile.DirtDepthMax));

        for (var x = 0; x < world.WidthTiles; x++)
        {
            var surfaceNoise = context.Noise.GetNoise(x, 0);
            var surfaceY = baseSurfaceY + (int)MathF.Round(surfaceNoise * amplitude);
            surfaceY = Math.Clamp(surfaceY, Math.Max(3, world.HeightTiles / 6), Math.Max(4, world.HeightTiles / 2));
            context.SurfaceHeights[x] = surfaceY;

            var dirtDepth = dirtDepthMin + Math.Abs(StableHash(context.Seed, x) % (dirtDepthMax - dirtDepthMin + 1));
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
