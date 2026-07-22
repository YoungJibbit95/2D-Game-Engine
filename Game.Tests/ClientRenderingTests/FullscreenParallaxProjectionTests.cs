using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class FullscreenParallaxProjectionTests
{
    [Theory]
    [InlineData(320, 240, 1024, 256)]
    [InlineData(1280, 720, 1536, 384)]
    [InlineData(1920, 1080, 1024, 256)]
    [InlineData(2560, 1440, 512, 128)]
    [InlineData(3440, 1440, 1024, 256)]
    [InlineData(5120, 1440, 1536, 384)]
    [InlineData(7680, 2160, 1024, 256)]
    public void Build_FullscreenDepthPlaneCoversViewportWithUniformIntegerScale(
        int viewportWidth,
        int viewportHeight,
        int sourceWidth,
        int sourceHeight)
    {
        var viewport = new Rectangle(-91, 37, viewportWidth, viewportHeight);

        var layout = ParallaxViewportLayoutPlanner.Build(
            sourceWidth,
            sourceHeight,
            viewport,
            surfaceHorizon: viewportHeight * 0.45f,
            undergroundBlend: 0f,
            verticalOffset: -8,
            verticalScroll: 10_000f,
            scaleMultiplier: 1f,
            coverViewport: false,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Far);
        var bounds = new Rectangle(viewport.X, layout.Y, layout.Width, layout.Height);

        Assert.True(ParallaxVerticalCoveragePlanner.CoversViewportVertically(bounds, viewport));
        Assert.Equal(0f, layout.Scale % 1f);
        Assert.Equal(0, layout.Width % sourceWidth);
        Assert.Equal(0, layout.Height % sourceHeight);
        Assert.Equal((long)layout.Width * sourceHeight, (long)layout.Height * sourceWidth);
        Assert.True(layout.Y <= viewport.Top);
        Assert.True(layout.Y + layout.Height >= viewport.Bottom);
    }

    [Theory]
    [InlineData(-1_000_000f)]
    [InlineData(-4096f)]
    [InlineData(0f)]
    [InlineData(4096f)]
    [InlineData(1_000_000f)]
    public void Build_FullscreenDepthPlaneBoundsExtremeCameraYWithoutChangingScale(float cameraY)
    {
        var viewport = new Rectangle(-320, 180, 3440, 1440);
        var baseline = Build(viewport, surfaceHorizon: 0f, verticalScroll: 0f);
        var actual = Build(
            viewport,
            surfaceHorizon: cameraY * 4f,
            verticalScroll: cameraY * 0.012f);
        var bounds = new Rectangle(viewport.X, actual.Y, actual.Width, actual.Height);

        Assert.Equal(baseline.Scale, actual.Scale);
        Assert.Equal(baseline.Width, actual.Width);
        Assert.Equal(baseline.Height, actual.Height);
        Assert.True(ParallaxVerticalCoveragePlanner.CoversViewportVertically(bounds, viewport));
        Assert.InRange(actual.Y, viewport.Bottom - actual.Height, viewport.Top);
    }

    [Fact]
    public void Build_FullscreenDepthPlaneIgnoresZoomDerivedHorizon()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        var baseline = Build(viewport, surfaceHorizon: -100_000f, verticalScroll: 12f);

        foreach (var zoom in new[] { 0.25f, 0.5f, 1f, 2f, 4f, 8f })
        {
            var actual = Build(
                viewport,
                surfaceHorizon: viewport.Height * zoom * 10_000f,
                verticalScroll: 12f);

            Assert.Equal(baseline, actual);
        }
    }

    [Fact]
    public void Build_FullscreenDepthPlaneSanitizesNonFinitePresentationInputs()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        var actual = ParallaxViewportLayoutPlanner.Build(
            1024,
            256,
            viewport,
            surfaceHorizon: float.NaN,
            undergroundBlend: float.NaN,
            verticalOffset: 0,
            verticalScroll: float.PositiveInfinity,
            scaleMultiplier: float.NaN,
            coverViewport: false,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Mid);
        var bounds = new Rectangle(viewport.X, actual.Y, actual.Width, actual.Height);

        Assert.True(float.IsFinite(actual.Scale));
        Assert.True(ParallaxVerticalCoveragePlanner.CoversViewportVertically(bounds, viewport));
    }

    [Fact]
    public void Build_FullscreenDepthPlaneIsAllocationFree()
    {
        var viewport = new Rectangle(0, 0, 2560, 1440);
        for (var warmup = 0; warmup < 512; warmup++)
        {
            _ = Build(viewport, 640f, -24f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            checksum += Build(viewport, 640f, -24f).Width;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotEqual(0, checksum);
        Assert.Equal(0, allocated);
    }

    private static ParallaxViewportLayout Build(
        Rectangle viewport,
        float surfaceHorizon,
        float verticalScroll)
    {
        return ParallaxViewportLayoutPlanner.Build(
            1024,
            256,
            viewport,
            surfaceHorizon,
            undergroundBlend: 0f,
            verticalOffset: 0,
            verticalScroll,
            scaleMultiplier: 1f,
            coverViewport: false,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Mid);
    }
}
