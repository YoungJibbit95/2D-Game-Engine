using Game.Core.Lighting;
using Xunit;

namespace Game.Tests.LightingTests;

public sealed class SolarRadianceModelTests
{
    [Fact]
    public void Evaluate_IsSymmetricInEnergyAndDirectionalAcrossNoon()
    {
        var morning = SolarRadianceModel.Evaluate(0.3d);
        var evening = SolarRadianceModel.Evaluate(0.7d);

        Assert.Equal(morning.DirectIrradiance, evening.DirectIrradiance, 5);
        Assert.Equal(morning.DiffuseIrradiance, evening.DiffuseIrradiance, 5);
        Assert.True(morning.HorizontalDirection < 0f);
        Assert.True(evening.HorizontalDirection > 0f);
        Assert.Equal(morning.VerticalDirection, evening.VerticalDirection, 5);
    }

    [Fact]
    public void Evaluate_UsesDiffuseTwilightWithoutDirectSunBelowHorizon()
    {
        var night = SolarRadianceModel.Evaluate(0d);
        var beforeSunrise = SolarRadianceModel.Evaluate(0.24d);
        var sunrise = SolarRadianceModel.Evaluate(0.25d);

        Assert.Equal(0f, night.DirectIrradiance);
        Assert.Equal(0f, beforeSunrise.DirectIrradiance);
        Assert.Equal(0f, sunrise.DirectIrradiance);
        Assert.True(sunrise.DiffuseIrradiance > beforeSunrise.DiffuseIrradiance);
        Assert.InRange(night.DiffuseIrradiance, 0.199f, 0.201f);
        Assert.InRange(night.LunarIrradiance, 0.109f, 0.111f);
        Assert.Equal(0f, SolarRadianceModel.Evaluate(0.5d).LunarIrradiance);
    }

    [Theory]
    [InlineData(-0.25d, 0.75d)]
    [InlineData(1.25d, 0.25d)]
    [InlineData(12.5d, 0.5d)]
    public void WrapUnit_HandlesNegativeAndLargeTimes(double input, double expected)
    {
        Assert.Equal(expected, SolarRadianceModel.WrapUnit(input), 8);
    }

    [Fact]
    public void Evaluate_HasZeroSteadyStateAllocation()
    {
        _ = SolarRadianceModel.Evaluate(0.42d);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            _ = SolarRadianceModel.Evaluate(iteration / 10_000d);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

