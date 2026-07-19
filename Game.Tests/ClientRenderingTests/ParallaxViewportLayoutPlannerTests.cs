using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParallaxViewportLayoutPlannerTests
{
    [Theory]
    [InlineData(1920, 1080, -4_000f)]
    [InlineData(1920, 1080, 8_000f)]
    [InlineData(2560, 1440, -8_000f)]
    [InlineData(2560, 1440, 16_000f)]
    public void Build_ExtremeCameraHeightAlwaysCoversViewportTop(int width, int height, float horizon)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var layout = ParallaxViewportLayoutPlanner.Build(
            512,
            128,
            viewport,
            horizon,
            undergroundBlend: 0f,
            verticalOffset: -8,
            verticalScroll: 24f,
            scaleMultiplier: 1f,
            coverViewport: false,
            ParallaxProjectionMode.ViewportBackdrop);

        Assert.True(layout.Y < viewport.Top);
        Assert.True(layout.Y + layout.Height >= layout.Horizon - 33f);
        Assert.InRange(layout.Horizon, height * 0.38f, height * 0.96f);
        Assert.True(layout.Width >= 512);
    }

    [Fact]
    public void Build_IsAllocationFreeAndDeterministic()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        var expected = ParallaxViewportLayoutPlanner.Build(
            512,
            128,
            viewport,
            640f,
            0.2f,
            -8,
            12f,
            1f,
            false,
            ParallaxProjectionMode.ViewportBackdrop);
        var actual = expected;
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            actual = ParallaxViewportLayoutPlanner.Build(
                512,
                128,
                viewport,
                640f,
                0.2f,
                -8,
                12f,
                1f,
                false,
                ParallaxProjectionMode.ViewportBackdrop);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, actual);
        Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(1920, 1080, 640f, -8, 12f)]
    [InlineData(2560, 1440, -8_000f, 22, -96f)]
    [InlineData(1280, 720, 8_000f, -40, 160f)]
    public void Build_AuthoredPanoramaCoversEntireViewport(
        int width,
        int height,
        float horizon,
        int verticalOffset,
        float verticalScroll)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var layout = ParallaxViewportLayoutPlanner.Build(
            1536,
            384,
            viewport,
            horizon,
            undergroundBlend: 0.35f,
            verticalOffset,
            verticalScroll,
            scaleMultiplier: 1f,
            coverViewport: true,
            ParallaxProjectionMode.ViewportBackdrop);

        Assert.True(layout.Y < viewport.Top);
        Assert.True(layout.Y + layout.Height > viewport.Bottom);
        Assert.True(layout.Width > viewport.Width);
    }

    [Theory]
    [InlineData(1280, 720, 0.5f)]
    [InlineData(1920, 1080, 0.5f)]
    [InlineData(2560, 1440, 0.6666667f)]
    [InlineData(3440, 1440, 0.6666667f)]
    [InlineData(3840, 2160, 1f)]
    [InlineData(7680, 2160, 1f)]
    public void Build_DistantPanoramaJumpTraceKeepsDistanceScaleAndDimensionsInvariant(
        int width,
        int height,
        float expectedPixelScale)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var horizon = ParallaxViewportLayoutPlanner.ResolveDistantSurfaceHorizon(
            viewport,
            160f,
            ParallaxDepthPlane.Far);
        var baseline = ParallaxViewportLayoutPlanner.Build(
            1536,
            384,
            viewport,
            horizon,
            undergroundBlend: 0f,
            verticalOffset: -8,
            verticalScroll: 0f,
            scaleMultiplier: 0.5f,
            coverViewport: false,
            ParallaxProjectionMode.DistantHorizonBand,
            ParallaxDepthPlane.Far);

        for (var cameraDeltaY = -160; cameraDeltaY <= 160; cameraDeltaY += 8)
        {
            var layout = ParallaxViewportLayoutPlanner.Build(
                1536,
                384,
                viewport,
                horizon,
                undergroundBlend: 0f,
                verticalOffset: -8,
                verticalScroll: cameraDeltaY * 0.0035f,
                scaleMultiplier: 0.5f,
                coverViewport: false,
                ParallaxProjectionMode.DistantHorizonBand,
                ParallaxDepthPlane.Far);

            Assert.Equal(baseline.Scale, layout.Scale);
            Assert.Equal(baseline.Width, layout.Width);
            Assert.Equal(baseline.Height, layout.Height);
            Assert.InRange(Math.Abs(layout.Y - baseline.Y), 0, 1);
        }

        Assert.Equal(expectedPixelScale, baseline.Scale, precision: 5);
        Assert.Equal((int)MathF.Round(1536 * expectedPixelScale), baseline.Width);
        Assert.Equal((int)MathF.Round(384 * expectedPixelScale), baseline.Height);
        Assert.Equal(4f, baseline.Width / (float)baseline.Height);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3440, 1440)]
    [InlineData(5120, 1440)]
    public void Build_DistantPanoramaDoesNotCropOrZoomAcrossCameraZoomExtremes(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var expectedScale = ParallaxViewportLayoutPlanner.ResolveAuthoredPixelScale(height, 0.5f);

        foreach (var cameraZoom in new[] { 0.25f, 0.5f, 1f, 2f, 4f })
        {
            var terrainDepth = 192f * cameraZoom;
            var horizon = ParallaxViewportLayoutPlanner.ResolveDistantSurfaceHorizon(
                viewport,
                terrainDepth,
                ParallaxDepthPlane.Far);
            var layout = ParallaxViewportLayoutPlanner.Build(
                1536,
                384,
                viewport,
                horizon,
                undergroundBlend: 0f,
                verticalOffset: -8,
                verticalScroll: 80f * 0.0035f,
                scaleMultiplier: 0.5f,
                coverViewport: false,
                ParallaxProjectionMode.DistantHorizonBand,
                ParallaxDepthPlane.Far);

            Assert.Equal((float)expectedScale, layout.Scale);
            Assert.Equal(1536 * expectedScale, layout.Width);
            Assert.Equal(384 * expectedScale, layout.Height);
            Assert.Equal(4f, layout.Width / (float)layout.Height);
        }
    }

    [Theory]
    [InlineData(0, 0.5f)]
    [InlineData(719, 0.5f)]
    [InlineData(720, 0.5f)]
    [InlineData(1080, 0.5f)]
    [InlineData(1440, 0.6666667f)]
    [InlineData(1620, 0.75f)]
    [InlineData(2160, 1f)]
    [InlineData(4320, 2f)]
    [InlineData(8640, 4f)]
    public void ResolveAuthoredPixelScale_UsesBoundedStableDistanceScale(int viewportHeight, float expected)
    {
        Assert.Equal(
            expected,
            ParallaxViewportLayoutPlanner.ResolveAuthoredPixelScale(viewportHeight, 0.5f),
            precision: 5);
    }

    [Fact]
    public void ResolveDistantSurfaceHorizon_DependsOnTerrainEnvelopeButNotCameraHeight()
    {
        var viewport = new Rectangle(0, 0, 2560, 1440);

        var shallow = ParallaxViewportLayoutPlanner.ResolveDistantSurfaceHorizon(
            viewport,
            32f,
            ParallaxDepthPlane.Far);
        var deep = ParallaxViewportLayoutPlanner.ResolveDistantSurfaceHorizon(
            viewport,
            640f,
            ParallaxDepthPlane.Far);

        Assert.True(deep > shallow);
        Assert.InRange(deep, viewport.Height * 0.38f, viewport.Height * 0.395f);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    public void Build_DepthStackOrdersSkyToTerrainAndOuterFillsCoverViewport(int width, int height)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var planes = new[]
        {
            (ParallaxDepthPlane.Far, 1536, 384, 0.5f, -8),
            (ParallaxDepthPlane.Mid, 1024, 256, 0.68f, 0),
            (ParallaxDepthPlane.Near, 512, 128, 0.92f, 8)
        };
        var previousHorizon = float.MinValue;

        foreach (var plane in planes)
        {
            var horizon = ParallaxViewportLayoutPlanner.ResolveDistantSurfaceHorizon(
                viewport,
                terrainEnvelopeDepthPixels: 192f,
                plane.Item1);
            var layout = ParallaxViewportLayoutPlanner.Build(
                plane.Item2,
                plane.Item3,
                viewport,
                horizon,
                undergroundBlend: 0f,
                plane.Item5,
                verticalScroll: 0f,
                plane.Item4,
                coverViewport: false,
                ParallaxProjectionMode.DistantHorizonBand,
                plane.Item1);
            var bounds = new Rectangle(viewport.X, layout.Y, layout.Width, layout.Height);

            Assert.True(layout.Horizon > previousHorizon);
            Assert.True(layout.Y + layout.Height < viewport.Bottom);
            if (layout.Y > viewport.Top)
            {
                Assert.True(ParallaxVerticalCoveragePlanner.TryBuildTopFill(bounds, viewport, out var topFill));
                Assert.Equal(viewport.Top, topFill.Top);
            }
            else
            {
                Assert.True(bounds.Top <= viewport.Top);
                Assert.False(ParallaxVerticalCoveragePlanner.TryBuildTopFill(bounds, viewport, out _));
            }
            if (bounds.Bottom < viewport.Bottom)
            {
                Assert.True(ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
                    bounds,
                    new Rectangle(0, 0, plane.Item2, plane.Item3),
                    viewport,
                    out var bottomFill));
                Assert.Equal(viewport.Bottom, bottomFill.Bounds.Bottom);
            }
            else
            {
                Assert.True(bounds.Bottom >= viewport.Bottom);
            }
            previousHorizon = layout.Horizon;
        }
    }
}
