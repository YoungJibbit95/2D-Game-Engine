using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class SolarPresentationRegressionMatrixTests
{
    [Fact]
    public void Curve_IsPeriodicFiniteAndBoundedAcrossMultipleDays()
    {
        const int sampleCount = 4_096;

        for (var index = -sampleCount * 2; index <= sampleCount * 2; index++)
        {
            var time = index / (float)sampleCount;
            var state = SolarIlluminationCurve.Evaluate(time);
            var nextDay = SolarIlluminationCurve.Evaluate(time + 1f);

            Assert.InRange(state.Elevation, -1f, 1f);
            Assert.InRange(state.DirectIrradiance, 0f, 1f);
            Assert.InRange(state.LunarIrradiance, 0f, 0.12f);
            Assert.InRange(state.DiffuseIrradiance, 0.2f, 1f);
            Assert.InRange(state.NightBlend, 0f, 1f);
            Assert.InRange(state.DirectionTowardPrimaryLight.Length(), 0.999f, 1.001f);
            Assert.True(state.DirectionTowardPrimaryLight.Y < 0f);
            AssertStatesNear(state, nextDay, 0.00001f);
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.75f)]
    [InlineData(1f)]
    public void Curve_IsContinuousAtDayNightAndWrapBoundaries(float boundary)
    {
        const float epsilon = 0.0001f;
        var before = SolarIlluminationCurve.Evaluate(boundary - epsilon);
        var at = SolarIlluminationCurve.Evaluate(boundary);
        var after = SolarIlluminationCurve.Evaluate(boundary + epsilon);

        AssertRadianceNear(before, at, 0.003f);
        AssertRadianceNear(at, after, 0.003f);
    }

    [Fact]
    public void Curve_KeepsDayBrightAndNightReadableWithLunarRadiance()
    {
        var sunrise = SolarIlluminationCurve.Evaluate(0.25f);
        var morning = SolarIlluminationCurve.Evaluate(0.35f);
        var noon = SolarIlluminationCurve.Evaluate(0.5f);
        var evening = SolarIlluminationCurve.Evaluate(0.65f);
        var sunset = SolarIlluminationCurve.Evaluate(0.75f);
        var midnight = SolarIlluminationCurve.Evaluate(0f);

        Assert.InRange(sunrise.DiffuseIrradiance, 0.75f, 1f);
        Assert.InRange(sunset.DiffuseIrradiance, 0.75f, 1f);
        Assert.True(morning.DirectIrradiance > 0.75f);
        Assert.True(evening.DirectIrradiance > 0.75f);
        Assert.InRange(noon.DirectIrradiance, 0.999f, 1f);
        Assert.InRange(noon.DiffuseIrradiance, 0.999f, 1f);
        Assert.Equal(0f, midnight.DirectIrradiance);
        Assert.InRange(midnight.LunarIrradiance, 0.1f, 0.12f);
        Assert.InRange(midnight.DiffuseIrradiance, 0.2f, 0.201f);
        Assert.InRange(midnight.NightBlend, 0.999f, 1f);
    }

    [Theory]
    [InlineData(0.02f)]
    [InlineData(0.08f)]
    [InlineData(0.16f)]
    [InlineData(0.24f)]
    public void Curve_IsSymmetricAroundNoon(float daylightOffset)
    {
        var morning = SolarIlluminationCurve.Evaluate(0.5f - daylightOffset);
        var evening = SolarIlluminationCurve.Evaluate(0.5f + daylightOffset);

        AssertNear(morning.Elevation, evening.Elevation, 0.00001f);
        AssertNear(morning.DirectIrradiance, evening.DirectIrradiance, 0.00001f);
        AssertNear(morning.DiffuseIrradiance, evening.DiffuseIrradiance, 0.00001f);
        AssertNear(morning.NightBlend, evening.NightBlend, 0.00001f);
        AssertNear(
            morning.DirectionTowardPrimaryLight.X,
            -evening.DirectionTowardPrimaryLight.X,
            0.00001f);
        AssertNear(
            morning.DirectionTowardPrimaryLight.Y,
            evening.DirectionTowardPrimaryLight.Y,
            0.00001f);
    }

    [Fact]
    public void Evaluate_FullDayTraceIsAllocationFreeAfterWarmup()
    {
        var checksum = 0f;
        for (var warmup = 0; warmup < 1_024; warmup++)
        {
            checksum += SolarIlluminationCurve.Evaluate(warmup / 1024f).DiffuseIrradiance;
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100_000; iteration++)
        {
            var state = SolarIlluminationCurve.Evaluate(iteration / 4096f);
            checksum += state.DirectIrradiance + state.LunarIrradiance + state.DiffuseIrradiance;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(checksum > 0f);
        Assert.Equal(0, allocated);
    }

    private static void AssertStatesNear(
        in SolarLightState expected,
        in SolarLightState actual,
        float tolerance)
    {
        AssertNear(expected.Elevation, actual.Elevation, tolerance);
        AssertNear(expected.DirectionTowardPrimaryLight.X, actual.DirectionTowardPrimaryLight.X, tolerance);
        AssertNear(expected.DirectionTowardPrimaryLight.Y, actual.DirectionTowardPrimaryLight.Y, tolerance);
        AssertNear(expected.DirectIrradiance, actual.DirectIrradiance, tolerance);
        AssertNear(expected.LunarIrradiance, actual.LunarIrradiance, tolerance);
        AssertNear(expected.DiffuseIrradiance, actual.DiffuseIrradiance, tolerance);
        AssertNear(expected.NightBlend, actual.NightBlend, tolerance);
    }

    private static void AssertRadianceNear(
        in SolarLightState expected,
        in SolarLightState actual,
        float tolerance)
    {
        AssertNear(expected.Elevation, actual.Elevation, tolerance);
        AssertNear(expected.DirectIrradiance, actual.DirectIrradiance, tolerance);
        AssertNear(expected.LunarIrradiance, actual.LunarIrradiance, tolerance);
        AssertNear(expected.DiffuseIrradiance, actual.DiffuseIrradiance, tolerance);
        AssertNear(expected.NightBlend, actual.NightBlend, tolerance);
    }

    private static void AssertNear(float expected, float actual, float tolerance)
    {
        Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);
    }
}
