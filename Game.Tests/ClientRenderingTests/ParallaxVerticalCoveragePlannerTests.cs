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
    public void Build_ExtendsAuthoredTopColorAcrossViewportWithoutTouchingPanorama(
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
    [InlineData(1280, 720, 420)]
    [InlineData(1920, 1080, 640)]
    [InlineData(2560, 1440, 462)]
    public void Build_ExtendsOpaquePanoramaEdgeThroughViewportBottom(int width, int height, int layerBottom)
    {
        var viewport = new Rectangle(0, 0, width, height);
        var layer = new Rectangle(-128, layerBottom - 384, width + 256, 384);
        var source = new Rectangle(12, 20, 1536, 384);

        var built = ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
            layer,
            source,
            viewport,
            out var command);

        Assert.True(built);
        Assert.Equal(layerBottom - 1, command.Bounds.Top);
        Assert.Equal(viewport.Bottom, command.Bounds.Bottom);
        Assert.Equal(layer.X, command.Bounds.X);
        Assert.Equal(layer.Width, command.Bounds.Width);
        Assert.Equal(source.X + source.Width / 2, command.SourceRectangle.X);
        Assert.Equal(source.Bottom - 1, command.SourceRectangle.Y);
        Assert.Equal(1, command.SourceRectangle.Width);
        Assert.Equal(1, command.SourceRectangle.Height);
    }

    [Fact]
    public void Build_ClampsExtensionToViewportWhenPanoramaEndsAboveIt()
    {
        var viewport = new Rectangle(25, 40, 1280, 720);

        var built = ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
            new Rectangle(-100, -400, 1600, 200),
            new Rectangle(0, 0, 512, 128),
            viewport,
            out var command);

        Assert.True(built);
        Assert.Equal(viewport.Top, command.Bounds.Top);
        Assert.Equal(viewport.Bottom, command.Bounds.Bottom);
    }

    [Fact]
    public void Build_DoesNothingWhenPanoramaAlreadyCoversViewportBottom()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);

        Assert.False(ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
            new Rectangle(0, -100, 1920, 1180),
            new Rectangle(0, 0, 1536, 384),
            viewport,
            out _));
        Assert.False(ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
            new Rectangle(0, -100, 1920, 1200),
            new Rectangle(0, 0, 1536, 384),
            viewport,
            out _));
    }

    [Fact]
    public void Build_TopExtensionDoesNothingWhenPanoramaAlreadyCoversViewportTop()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        Assert.False(ParallaxVerticalCoveragePlanner.TryBuildTopFill(
            new Rectangle(-100, -1, 1536, 384),
            viewport,
            out _));
        Assert.False(ParallaxVerticalCoveragePlanner.TryBuildTopFill(
            new Rectangle(-100, -200, 1536, 384),
            viewport,
            out _));
    }

    [Fact]
    public void Build_IsAllocationFreeInSteadyState()
    {
        var viewport = new Rectangle(0, 0, 2560, 1440);
        var layer = new Rectangle(-320, -562, 3072, 1024);
        var source = new Rectangle(0, 0, 1536, 384);
        _ = ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
            layer,
            source,
            viewport,
            out var command);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var allBuilt = true;

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            allBuilt &= ParallaxVerticalCoveragePlanner.TryBuildBottomEdgeExtension(
                layer,
                source,
                viewport,
                out command);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allBuilt);
        Assert.Equal(viewport.Bottom, command.Bounds.Bottom);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Build_TopExtensionIsAllocationFreeInSteadyState()
    {
        var viewport = new Rectangle(0, 0, 3440, 1440);
        var layer = new Rectangle(-320, 501, 1536, 384);
        var bounds = Rectangle.Empty;
        for (var warmup = 0; warmup < 1_000; warmup++)
        {
            _ = ParallaxVerticalCoveragePlanner.TryBuildTopFill(
                layer,
                viewport,
                out bounds);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var allBuilt = true;

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            allBuilt &= ParallaxVerticalCoveragePlanner.TryBuildTopFill(
                layer,
                viewport,
                out bounds);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allBuilt);
        Assert.Equal(viewport.Top, bounds.Top);
        Assert.Equal(0, allocated);
    }
}
