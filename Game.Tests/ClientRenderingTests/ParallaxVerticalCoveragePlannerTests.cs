using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParallaxVerticalCoveragePlannerTests
{
    [Theory]
    [InlineData(1280, 720, 54)]
    [InlineData(1920, 1080, 286)]
    [InlineData(3440, 1440, 501)]
    public void Build_ExtendsLegacyTopColorWithoutSamplingPanoramaEdges(
        int width,
        int height,
        int layerTop)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var layer = new Rectangle(-512, layerTop, 1536, 384);

        var built = ParallaxVerticalCoveragePlanner.TryBuildTopFill(
            layer,
            viewport,
            out var bounds);

        Assert.True(built);
        Assert.Equal(viewport.Top, bounds.Top);
        Assert.Equal(layerTop + 1, bounds.Bottom);
        Assert.Equal(viewport.X, bounds.X);
        Assert.Equal(viewport.Width, bounds.Width);
    }

    [Theory]
    [InlineData(-256, -64, 2560, 1440)]
    [InlineData(0, 0, 1920, 1080)]
    [InlineData(320, 180, 3440, 1440)]
    public void CoversViewportVertically_RequiresBothEdges(
        int viewportX,
        int viewportY,
        int width,
        int height)
    {
        var viewport = new Rectangle(viewportX, viewportY, width, height);

        Assert.True(ParallaxVerticalCoveragePlanner.CoversViewportVertically(
            new Rectangle(viewportX - 512, viewportY - 32, width + 1024, height + 64),
            viewport));
        Assert.False(ParallaxVerticalCoveragePlanner.CoversViewportVertically(
            new Rectangle(viewportX - 512, viewportY + 1, width + 1024, height),
            viewport));
        Assert.False(ParallaxVerticalCoveragePlanner.CoversViewportVertically(
            new Rectangle(viewportX - 512, viewportY - 1, width + 1024, height),
            viewport));
    }

    [Theory]
    [InlineData(1280, 720, 1536, 384)]
    [InlineData(1920, 1080, 1024, 256)]
    [InlineData(3440, 1440, 512, 128)]
    [InlineData(7680, 2160, 1024, 256)]
    public void FullscreenLayout_CoversViewportWithoutTopOrBottomExtension(
        int viewportWidth,
        int viewportHeight,
        int sourceWidth,
        int sourceHeight)
    {
        var viewport = new Rectangle(-73, 41, viewportWidth, viewportHeight);
        var layout = ParallaxViewportLayoutPlanner.Build(
            sourceWidth,
            sourceHeight,
            viewport,
            surfaceHorizon: -40_000f,
            undergroundBlend: 1f,
            verticalOffset: 24,
            verticalScroll: -100_000f,
            scaleMultiplier: 0.5f,
            coverViewport: false,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Far);
        var layer = new Rectangle(viewport.X, layout.Y, layout.Width, layout.Height);

        Assert.True(ParallaxVerticalCoveragePlanner.CoversViewportVertically(layer, viewport));
        Assert.False(ParallaxVerticalCoveragePlanner.TryBuildTopFill(layer, viewport, out _));
        Assert.True(layer.Top <= viewport.Top);
        Assert.True(layer.Bottom >= viewport.Bottom);
    }

    [Fact]
    public void CoverageChecks_AreAllocationFreeInSteadyState()
    {
        var viewport = new Rectangle(-320, 40, 3440, 1440);
        var layer = new Rectangle(-2048, -80, 8192, 1728);
        _ = ParallaxVerticalCoveragePlanner.CoversViewportVertically(layer, viewport);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var covered = true;

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            covered &= ParallaxVerticalCoveragePlanner.CoversViewportVertically(layer, viewport);
        }

        Assert.True(covered);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void InvalidBoundsNeverClaimCoverage()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);

        Assert.False(ParallaxVerticalCoveragePlanner.CoversViewportVertically(
            Rectangle.Empty,
            viewport));
        Assert.False(ParallaxVerticalCoveragePlanner.CoversViewportVertically(
            new Rectangle(0, 0, 1920, 1080),
            Rectangle.Empty));
    }
}
