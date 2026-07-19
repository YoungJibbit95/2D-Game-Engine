using Game.Core.Biomes;

namespace Game.Core.World.Generation;

public sealed class WorldGenerationContext
{
    public WorldGenerationContext(World world, int seed, Random random, INoiseService noise, WorldGenerationProfile? profile = null)
    {
        World = world;
        Seed = seed;
        Random = random;
        Noise = noise;
        Profile = profile ?? WorldGenerationProfile.ForDimensions(world.WidthTiles, world.HeightTiles);
        Tiles = new WorldGenerationWorkspace(world);
        SurfaceHeights = new int[world.WidthTiles];
        Biomes = new BiomeMap("forest");
    }

    public World World { get; }

    public int Seed { get; }

    public Random Random { get; }

    public INoiseService Noise { get; }

    public WorldGenerationProfile Profile { get; }

    public WorldGenerationWorkspace Tiles { get; }

    public int[] SurfaceHeights { get; }

    public BiomeMap Biomes { get; }
}
