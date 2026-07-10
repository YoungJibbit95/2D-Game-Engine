using Game.Core.World;
using Game.Core.World.Generation;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class WorldGenerationMilestoneTests
{
    [Fact]
    public void CavernStep_CarvesConnectedOrganicRoomsDeterministically()
    {
        var profile = CreateProfile() with
        {
            CavernRoomCount = 4,
            CavernMinDepthOffset = 8,
            CavernMinRadiusX = 8,
            CavernMaxRadiusX = 10,
            CavernMinRadiusY = 5,
            CavernMaxRadiusY = 7,
            CavernIrregularity = 0.3f,
            CavernConnectorRadius = 2,
            CavernConnectorWander = 5
        };

        var first = GenerateWithSteps(profile, seed: 8128, new TerrainGenerationStep(), new CavernGenerationStep());
        var second = GenerateWithSteps(profile, seed: 8128, new TerrainGenerationStep(), new CavernGenerationStep());
        var analysis = new WorldAnalyzer().Analyze(first);

        AssertWorldEqual(first, second);
        Assert.Equal(1, analysis.CavernRegionCount);
        Assert.True(analysis.LargestCavernTileCount >= 250);
    }

    [Fact]
    public void SurfaceLakeStep_CreatesShapedSurfaceBasin()
    {
        var profile = CreateProfile() with
        {
            WaterPocketAttempts = 1,
            SurfaceLakeAttempts = 1,
            SurfaceLakeMinWidth = 24,
            SurfaceLakeMaxWidth = 24,
            SurfaceLakeMinDepth = 7,
            SurfaceLakeMaxDepth = 7,
            SurfaceLakeBottomIrregularity = 0.35f
        };
        var world = GenerateWithSteps(profile, seed: 441, new TerrainGenerationStep(), new SurfaceLakeGenerationStep());
        var analysis = new WorldAnalyzer().Analyze(world);
        var liquidColumns = GetLiquidColumnDepths(world);

        Assert.True(analysis.SurfaceLiquidTileCount > 20);
        Assert.Equal(0, analysis.CaveLiquidTileCount);
        Assert.True(liquidColumns.Values.Distinct().Count() >= 3);
        Assert.Equal(1, analysis.LiquidBodyCount);
    }

    [Fact]
    public void CavePoolStep_ShapesContainedPoolBelowSurface()
    {
        var profile = CreateProfile() with
        {
            WidthTiles = 64,
            HeightTiles = 48,
            SurfaceBaseY = 10,
            WaterPocketAttempts = 1,
            CavePoolAttempts = 1,
            CavePoolMinDepthOffset = 5,
            CavePoolMinWidth = 16,
            CavePoolMaxWidth = 16,
            CavePoolMinDepth = 5,
            CavePoolMaxDepth = 5,
            CavePoolBottomIrregularity = 0.3f
        };
        var context = CreateTerrainContext(profile, seed: 917);
        for (var x = 8; x <= 55; x++)
        {
            for (var y = 18; y <= 28; y++)
            {
                context.World.RemoveTile(x, y);
            }
        }

        new CavePoolGenerationStep().Apply(context);
        var analysis = new WorldAnalyzer().Analyze(context.World);
        var liquidColumns = GetLiquidColumnDepths(context.World);

        Assert.True(analysis.CaveLiquidTileCount > 0);
        Assert.Equal(0, analysis.SurfaceLiquidTileCount);
        Assert.True(liquidColumns.Values.Distinct().Count() >= 2);
        Assert.All(liquidColumns.Keys, x => Assert.True(liquidColumns[x] < profile.CavePoolMaxDepth + 2));
    }

    [Fact]
    public void WallSteps_PreserveForegroundAndCleanOpenCaveCores()
    {
        var profile = CreateProfile() with
        {
            WidthTiles = 40,
            HeightTiles = 40,
            SurfaceBaseY = 8,
            UndergroundWallStartDepthOffset = 2,
            UndergroundWallCoverageChance = 1f,
            CaveWallCoverageChance = 1f,
            CaveWallCleanupPasses = 2,
            CaveWallCleanupMinNeighbors = 2,
            CaveWallCoreOpenNeighborThreshold = 7
        };
        var context = CreateTerrainContext(profile, seed: 73);
        for (var x = 10; x <= 29; x++)
        {
            for (var y = 15; y <= 27; y++)
            {
                context.World.RemoveTile(x, y);
            }
        }

        var foregroundBefore = SnapshotForeground(context.World);
        new UndergroundWallGenerationStep().Apply(context);
        new CaveWallCleanupStep().Apply(context);
        var analysis = new WorldAnalyzer().Analyze(context.World);

        Assert.Equal(foregroundBefore, SnapshotForeground(context.World));
        Assert.NotEqual((ushort)0, context.World.GetTile(5, 20).WallId);
        Assert.Equal((ushort)0, context.World.GetTile(19, 20).WallId);
        Assert.NotEqual((ushort)0, context.World.GetTile(10, 20).WallId);
        Assert.True(analysis.WallTileCount > 0);
        Assert.True(analysis.ExposedWallTileCount > 0);
        Assert.All(
            EnumerateTiles(context.World).Where(tile => tile.WallId != 0),
            tile => Assert.True(tile.Flags.HasFlag(TileFlags.HasWall)));
    }

    [Fact]
    public void MilestoneSteps_ClampOversizedShapesAtWorldEdges()
    {
        var profile = CreateProfile() with
        {
            WidthTiles = 24,
            HeightTiles = 24,
            SurfaceBaseY = 8,
            SurfaceAmplitude = 1,
            CavernRoomCount = 3,
            CavernMinDepthOffset = 0,
            CavernMinRadiusX = 20,
            CavernMaxRadiusX = 40,
            CavernMinRadiusY = 20,
            CavernMaxRadiusY = 40,
            WaterPocketAttempts = 4,
            SurfaceLakeAttempts = 2,
            SurfaceLakeMinWidth = 20,
            SurfaceLakeMaxWidth = 100,
            SurfaceLakeMinDepth = 10,
            SurfaceLakeMaxDepth = 100,
            CavePoolAttempts = 2,
            CavePoolMinWidth = 20,
            CavePoolMaxWidth = 100,
            CavePoolMinDepth = 10,
            CavePoolMaxDepth = 100
        };

        var exception = Record.Exception(() => new AdvancedWorldGenerator().Generate(profile, seed: -991));

        Assert.Null(exception);
    }

    private static WorldGenerationProfile CreateProfile()
    {
        return WorldGenerationProfile.ForDimensions(160, 96) with
        {
            Id = "milestone_test",
            SurfaceBaseY = 24,
            SurfaceAmplitude = 2,
            DirtDepthMin = 5,
            DirtDepthMax = 8,
            CaveWalkerCount = 0,
            CavernRoomCount = 0,
            Ores = Array.Empty<OreGenerationDefinition>(),
            TreeAttempts = 0,
            WaterPocketAttempts = 0,
            SurfaceLakeAttempts = 0,
            CavePoolAttempts = 0,
            UndergroundWallCoverageChance = 0f,
            CaveWallCoverageChance = 0f
        };
    }

    private static World GenerateWithSteps(
        WorldGenerationProfile profile,
        int seed,
        params IWorldGenerationStep[] steps)
    {
        return new AdvancedWorldGenerator(steps, value => new FastNoiseLiteNoiseService(value)).Generate(profile, seed);
    }

    private static WorldGenerationContext CreateTerrainContext(WorldGenerationProfile profile, int seed)
    {
        var world = new World(profile.WidthTiles, profile.HeightTiles, WorldMetadata.CreateDefault(seed));
        var context = new WorldGenerationContext(
            world,
            seed,
            new Random(seed),
            new FastNoiseLiteNoiseService(seed),
            profile);
        new TerrainGenerationStep().Apply(context);
        return context;
    }

    private static Dictionary<int, int> GetLiquidColumnDepths(World world)
    {
        var result = new Dictionary<int, int>();
        for (var x = 0; x < world.WidthTiles; x++)
        {
            var count = 0;
            for (var y = 0; y < world.HeightTiles; y++)
            {
                if (world.GetTile(x, y).HasLiquid)
                {
                    count++;
                }
            }

            if (count > 0)
            {
                result[x] = count;
            }
        }

        return result;
    }

    private static ushort[] SnapshotForeground(World world)
    {
        return EnumerateTiles(world)
            .Select(tile => (ushort)((tile.TileId << 1) | (tile.IsSolid ? 1 : 0)))
            .ToArray();
    }

    private static IEnumerable<TileInstance> EnumerateTiles(World world)
    {
        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                yield return world.GetTile(x, y);
            }
        }
    }

    private static void AssertWorldEqual(World expected, World actual)
    {
        Assert.Equal(expected.WidthTiles, actual.WidthTiles);
        Assert.Equal(expected.HeightTiles, actual.HeightTiles);
        for (var y = 0; y < expected.HeightTiles; y++)
        {
            for (var x = 0; x < expected.WidthTiles; x++)
            {
                var expectedTile = expected.GetTile(x, y);
                var actualTile = actual.GetTile(x, y);
                Assert.Equal(expectedTile.TileId, actualTile.TileId);
                Assert.Equal(expectedTile.WallId, actualTile.WallId);
                Assert.Equal(expectedTile.LiquidAmount, actualTile.LiquidAmount);
                Assert.Equal(expectedTile.Flags, actualTile.Flags);
            }
        }
    }
}
