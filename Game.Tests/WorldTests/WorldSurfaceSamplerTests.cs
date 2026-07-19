using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldSurfaceSamplerTests
{
    [Theory]
    [InlineData(17)]
    [InlineData(1337)]
    [InlineData(7719)]
    [InlineData(91_244)]
    public void GetSurfaceHeight_ProducesBroadTerrainWithStableSpawnPlateau(int seed)
    {
        var profile = WorldGenerationProfile.Large with
        {
            HeightTiles = 320,
            SurfaceBaseY = 82,
            SurfaceAmplitude = 18
        };
        var heights = Enumerable.Range(-512, 1025)
            .Select(x => WorldSurfaceSampler.GetSurfaceHeight(profile, seed, x))
            .ToArray();
        var largestStep = heights
            .Zip(heights.Skip(1), (left, right) => Math.Abs(right - left))
            .Max();
        var spawnHeights = Enumerable.Range(-24, 49)
            .Select(x => WorldSurfaceSampler.GetSurfaceHeight(profile, seed, x))
            .ToArray();

        Assert.InRange(largestStep, 0, 1);
        Assert.True(heights.Max() - heights.Min() >= 8);
        Assert.True(LongestPlateau(heights) >= 5);
        Assert.InRange(spawnHeights.Max() - spawnHeights.Min(), 0, 1);
    }

    [Fact]
    public void TerrainGenerationStep_UsesSameTopologyCoordinatesAsInfiniteGenerator()
    {
        var profile = WorldGenerationProfile.Small with
        {
            WidthTiles = 257,
            HeightTiles = 160,
            SurfaceBaseY = 58,
            SurfaceAmplitude = 12,
            DirtDepthMin = 7,
            DirtDepthMax = 17
        };
        const int seed = 81_117;
        var finiteWorld = new World(profile.WidthTiles, profile.HeightTiles, WorldMetadata.CreateDefault(seed));
        var context = new WorldGenerationContext(
            finiteWorld,
            seed,
            new Random(seed),
            new FlatNoiseService(),
            profile);
        new TerrainGenerationStep().Apply(context);
        var infinite = new InfiniteWorldChunkGenerator(_ => new FlatNoiseService());
        var origin = profile.WidthTiles / 2;

        for (var finiteX = 0; finiteX < profile.WidthTiles; finiteX++)
        {
            var topologyX = finiteX - origin;
            Assert.Equal(
                context.SurfaceHeights[finiteX],
                infinite.GetSurfaceHeightAt(profile, seed, topologyX));
        }
    }

    [Fact]
    public void LegacyInfiniteWorldKeepsLegacySamplerWhileNewWorldUsesTopologyV2()
    {
        var profile = WorldGenerationProfile.Small with
        {
            HeightTiles = 128,
            SurfaceBaseY = 44,
            SurfaceAmplitude = 12,
            TreeAttempts = 0,
            WaterPocketAttempts = 0,
            Ores = Array.Empty<OreGenerationDefinition>()
        };
        var legacyGenerator = new InfiniteWorldChunkGenerator(
            _ => new FlatNoiseService(),
            generationVersion: WorldGenerationVersions.Legacy);
        var currentGenerator = new InfiniteWorldChunkGenerator(
            _ => new FlatNoiseService(),
            generationVersion: WorldGenerationVersions.Current);
        var legacyWorld = legacyGenerator.CreateWorld(profile, seed: 4711);
        var currentWorld = currentGenerator.CreateWorld(profile, seed: 4711);

        legacyGenerator.EnsureChunk(legacyWorld, profile, new ChunkPos(3, 1));
        currentGenerator.EnsureChunk(currentWorld, profile, new ChunkPos(3, 1));

        Assert.Equal(WorldGenerationVersions.Legacy, legacyWorld.Metadata.GenerationVersion);
        Assert.Equal(WorldGenerationVersions.Current, currentWorld.Metadata.GenerationVersion);
        Assert.False(
            legacyWorld.Chunks[new ChunkPos(3, 1)].Tiles.SequenceEqual(
                currentWorld.Chunks[new ChunkPos(3, 1)].Tiles));
    }

    [Fact]
    public void GetDirtDepth_FormsCoherentGeologicalBandInsteadOfColumnNoise()
    {
        var profile = WorldGenerationProfile.Medium with
        {
            DirtDepthMin = 8,
            DirtDepthMax = 22
        };
        var depths = Enumerable.Range(-512, 1025)
            .Select(x => WorldSurfaceSampler.GetDirtDepth(profile, 29_117, x))
            .ToArray();
        var largestStep = depths
            .Zip(depths.Skip(1), (left, right) => Math.Abs(right - left))
            .Max();

        Assert.InRange(largestStep, 0, 1);
        Assert.True(LongestPlateau(depths) >= 4);
        Assert.True(depths.Max() - depths.Min() >= 4);
    }

    private static int LongestPlateau(IReadOnlyList<int> values)
    {
        var longest = values.Count == 0 ? 0 : 1;
        var current = longest;
        for (var index = 1; index < values.Count; index++)
        {
            if (values[index] == values[index - 1])
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 1;
            }
        }

        return longest;
    }

    private sealed class FlatNoiseService : INoiseService
    {
        public float GetNoise(float x, float y)
        {
            return 0f;
        }
    }
}
