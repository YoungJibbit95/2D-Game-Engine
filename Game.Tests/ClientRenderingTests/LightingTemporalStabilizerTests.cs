using Game.Client.Rendering.Lighting;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class LightingTemporalStabilizerTests
{
    [Fact]
    public void Apply_ReprojectsHistoryThroughWorldSpaceDuringCameraMotion()
    {
        var previousShadow = new float[8];
        var previousRed = new float[8];
        var previousGreen = new float[8];
        var previousBlue = new float[8];
        previousShadow[1] = 0.4f;
        previousRed[1] = 0.2f;
        var currentShadow = new float[8];
        var currentRed = new float[8];
        var currentGreen = new float[8];
        var currentBlue = new float[8];
        currentShadow[0] = 0.42f;
        currentRed[0] = 0.22f;

        var telemetry = LightingTemporalStabilizer.Apply(
            new Rectangle(0, 0, 40, 20),
            new Rectangle(10, 0, 40, 20),
            new Point(4, 2),
            previousShadow,
            previousRed,
            previousGreen,
            previousBlue,
            currentShadow,
            currentRed,
            currentGreen,
            currentBlue);

        Assert.False(telemetry.HistoryRejected);
        Assert.Equal(6, telemetry.ReprojectedPixels);
        Assert.InRange(currentShadow[0], 0.407f, 0.409f);
        Assert.InRange(currentRed[0], 0.213f, 0.215f);
    }

    [Fact]
    public void Apply_RejectsLargeDisocclusionSoMinedTileUpdatesImmediately()
    {
        var previousShadow = Filled(4, 0.8f);
        var previousLight = new float[4];
        var currentShadow = new float[4];
        var currentRed = new float[4];
        var currentGreen = new float[4];
        var currentBlue = new float[4];

        var telemetry = LightingTemporalStabilizer.Apply(
            new Rectangle(0, 0, 32, 32),
            new Rectangle(0, 0, 32, 32),
            new Point(2, 2),
            previousShadow,
            previousLight,
            previousLight,
            previousLight,
            currentShadow,
            currentRed,
            currentGreen,
            currentBlue);

        Assert.Equal(4, telemetry.DisocclusionRejectedPixels);
        Assert.All(currentShadow, value => Assert.Equal(0f, value));
    }

    [Fact]
    public void Apply_HasZeroSteadyStateAllocation()
    {
        var previous = new float[128];
        var current = new float[128];
        var bounds = new Rectangle(-64, 32, 256, 128);
        var size = new Point(16, 8);
        _ = LightingTemporalStabilizer.Apply(
            bounds,
            bounds,
            size,
            previous,
            previous,
            previous,
            previous,
            current,
            current,
            current,
            current);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = LightingTemporalStabilizer.Apply(
                bounds,
                bounds,
                size,
                previous,
                previous,
                previous,
                previous,
                current,
                current,
                current,
                current);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static float[] Filled(int length, float value)
    {
        var result = new float[length];
        Array.Fill(result, value);
        return result;
    }
}

