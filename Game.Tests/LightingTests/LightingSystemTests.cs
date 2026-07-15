using Game.Core;
using Game.Core.Lighting;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Core.World.Streaming;
using Xunit;

namespace Game.Tests.LightingTests;

public sealed class LightingSystemTests
{
    [Fact]
    public void Recalculate_AppliesSunlightFromTop()
    {
        var world = new SimpleWorldGenerator().Generate(32, 32, seed: 22);
        new LightingSystem().Recalculate(world, Array.Empty<LightSource>());
        Assert.Equal(255, world.GetTile(0, 0).Light);
        Assert.True(world.GetTile(0, 31).Light < 255);
    }

    [Fact]
    public void Recalculate_UsesConfiguredSunlightIntensity()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
        new LightingSystem().Recalculate(world, Array.Empty<LightSource>(), sunlight: 64);
        Assert.Equal(64, world.GetTile(0, 0).Light);
    }

    [Fact]
    public void Recalculate_PropagatesPointLight()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 1));
        AddStoneRoof(world);
        var source = new LightSource(new TilePos(16, 16), 220, Radius: 5);
        new LightingSystem().Recalculate(world, new[] { source });
        Assert.True(world.GetTile(16, 16).Light >= 220);
        Assert.True(world.GetTile(18, 16).Light > world.GetTile(22, 16).Light);
    }

    [Fact]
    public void Recalculate_SolidTilesAttenuatePointLight()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 1));
        AddStoneRoof(world);
        world.SetTile(17, 16, KnownTileIds.Stone);
        var source = new LightSource(new TilePos(16, 16), 220, Radius: 5);
        new LightingSystem().Recalculate(world, new[] { source });
        Assert.True(world.GetTile(18, 16).Light < world.GetTile(16, 16).Light);
    }

    [Fact]
    public void Recalculate_LeavesUndergroundCavesDarkWithoutLightSources()
    {
        var world = new World(8, 10, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            for (var y = 0; y < 5; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }

        new LightingSystem().Recalculate(world, Array.Empty<LightSource>());
        Assert.InRange(world.GetTile(4, 7).Light, 0, 12);
    }

    [Fact]
    public void Recalculate_SkylightFallsOffSmoothlyFromOpenShaftIntoSideChamber()
    {
        var world = new World(12, 16, WorldMetadata.CreateDefault(seed: 17));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            if (x == 3)
            {
                continue;
            }

            for (var y = 0; y < 6; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }

        new LightingSystem().Recalculate(world, Array.Empty<LightSource>());

        var shaft = world.GetTile(3, 9).Light;
        var near = world.GetTile(4, 9).Light;
        var middle = world.GetTile(6, 9).Light;
        var far = world.GetTile(9, 9).Light;
        Assert.Equal(255, shaft);
        Assert.True(shaft > near);
        Assert.True(near > middle);
        Assert.True(middle > far);
        Assert.True(far > LightingOptions.Default.MinimumAmbientLight);
    }

    [Fact]
    public void Recalculate_FirstSolidSurfaceAttenuatesInsteadOfReceivingFullSunlight()
    {
        var world = new World(4, 8, WorldMetadata.CreateDefault(seed: 18));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 3, KnownTileIds.Stone);
        }

        new LightingSystem().Recalculate(world, Array.Empty<LightSource>());

        Assert.Equal(255, world.GetTile(2, 2).Light);
        Assert.True(world.GetTile(2, 3).Light < 255);
        Assert.True(world.GetTile(2, 4).Light < world.GetTile(2, 3).Light);
    }

    [Fact]
    public void Recalculate_UsesAmbientFloorUnderground()
    {
        var world = new World(4, 8, WorldMetadata.CreateDefault(seed: 1));
        for (var y = 0; y < 8; y++)
        {
            world.SetTile(0, y, KnownTileIds.Stone);
        }

        new LightingSystem().Recalculate(
            world,
            Array.Empty<LightSource>(),
            options: new LightingOptions(MinimumAmbientLight: 9));
        Assert.Equal(9, world.GetTile(0, 7).Light);
    }

    [Fact]
    public void RecalculateDirty_RemovesStaleMiningShadowAndClearsDirtyFlag()
    {
        var world = new World(8, 12, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            for (var y = 0; y < 5; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }

        var lighting = new LightingSystem();
        lighting.Recalculate(world, Array.Empty<LightSource>());
        var shadowed = world.GetTile(4, 8).Light;
        for (var y = 0; y < 5; y++)
        {
            world.RemoveTile(4, y);
        }

        var result = lighting.RecalculateDirty(
            world,
            TileRegistry.Create(Array.Empty<TileDefinition>()),
            sunlight: 255);
        Assert.True(result.UpdatedChunks > 0);
        Assert.True(world.GetTile(4, 8).Light > shadowed);
        Assert.All(world.Chunks.Values, chunk => Assert.False(chunk.NeedsLightUpdate));
    }

    [Fact]
    public void RecalculateDirty_UsesDataDrivenTileEmission()
    {
        var world = new World(12, 12, WorldMetadata.CreateDefault(seed: 2));
        AddStoneRoof(world);
        var torch = new TileDefinition
        {
            NumericId = 11,
            Id = "torch",
            DisplayName = "Torch",
            TexturePath = "world/objects/torch",
            Hardness = 0.25f,
            EmittedLight = 238,
            LightRadius = 10
        };
        var lighting = new LightingSystem();
        lighting.Recalculate(world, Array.Empty<LightSource>());
        world.SetTile(6, 8, TileInstance.FromTileId(torch.NumericId));
        lighting.RecalculateDirty(world, TileRegistry.Create(new[] { torch }), sunlight: 64);
        Assert.True(world.GetTile(6, 8).Light >= 238);
        Assert.True(world.GetTile(8, 8).Light > world.GetTile(11, 8).Light);
    }

    [Fact]
    public void EvaluateSkyExposure_DistinguishesUnknownOpenAndOccludedColumns()
    {
        var world = CreateStreamingWorld();
        MaterializeChunk(world, new ChunkPos(0, 1));

        Assert.Equal(SkyExposureState.Unknown, LightingSystem.EvaluateSkyExposure(world, 4, 40));

        MaterializeChunk(world, new ChunkPos(0, 0));
        Assert.Equal(SkyExposureState.Open, LightingSystem.EvaluateSkyExposure(world, 4, 40));

        world.SetTile(4, 12, KnownTileIds.Stone);
        Assert.Equal(SkyExposureState.Occluded, LightingSystem.EvaluateSkyExposure(world, 4, 40));
    }

    [Fact]
    public void RecalculateDirty_UnknownExposureTransitionsAfterAboveChunkMaterializes()
    {
        var world = CreateStreamingWorld();
        var below = MaterializeChunk(world, new ChunkPos(0, 1));
        world.ClearAllDirtyFlags();
        below.MarkLightDirty();
        var lighting = new LightingSystem();
        var visible = CoordinateUtils.ChunkTileBounds(below.Position);

        lighting.RecalculateDirty(
            world,
            EmptyTiles(),
            sunlight: 255,
            visible,
            maxChunks: 1);

        Assert.Equal(1, lighting.LastSchedulingTelemetry.UnknownSkyChunks);
        Assert.Equal(LightingOptions.Default.UnknownSkyLight, world.GetTile(4, 40).Light);
        Assert.False(below.NeedsLightUpdate);

        var above = MaterializeChunk(world, new ChunkPos(0, 0));
        lighting.RecalculateDirty(
            world,
            EmptyTiles(),
            sunlight: 255,
            visible,
            maxChunks: 1);

        Assert.Equal(new ChunkPos(0, 1), Assert.Single(lighting.LastProcessedChunkPositions));
        Assert.Equal(1, lighting.LastSchedulingTelemetry.VisibleChunksUpdated);
        Assert.Equal(255, world.GetTile(4, 40).Light);
        Assert.Equal(SkyExposureState.Open, LightingSystem.EvaluateSkyExposure(world, 4, 40));

        Assert.False(above.IsDirty);
        Assert.True(world.UnloadChunk(above.Position));
        lighting.RecalculateDirty(
            world,
            EmptyTiles(),
            sunlight: 255,
            visible,
            maxChunks: 1);

        Assert.Equal(1, lighting.LastSchedulingTelemetry.UnknownSkyChunks);
        Assert.Equal(LightingOptions.Default.UnknownSkyLight, world.GetTile(4, 40).Light);
        Assert.Equal(SkyExposureState.Unknown, LightingSystem.EvaluateSkyExposure(world, 4, 40));
    }

    [Fact]
    public void RecalculateDirty_MaterializedSnapshotRetainsPendingLightInvalidation()
    {
        var world = CreateStreamingWorld();
        var position = new ChunkPos(2, 1);
        var target = world.GetOrCreateChunk(position);
        target.MarkLightDirty();
        var tiles = Enumerable.Repeat(TileInstance.Air, GameConstants.ChunkSize * GameConstants.ChunkSize).ToArray();
        var snapshot = new ChunkStreamingChunkSnapshot(position, tiles, ChunkMetadata.Empty);

        snapshot.ApplyTo(target);

        Assert.True(target.NeedsLightUpdate);
        var lighting = new LightingSystem();
        var result = lighting.RecalculateDirty(
            world,
            EmptyTiles(),
            sunlight: 255,
            CoordinateUtils.ChunkTileBounds(position),
            maxChunks: 1);
        Assert.Equal(1, result.UpdatedChunks);
        Assert.False(target.NeedsLightUpdate);
    }

    [Fact]
    public void RecalculateDirty_VisibleFirstOrderIsDeterministicAndBounded()
    {
        var positions = new[]
        {
            new ChunkPos(10, 2),
            new ChunkPos(4, 3),
            new ChunkPos(5, 2),
            new ChunkPos(3, 2),
            new ChunkPos(4, 1),
            new ChunkPos(4, 2)
        };
        var visible = CoordinateUtils.ChunkTileBounds(new ChunkPos(4, 2));

        var first = RunPriorityUpdate(positions, visible, out var firstResult, out var firstTelemetry);
        var second = RunPriorityUpdate(positions.Reverse(), visible, out var secondResult, out var secondTelemetry);

        var expected = new[] { new ChunkPos(4, 2), new ChunkPos(4, 1), new ChunkPos(3, 2) };
        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
        Assert.Equal(3, firstResult.UpdatedChunks);
        Assert.Equal(3, firstTelemetry.DeferredChunks);
        Assert.Equal(1, firstTelemetry.VisibleChunksUpdated);
        Assert.Equal(firstResult, secondResult);
        Assert.Equal(firstTelemetry, secondTelemetry);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RecalculateDirty_AboveBelowAndLateralLoadOrdersConverge(int orderIndex)
    {
        var orders = new[]
        {
            new[] { new ChunkPos(0, 0), new ChunkPos(1, 0), new ChunkPos(0, 1), new ChunkPos(1, 1) },
            new[] { new ChunkPos(1, 1), new ChunkPos(0, 1), new ChunkPos(1, 0), new ChunkPos(0, 0) },
            new[] { new ChunkPos(0, 1), new ChunkPos(0, 0), new ChunkPos(1, 1), new ChunkPos(1, 0) }
        };

        var actual = RunLoadOrder(orders[orderIndex]);
        var expected = RunLoadOrder(orders[0]);

        Assert.Equal(expected, actual);
        Assert.Equal(255, actual[0]);
        Assert.True(actual[0] > actual[1]);
        Assert.True(actual[1] > actual[2]);
        Assert.True(actual[2] > LightingOptions.Default.MinimumAmbientLight);
    }

    [Fact]
    public void RecalculateDirty_SurfaceShaftTransitionsSmoothlyIntoLoadedCave()
    {
        var light = RunLoadOrder(new[]
        {
            new ChunkPos(0, 0),
            new ChunkPos(1, 0),
            new ChunkPos(0, 1),
            new ChunkPos(1, 1)
        });

        Assert.Equal(255, light[0]);
        Assert.InRange(light[1], (byte)1, (byte)254);
        Assert.InRange(light[2], (byte)(LightingOptions.Default.MinimumAmbientLight + 1), (byte)253);
    }

    [Fact]
    public void ResolveSunlight_IsSmoothBoundedAndBrightestAtMidday()
    {
        var midnight = LightingSystem.ResolveSunlight(0.0);
        var dawn = LightingSystem.ResolveSunlight(0.25);
        var midday = LightingSystem.ResolveSunlight(0.5);
        Assert.InRange(midnight, (byte)50, (byte)80);
        Assert.True(dawn > midnight);
        Assert.True(midday > dawn);
        Assert.Equal(midnight, LightingSystem.ResolveSunlight(1.0));
    }

    private static void AddStoneRoof(World world)
    {
        for (var x = 0; x < world.WidthTiles; x++)
        {
            for (var y = 0; y < 6; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }
    }

    private static TileRegistry EmptyTiles()
    {
        return TileRegistry.Create(Array.Empty<TileDefinition>());
    }

    private static World CreateStreamingWorld()
    {
        return new World(
            GameConstants.ChunkSize,
            GameConstants.ChunkSize * 4,
            WorldMetadata.CreateDefault(seed: 808),
            isHorizontallyInfinite: true);
    }

    private static Chunk MaterializeChunk(World world, ChunkPos position, bool addCaveRoof = false)
    {
        var tiles = Enumerable.Repeat(TileInstance.Air, GameConstants.ChunkSize * GameConstants.ChunkSize).ToArray();
        if (addCaveRoof && position.Y == 1)
        {
            for (var localX = 0; localX < GameConstants.ChunkSize; localX++)
            {
                var isShaft = position.X == 0
                    ? localX == GameConstants.ChunkSize - 1
                    : position.X == 1 && localX == 0;
                if (!isShaft)
                {
                    tiles[localX] = TileInstance.FromTileId(KnownTileIds.Stone);
                }
            }
        }

        var chunk = world.GetOrCreateChunk(position);
        chunk.LoadTiles(tiles);
        return chunk;
    }

    private static ChunkPos[] RunPriorityUpdate(
        IEnumerable<ChunkPos> positions,
        RectI visible,
        out LightingUpdateResult result,
        out LightingSchedulingTelemetry telemetry)
    {
        var world = CreateStreamingWorld();
        foreach (var position in positions)
        {
            MaterializeChunk(world, position);
        }

        var lighting = new LightingSystem();
        result = lighting.RecalculateDirty(
            world,
            EmptyTiles(),
            sunlight: 255,
            visible,
            maxChunks: 3);
        telemetry = lighting.LastSchedulingTelemetry;
        return lighting.LastProcessedChunkPositions.ToArray();
    }

    private static byte[] RunLoadOrder(IReadOnlyList<ChunkPos> order)
    {
        var world = CreateStreamingWorld();
        var lighting = new LightingSystem();
        var visible = new RectI(31, 32, 10, 16);
        foreach (var position in order)
        {
            MaterializeChunk(world, position, addCaveRoof: true);
            lighting.RecalculateDirty(
                world,
                EmptyTiles(),
                sunlight: 255,
                visible,
                maxChunks: 1);
        }

        for (var update = 0; update < 32 && world.Chunks.Values.Any(chunk => chunk.NeedsLightUpdate); update++)
        {
            lighting.RecalculateDirty(
                world,
                EmptyTiles(),
                sunlight: 255,
                visible,
                maxChunks: 4);
        }

        Assert.DoesNotContain(world.Chunks.Values, chunk => chunk.NeedsLightUpdate);
        return new[]
        {
            world.GetTile(32, 40).Light,
            world.GetTile(34, 40).Light,
            world.GetTile(38, 40).Light
        };
    }

}
