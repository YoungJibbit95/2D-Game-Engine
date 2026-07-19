using System.Diagnostics;
using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core.Settings;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace Game.Tests.PerformanceTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class LightingMaskPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public LightingMaskPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Medium1080pMask_RemainsWithinInteractiveCpuBudgetAndAllocatesNothing()
    {
        var world = new World(128, 96, WorldMetadata.CreateDefault(917));
        for (var y = 55; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                world.SetTile(x, y, TileInstance.FromTileId(1));
            }
        }

        var viewport = new Rectangle(0, 0, 1920, 1080);
        var profile = PresentationSettingsAdapter.Create(new RenderingSettings(), viewport).Lighting;
        var buffers = new Buffers(profile.MaskPixelCount);
        var frame = new LightingFrameParameters(
            NormalizedTimeOfDay: 0.213f,
            AmbientLight: 0.08f,
            SkyLightMultiplier: 1f,
            EmissiveLightMultiplier: 1f,
            CaveBlend: 0f,
            CaveResidualLight: 0.16f,
            ShadowStrength: 1f,
            BloomStrength: 0.65f,
            WeatherOcclusion: 0f,
            FrameIndex: 120);

        for (var iteration = 0; iteration < 8; iteration++)
        {
            Build(world, viewport, profile, frame, buffers);
        }

        const int samples = 40;
        var stopwatch = new Stopwatch();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Start();
        for (var iteration = 0; iteration < samples; iteration++)
        {
            Build(world, viewport, profile, frame with { FrameIndex = 120 + iteration }, buffers);
        }

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / samples;
#if DEBUG
        const double budgetMilliseconds = 12d;
#else
        const double budgetMilliseconds = 6d;
#endif
        _output.WriteLine(
            $"Medium 1080p lighting mask: {profile.MaskSize.X}x{profile.MaskSize.Y}, " +
            $"{averageMilliseconds:0.000} ms average, {allocated} B allocated.");

        Assert.Equal(0, allocated);
        Assert.True(
            averageMilliseconds < budgetMilliseconds,
            $"Medium 1080p lighting mask averaged {averageMilliseconds:0.000} ms; " +
            $"budget is {budgetMilliseconds:0} ms.");
    }

    private static void Build(
        World world,
        Rectangle viewport,
        PresentationQualityProfile profile,
        LightingFrameParameters frame,
        Buffers buffers)
    {
        _ = TileRayCastShadowMaskBuilder.Build(
            world,
            viewport,
            profile,
            frame,
            ReadOnlySpan<ScreenSpaceLight>.Empty,
            buffers.Shadow,
            buffers.Red,
            buffers.Green,
            buffers.Blue,
            buffers.Bloom,
            buffers.Scratch);
    }

    private sealed class Buffers
    {
        public Buffers(int count)
        {
            Shadow = new float[count];
            Red = new float[count];
            Green = new float[count];
            Blue = new float[count];
            Bloom = new float[count];
            Scratch = new float[count];
        }

        public float[] Shadow { get; }

        public float[] Red { get; }

        public float[] Green { get; }

        public float[] Blue { get; }

        public float[] Bloom { get; }

        public float[] Scratch { get; }
    }
}
