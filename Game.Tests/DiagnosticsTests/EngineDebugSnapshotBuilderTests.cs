using Game.Core;
using Game.Core.Diagnostics;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.Time;
using Game.Core.World;
using System.Numerics;
using Xunit;

namespace Game.Tests.DiagnosticsTests;

public sealed class EngineDebugSnapshotBuilderTests
{
    [Fact]
    public void Build_ReturnsWorldChunkEntityAndTimeMetrics()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 99) with { Name = "Debug" });
        world.SetTile(1, 1, KnownTileIds.Dirt);
        world.SetTile(2, 1, TileInstance.Liquid(255));
        var entities = new EntityManager();
        entities.Add(new PlayerEntity(Vector2.Zero, new TileCollisionResolver()));
        var time = new WorldTime();
        time.SetNight();

        var snapshot = new EngineDebugSnapshotBuilder().Build(world, entities, time);

        Assert.Equal("Debug", snapshot.WorldName);
        Assert.Equal(99, snapshot.Seed);
        Assert.Equal(1, snapshot.LoadedChunkCount);
        Assert.Equal(1, snapshot.EntityCount);
        Assert.Equal(1, snapshot.LiquidTileCount);
        Assert.Equal(1, snapshot.SolidTileCount);
        Assert.True(snapshot.IsNight);
        Assert.Equal(1, snapshot.EntityCountsByType["PlayerEntity"]);
    }

    [Fact]
    public void Build_AnalyzesLoadedNegativeChunksWithoutFiniteWidthIndexing()
    {
        var world = new World(
            GameConstants.ChunkSize,
            64,
            WorldMetadata.CreateDefault(seed: 42),
            isHorizontallyInfinite: true);
        world.SetTile(-40, 7, KnownTileIds.Stone);

        var snapshot = new EngineDebugSnapshotBuilder().Build(world, new EntityManager());

        Assert.Equal(1, snapshot.LoadedChunkCount);
        Assert.Equal(1, snapshot.SolidTileCount);
        Assert.Equal(7, snapshot.MinSurfaceY);
        Assert.Equal(7, snapshot.MaxSurfaceY);
    }}
