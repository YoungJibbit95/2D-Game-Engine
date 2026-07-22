using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParallaxProjectionRegressionMatrixTests
{
    [Fact]
    public void FullscreenProjection_MatrixPreservesAspectAndFiniteVerticalCoverage()
    {
        var viewports = new[]
        {
            new Rectangle(0, 0, 320, 180),
            new Rectangle(-73, 41, 1280, 720),
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(320, -180, 3440, 1440),
            new Rectangle(-512, 96, 7680, 2160)
        };
        var sources = new[]
        {
            (Width: 1536, Height: 384, Plane: ParallaxDepthPlane.Far),
            (Width: 1024, Height: 256, Plane: ParallaxDepthPlane.Mid),
            (Width: 512, Height: 128, Plane: ParallaxDepthPlane.Near)
        };
        var verticalScrolls = new[]
        {
            -1_000_000f,
            -96f,
            0f,
            96f,
            1_000_000f,
            float.NaN,
            float.PositiveInfinity
        };
        var scaleMultipliers = new[] { 0.25f, 0.5f, 1f, 4f, float.NaN };

        foreach (var viewport in viewports)
        {
            foreach (var source in sources)
            {
                foreach (var verticalScroll in verticalScrolls)
                {
                    foreach (var scaleMultiplier in scaleMultipliers)
                    {
                        var layout = ParallaxViewportLayoutPlanner.Build(
                            source.Width,
                            source.Height,
                            viewport,
                            surfaceHorizon: viewport.Top - 100_000f,
                            undergroundBlend: 0.73f,
                            verticalOffset: 17,
                            verticalScroll,
                            scaleMultiplier,
                            coverViewport: false,
                            ParallaxProjectionMode.FullscreenDepthPlane,
                            source.Plane);
                        var bounds = new Rectangle(
                            viewport.X,
                            layout.Y,
                            layout.Width,
                            layout.Height);
                        var minimumIntegerScale = Math.Max(
                            1,
                            (int)MathF.Ceiling(viewport.Height / (float)source.Height));

                        Assert.True(float.IsFinite(layout.Scale));
                        Assert.Equal(0f, layout.Scale % 1f);
                        Assert.InRange(layout.Scale, minimumIntegerScale, minimumIntegerScale * 4f);
                        Assert.Equal(
                            (long)layout.Width * source.Height,
                            (long)layout.Height * source.Width);
                        Assert.True(ParallaxVerticalCoveragePlanner.CoversViewportVertically(
                            bounds,
                            viewport));
                        Assert.False(ParallaxVerticalCoveragePlanner.TryBuildTopFill(
                            bounds,
                            viewport,
                            out _));
                        Assert.InRange(layout.Y, viewport.Bottom - layout.Height, viewport.Top);
                    }
                }
            }
        }
    }

    [Fact]
    public void FullscreenComposition_RepeatsOnlyHorizontallyWithOneStableVerticalProjection()
    {
        var viewports = new[]
        {
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(-320, 73, 3440, 1440),
            new Rectangle(160, -90, 7680, 2160)
        };
        var cameraPositions = new[]
        {
            -24_987_654.75f,
            -8192f,
            0f,
            8192f,
            24_987_654.75f
        };
        var layer = CreateFullscreenLayer();
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];

        foreach (var viewport in viewports)
        {
            var layout = ParallaxViewportLayoutPlanner.Build(
                sourceWidth: 1536,
                sourceHeight: 384,
                viewport,
                surfaceHorizon: viewport.Bottom + 50_000f,
                undergroundBlend: 1f,
                verticalOffset: -8,
                verticalScroll: 40_000f,
                scaleMultiplier: 0.5f,
                coverViewport: false,
                ParallaxProjectionMode.FullscreenDepthPlane,
                ParallaxDepthPlane.Far);

            foreach (var cameraX in cameraPositions)
            {
                var count = ParallaxCompositionPlanner.Build(
                    layer,
                    cameraX,
                    viewport,
                    layout.Width,
                    layout.Height,
                    layout.Y,
                    commands);
                var previousRight = int.MinValue;
                var firstLeft = int.MaxValue;
                var lastRight = int.MinValue;

                Assert.InRange(count, 1, ParallaxCompositionPlanner.MaximumRepeatCommandCount);
                for (var index = 0; index < count; index++)
                {
                    ref readonly var command = ref commands[index];
                    Assert.Equal(ParallaxCompositionCommandKind.Repeat, command.Kind);
                    Assert.Equal(layout.Y, command.Bounds.Y);
                    Assert.Equal(layout.Height, command.Bounds.Height);
                    Assert.Equal(layout.Width, command.Bounds.Width);
                    Assert.False(command.UseAlternateSprite);
                    Assert.False(command.FlipHorizontally);

                    if (previousRight != int.MinValue)
                    {
                        Assert.Equal(previousRight, command.Bounds.Left);
                    }

                    previousRight = command.Bounds.Right;
                    firstLeft = Math.Min(firstLeft, command.Bounds.Left);
                    lastRight = Math.Max(lastRight, command.Bounds.Right);
                }

                Assert.True(firstLeft <= viewport.Left);
                Assert.True(lastRight >= viewport.Right);
            }
        }
    }

    [Fact]
    public void LegacyTopFill_IsBoundedToTheFiniteGapAboveTheLayer()
    {
        var viewport = new Rectangle(-125, 75, 2560, 1440);

        for (var layerTop = viewport.Top + 1;
             layerTop <= viewport.Bottom + 256;
             layerTop += 37)
        {
            var layer = new Rectangle(viewport.Left - 512, layerTop, 1536, 384);

            Assert.True(ParallaxVerticalCoveragePlanner.TryBuildTopFill(
                layer,
                viewport,
                out var fill));
            Assert.Equal(viewport.Left, fill.Left);
            Assert.Equal(viewport.Right, fill.Right);
            Assert.Equal(viewport.Top, fill.Top);
            Assert.Equal(Math.Min(layerTop + 1, viewport.Bottom), fill.Bottom);
            Assert.InRange(fill.Height, 1, viewport.Height);
        }
    }

    [Fact]
    public void ProjectionAndComposition_AreAllocationFreeAfterWarmup()
    {
        var viewport = new Rectangle(-320, 73, 3440, 1440);
        var layer = CreateFullscreenLayer();
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
        long checksum = 0;

        for (var warmup = 0; warmup < 1_024; warmup++)
        {
            checksum += BuildFrame(layer, viewport, warmup * 0.25f, commands);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 20_000; iteration++)
        {
            checksum += BuildFrame(layer, viewport, iteration * -0.25f, commands);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotEqual(0, checksum);
        Assert.Equal(0, allocated);
    }

    private static int BuildFrame(
        in ParallaxLayerDescriptor layer,
        in Rectangle viewport,
        float cameraX,
        Span<ParallaxCompositionCommand> commands)
    {
        var layout = ParallaxViewportLayoutPlanner.Build(
            sourceWidth: 1536,
            sourceHeight: 384,
            viewport,
            surfaceHorizon: 640f,
            undergroundBlend: 0f,
            verticalOffset: -8,
            verticalScroll: cameraX * 0.0012f,
            scaleMultiplier: 0.5f,
            coverViewport: false,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Far);
        return ParallaxCompositionPlanner.Build(
            layer,
            cameraX,
            viewport,
            layout.Width,
            layout.Height,
            layout.Y,
            commands);
    }

    private static ParallaxLayerDescriptor CreateFullscreenLayer()
    {
        return new ParallaxLayerDescriptor(
            "world/backgrounds/depth_v6/forest_far",
            null,
            null,
            0,
            ParallaxLandmarkStyle.None,
            0.035f,
            0.0012f,
            0.62f,
            -8,
            0.5f,
            0f,
            0,
            0,
            0xA24BAED5u,
            true,
            ParallaxVerticalFillMode.None,
            Color.White,
            Color.White)
        {
            ProjectionMode = ParallaxProjectionMode.FullscreenDepthPlane,
            DepthPlane = ParallaxDepthPlane.Far
        };
    }
}
