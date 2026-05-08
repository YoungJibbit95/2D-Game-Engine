using Game.Core.World.Generation;

namespace Game.Core.World;

public sealed class SimpleWorldGenerator
{
    private readonly Func<int, INoiseService> _noiseFactory;

    public SimpleWorldGenerator()
        : this(seed => new FastNoiseLiteNoiseService(seed))
    {
    }

    public SimpleWorldGenerator(Func<int, INoiseService> noiseFactory)
    {
        _noiseFactory = noiseFactory;
    }

    public World Generate(int widthTiles, int heightTiles, int seed)
    {
        var world = new World(widthTiles, heightTiles, WorldMetadata.CreateDefault(seed));
        var noise = _noiseFactory(seed);

        var baseSurfaceY = Math.Clamp(heightTiles / 3, 4, heightTiles - 8);
        var amplitude = Math.Max(2, heightTiles / 12);

        for (var x = 0; x < widthTiles; x++)
        {
            var surfaceNoise = noise.GetNoise(x, 0);
            var surfaceY = baseSurfaceY + (int)MathF.Round(surfaceNoise * amplitude);
            surfaceY = Math.Clamp(surfaceY, Math.Max(2, heightTiles / 5), Math.Max(3, heightTiles / 2));
            var dirtDepth = 4 + Math.Abs(StableHash(seed, x) % 4);

            for (var y = surfaceY; y < heightTiles; y++)
            {
                var tileId = y == surfaceY
                    ? KnownTileIds.Grass
                    : y < surfaceY + dirtDepth
                        ? KnownTileIds.Dirt
                        : KnownTileIds.Stone;

                world.SetTile(x, y, TileInstance.FromTileId(tileId, TileFlags.IsNatural));
            }
        }

        var spawnTile = new Generation.SpawnPointFinder().FindSurfaceSpawn(world);
        world.SetMetadata(world.Metadata with { SpawnTile = spawnTile });
        ClearGenerationDirtyFlags(world);
        return world;
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

    private static void ClearGenerationDirtyFlags(World world)
    {
        foreach (var chunk in world.Chunks.Values)
        {
            chunk.ClearDirtyFlags();
        }
    }
}
