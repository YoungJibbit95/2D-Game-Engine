using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParallaxTerrainEnvelopePlannerTests
{
    [Fact]
    public void Build_AnchorsBelowDeepestVisibleTerrainWithSafetyOverlap()
    {
        static int Surface(int tileX) => tileX == 18 ? 74 : 52 + Math.Abs(tileX % 7);

        var result = ParallaxTerrainEnvelopePlanner.Build(
            fallbackSurfaceTileY: 50,
            new Rectangle(-320, 0, 1280, 720),
            tileSize: 16,
            Surface,
            horizontalMarginTiles: 0);

        Assert.Equal(-20, result.MinimumTileX);
        Assert.Equal(59, result.MaximumTileX);
        Assert.Equal(76, result.DeepestSurfaceTileY);
        Assert.Equal(80, result.SampleCount);
    }

    [Fact]
    public void Build_HandlesNegativeWorldCoordinatesAndNullResolver()
    {
        var range = ParallaxTerrainEnvelopePlanner.GetVisibleTileRange(
            new Rectangle(-33, 0, 32, 720),
            tileSize: 16);
        var result = ParallaxTerrainEnvelopePlanner.Build(
            fallbackSurfaceTileY: 44,
            new Rectangle(-33, 0, 32, 720),
            tileSize: 16,
            surfaceHeightResolver: null,
            horizontalMarginTiles: 2);

        Assert.Equal(-3, range.MinimumTileX);
        Assert.Equal(-1, range.MaximumTileX);
        Assert.Equal(-5, result.MinimumTileX);
        Assert.Equal(1, result.MaximumTileX);
        Assert.Equal(44, result.DeepestSurfaceTileY);
        Assert.Equal(0, result.SampleCount);
    }

    [Fact]
    public void Build_BoundsSamplingForExtremeZoomedOutViews()
    {
        static int Surface(int tileX) => 40 + Math.Abs(tileX % 11);

        var result = ParallaxTerrainEnvelopePlanner.Build(
            fallbackSurfaceTileY: 40,
            new Rectangle(-100_000, 0, 200_000, 720),
            tileSize: 16,
            Surface);

        Assert.InRange(result.SampleCount, 2, ParallaxTerrainEnvelopePlanner.MaximumSamples + 1);
        Assert.True(result.DeepestSurfaceTileY >= 50);
    }

    [Fact]
    public void Build_IsAllocationFreeInSteadyState()
    {
        static int Surface(int tileX) => 48 + Math.Abs(tileX % 13);
        var viewport = new Rectangle(-640, 0, 2560, 1440);
        _ = ParallaxTerrainEnvelopePlanner.Build(48, viewport, 16, Surface);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var deepest = 0;

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            deepest = ParallaxTerrainEnvelopePlanner.Build(48, viewport, 16, Surface).DeepestSurfaceTileY;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(deepest > 48);
        Assert.Equal(0, allocated);
    }
}
