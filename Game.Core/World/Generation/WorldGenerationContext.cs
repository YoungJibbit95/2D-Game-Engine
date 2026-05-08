using Game.Core.Biomes;

namespace Game.Core.World.Generation;

public sealed class WorldGenerationContext
{
    public WorldGenerationContext(World world, int seed, Random random, INoiseService noise)
    {
        World = world;
        Seed = seed;
        Random = random;
        Noise = noise;
        SurfaceHeights = new int[world.WidthTiles];
        Biomes = new BiomeMap("forest");
    }

    public World World { get; }

    public int Seed { get; }

    public Random Random { get; }

    public INoiseService Noise { get; }

    public int[] SurfaceHeights { get; }

    public BiomeMap Biomes { get; }
}
