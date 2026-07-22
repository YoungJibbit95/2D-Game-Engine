using Game.Client.Rendering.Effects;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class SolarIlluminationCurveTests
{
    [Fact]
    public void Evaluate_SeparatesDirectSunFromDiffuseTwilight()
    {
        var beforeSunrise = SolarIlluminationCurve.Evaluate(0.24f);
        var sunrise = SolarIlluminationCurve.Evaluate(0.25f);
        var morning = SolarIlluminationCurve.Evaluate(0.33f);
        var noon = SolarIlluminationCurve.Evaluate(0.5f);

        Assert.Equal(0f, beforeSunrise.DirectIrradiance);
        Assert.Equal(0f, sunrise.DirectIrradiance);
        Assert.True(sunrise.DiffuseIrradiance > 0.2f);
        Assert.True(morning.DirectIrradiance > 0.4f);
        Assert.InRange(noon.DirectIrradiance, 0.999f, 1f);
        Assert.InRange(noon.DiffuseIrradiance, 0.999f, 1f);
        Assert.True(beforeSunrise.LunarIrradiance > 0f);
        Assert.Equal(0f, noon.LunarIrradiance);
    }

    [Fact]
    public void Evaluate_ProducesNormalizedSunDirectionAcrossTheDay()
    {
        var morning = SolarIlluminationCurve.Evaluate(0.3f);
        var noon = SolarIlluminationCurve.Evaluate(0.5f);
        var evening = SolarIlluminationCurve.Evaluate(0.7f);

        Assert.InRange(morning.DirectionTowardPrimaryLight.Length(), 0.999f, 1.001f);
        Assert.InRange(noon.DirectionTowardPrimaryLight.Length(), 0.999f, 1.001f);
        Assert.InRange(evening.DirectionTowardPrimaryLight.Length(), 0.999f, 1.001f);
        Assert.True(morning.DirectionTowardPrimaryLight.X < 0f);
        Assert.InRange(MathF.Abs(noon.DirectionTowardPrimaryLight.X), 0f, 0.001f);
        Assert.True(evening.DirectionTowardPrimaryLight.X > 0f);
        Assert.All(
            new[] { morning, noon, evening },
            state => Assert.True(state.DirectionTowardPrimaryLight.Y < 0f));
    }

    [Fact]
    public void Evaluate_SanitizesNonFiniteTime()
    {
        var state = SolarIlluminationCurve.Evaluate(float.NaN);

        Assert.True(float.IsFinite(state.DirectIrradiance));
        Assert.True(float.IsFinite(state.DiffuseIrradiance));
        Assert.True(float.IsFinite(state.DirectionTowardPrimaryLight.X));
        Assert.True(float.IsFinite(state.DirectionTowardPrimaryLight.Y));
    }
}

