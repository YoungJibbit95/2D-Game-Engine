namespace Game.Core.World.Generation;

public sealed class AdvancedWorldGenerator
{
    private readonly IReadOnlyList<IWorldGenerationStep> _steps;
    private readonly Func<int, INoiseService> _noiseFactory;

    public AdvancedWorldGenerator()
        : this(
            new IWorldGenerationStep[]
            {
                new BiomeAssignmentStep(),
                new TerrainGenerationStep(),
                new CaveGenerationStep(),
                new OreGenerationStep(),
                new WaterPocketGenerationStep(),
                new StructureGenerationStep(),
                new TreeGenerationStep()
            },
            seed => new FastNoiseLiteNoiseService(seed))
    {
    }

    public AdvancedWorldGenerator(IEnumerable<IWorldGenerationStep> steps, Func<int, INoiseService> noiseFactory)
    {
        _steps = steps.OrderBy(step => step.Order).ToArray();
        _noiseFactory = noiseFactory;
    }

    public World Generate(int widthTiles, int heightTiles, int seed)
    {
        return GenerateDetailed(widthTiles, heightTiles, seed).World;
    }

    public WorldGenerationResult GenerateDetailed(int widthTiles, int heightTiles, int seed)
    {
        var world = new World(widthTiles, heightTiles, WorldMetadata.CreateDefault(seed));
        var context = new WorldGenerationContext(world, seed, new Random(seed), _noiseFactory(seed));

        foreach (var step in _steps)
        {
            step.Apply(context);
        }

        var spawnTile = new SpawnPointFinder().FindSurfaceSpawn(world);
        world.SetMetadata(world.Metadata with { SpawnTile = spawnTile });
        world.ClearAllDirtyFlags();
        return new WorldGenerationResult(world, context.Biomes, spawnTile);
    }
}
