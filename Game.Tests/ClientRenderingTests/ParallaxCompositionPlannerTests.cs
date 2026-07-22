using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParallaxCompositionPlannerTests
{
    [Fact]
    public void Build_IsDeterministicAcrossLargeNegativeCameraPositions()
    {
        var layer = CreateLayer(0xC0FFEE12u);
        var first = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
        var second = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
        var viewport = new Rectangle(0, 0, 1920, 1080);

        var firstCount = ParallaxCompositionPlanner.Build(
            layer,
            -24_987_654.75f,
            viewport,
            384,
            192,
            740,
            first);
        var secondCount = ParallaxCompositionPlanner.Build(
            layer,
            -24_987_654.75f,
            viewport,
            384,
            192,
            740,
            second);

        Assert.Equal(firstCount, secondCount);
        Assert.InRange(firstCount, 2, ParallaxCompositionPlanner.MaximumCommandCount);
        var hasNegativeCell = false;
        for (var index = 0; index < firstCount; index++)
        {
            Assert.Equal(first[index], second[index]);
            hasNegativeCell |= first[index].CellIndex < 0;
        }

        Assert.True(hasNegativeCell);
    }

    [Fact]
    public void Build_AlternatesAssetsAndBreaksAdjacentRepeatSilhouettes()
    {
        var layer = CreateLayer(0x5A17D3C9u);
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];

        var count = ParallaxCompositionPlanner.Build(
            layer,
            -3_200f,
            new Rectangle(0, 0, 2560, 1440),
            320,
            180,
            920,
            commands);

        var repeats = 0;
        var distinctPresentationCount = 0;
        var previousAlternate = false;
        var previousFlip = false;
        var previousY = 0;
        for (var index = 0; index < count; index++)
        {
            ref readonly var command = ref commands[index];
            if (command.Kind != ParallaxCompositionCommandKind.Repeat)
            {
                continue;
            }

            if (repeats > 0)
            {
                Assert.NotEqual(previousAlternate, command.UseAlternateSprite);
                if (previousFlip != command.FlipHorizontally || previousY != command.Bounds.Y)
                {
                    distinctPresentationCount++;
                }
            }

            previousAlternate = command.UseAlternateSprite;
            previousFlip = command.FlipHorizontally;
            previousY = command.Bounds.Y;
            repeats++;
        }

        Assert.InRange(repeats, 6, ParallaxCompositionPlanner.MaximumRepeatCommandCount);
        Assert.True(distinctPresentationCount >= 3);
    }

    [Fact]
    public void Build_PreservesAuthoredSeamForCompositePanoramas()
    {
        var layer = CreateLayer(0x5A17D3C9u) with
        {
            AlternateSpriteId = null,
            LandmarkStyle = ParallaxLandmarkStyle.None,
            RepeatOverlap = 0f,
            VerticalJitter = 0,
            LandmarkPeriod = 0,
            PreserveAuthoredRepeat = true
        };
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];

        var count = ParallaxCompositionPlanner.Build(
            layer,
            -3_200f,
            new Rectangle(0, 0, 2560, 1440),
            512,
            256,
            920,
            commands);

        var previousRight = int.MinValue;
        var repeats = 0;
        for (var index = 0; index < count; index++)
        {
            ref readonly var command = ref commands[index];
            Assert.Equal(ParallaxCompositionCommandKind.Repeat, command.Kind);
            Assert.False(command.UseAlternateSprite);
            Assert.False(command.FlipHorizontally);
            Assert.Equal(920, command.Bounds.Y);
            if (repeats > 0)
            {
                Assert.Equal(previousRight, command.Bounds.Left);
            }

            previousRight = command.Bounds.Right;
            repeats++;
        }

        Assert.InRange(repeats, 6, ParallaxCompositionPlanner.MaximumRepeatCommandCount);
    }

    [Theory]
    [InlineData(-1000000000f)]
    [InlineData(-4096f)]
    [InlineData(0f)]
    [InlineData(4096f)]
    [InlineData(1000000000f)]
    public void Build_KeepsCommandsInsideBoundedVisibleEnvelope(float cameraX)
    {
        var layer = CreateLayer(0x91E10DA5u);
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
        var viewport = new Rectangle(25, 40, 1600, 900);
        const int repeatWidth = 448;

        var count = ParallaxCompositionPlanner.Build(
            layer,
            cameraX,
            viewport,
            repeatWidth,
            224,
            650,
            commands);

        Assert.InRange(count, 1, ParallaxCompositionPlanner.MaximumCommandCount);
        for (var index = 0; index < count; index++)
        {
            var command = commands[index];
            Assert.True(command.Bounds.Width > 0);
            Assert.True(command.Bounds.Height > 0);
            if (command.Kind == ParallaxCompositionCommandKind.Landmark)
            {
                Assert.True(command.Bounds.Right > viewport.Left);
                Assert.True(command.Bounds.Left < viewport.Right);
                Assert.True(command.Bounds.Bottom > viewport.Top);
                Assert.True(command.Bounds.Top < viewport.Bottom);
                continue;
            }

            Assert.True((long)command.Bounds.Right > viewport.Left - repeatWidth);
            Assert.True((long)command.Bounds.Left < viewport.Right + repeatWidth);
        }
    }

    [Fact]
    public void Build_UsesFixedCapacityAndAllocatesNothingInSteadyState()
    {
        var layer = CreateLayer(0x11335577u);
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
        var viewport = new Rectangle(0, 0, 7680, 2160);
        _ = ParallaxCompositionPlanner.Build(layer, -512f, viewport, 192, 128, 720, commands);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var maximumObserved = 0;
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var count = ParallaxCompositionPlanner.Build(
                layer,
                -512f - iteration * 0.25f,
                viewport,
                192,
                128,
                720,
                commands);
            maximumObserved = Math.Max(maximumObserved, count);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(ParallaxCompositionPlanner.MaximumCommandCount, commands.Length);
        Assert.InRange(maximumObserved, 1, ParallaxCompositionPlanner.MaximumCommandCount);
        Assert.Throws<ArgumentException>(() => ParallaxCompositionPlanner.Build(
            layer,
            0f,
            viewport,
            192,
            128,
            720,
            new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount - 1]));
    }

    [Fact]
    public void Build_DistantBandRepeatsCoverUltraWideViewportWithoutGaps()
    {
        var layer = CreateLayer(0x5511AA77u) with
        {
            ProjectionMode = ParallaxProjectionMode.DistantHorizonBand
        };
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
        var viewport = new Rectangle(0, 0, 7680, 1080);

        var count = ParallaxCompositionPlanner.Build(
            layer,
            cameraX: -8_192f,
            viewport,
            repeatWidth: 256,
            repeatHeight: 144,
            baseY: 500,
            commands);
        var repeats = commands.AsSpan(0, count).ToArray()
            .Where(command => command.Kind == ParallaxCompositionCommandKind.Repeat)
            .OrderBy(command => command.Bounds.X)
            .ToArray();

        Assert.NotEmpty(repeats);
        Assert.True(repeats[0].Bounds.Left <= viewport.Left);
        Assert.True(repeats[^1].Bounds.Right >= viewport.Right);
        for (var index = 1; index < repeats.Length; index++)
        {
            Assert.True(repeats[index].Bounds.Left <= repeats[index - 1].Bounds.Right);
        }
    }

    [Theory]
    [InlineData(1280, 720, 1, -24_987_654.75f)]
    [InlineData(1920, 1080, 1, 0f)]
    [InlineData(2560, 1440, 1, 8_192f)]
    [InlineData(3440, 1440, 1, -8_192f)]
    [InlineData(5120, 1440, 1, -24_987_654.75f)]
    [InlineData(7680, 2160, 2, 24_987_654.75f)]
    public void Build_FullscreenAuthoredRepeatsRemainContiguousAtNegativeAndUltrawideCoordinates(
        int viewportWidth,
        int viewportHeight,
        int pixelScale,
        float cameraX)
    {
        var layer = CreateLayer(0x5511AA77u) with
        {
            AlternateSpriteId = null,
            LandmarkStyle = ParallaxLandmarkStyle.None,
            RepeatOverlap = 0f,
            VerticalJitter = 0,
            LandmarkPeriod = 0,
            PreserveAuthoredRepeat = true,
            ProjectionMode = ParallaxProjectionMode.FullscreenDepthPlane
        };
        var commands = new ParallaxCompositionCommand[ParallaxCompositionPlanner.MaximumCommandCount];
        var viewport = new Rectangle(0, 0, viewportWidth, viewportHeight);
        var repeatWidth = 1536 * pixelScale;
        var repeatHeight = 384 * pixelScale;

        var count = ParallaxCompositionPlanner.Build(
            layer,
            cameraX,
            viewport,
            repeatWidth,
            repeatHeight,
            baseY: 320,
            commands);

        var firstRepeat = true;
        var previousRight = int.MinValue;
        var leftmost = int.MaxValue;
        var rightmost = int.MinValue;
        for (var index = 0; index < count; index++)
        {
            ref readonly var command = ref commands[index];
            Assert.Equal(ParallaxCompositionCommandKind.Repeat, command.Kind);
            Assert.False(command.UseAlternateSprite);
            Assert.False(command.FlipHorizontally);
            Assert.Equal(repeatWidth, command.Bounds.Width);
            Assert.Equal(repeatHeight, command.Bounds.Height);
            Assert.Equal(4f, command.Bounds.Width / (float)command.Bounds.Height);
            Assert.True(command.Bounds.Right > viewport.Left);
            Assert.True(command.Bounds.Left < viewport.Right);
            if (!firstRepeat)
            {
                Assert.Equal(previousRight, command.Bounds.Left);
            }

            firstRepeat = false;
            previousRight = command.Bounds.Right;
            leftmost = Math.Min(leftmost, command.Bounds.Left);
            rightmost = Math.Max(rightmost, command.Bounds.Right);
        }

        Assert.False(firstRepeat);
        Assert.True(leftmost <= viewport.Left);
        Assert.True(rightmost >= viewport.Right);
    }

    private static ParallaxLayerDescriptor CreateLayer(uint seed)
    {
        return new ParallaxLayerDescriptor(
            "world/backgrounds/wave04/forest_parallax_layer",
            "world/backgrounds/forest_parallax_layer",
            null,
            0,
            ParallaxLandmarkStyle.Canopy,
            0.18f,
            0.04f,
            0.8f,
            -24,
            1f,
            0.1f,
            9,
            4,
            seed,
            false,
            ParallaxVerticalFillMode.None,
            Color.White,
            new Color(45, 78, 59));
    }
}
