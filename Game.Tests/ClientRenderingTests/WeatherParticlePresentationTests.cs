using Game.Client.Rendering.Effects;
using Game.Core.Runtime;
using Game.Core.Weather;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class WeatherParticlePresentationTests
{
    [Fact]
    public void Resolve_UsesExplicitFrozenWeatherAndSuppressesItUnderground()
    {
        var snow = CreateFrozenSnapshot(WeatherKind.Snow);
        var blizzard = CreateFrozenSnapshot(WeatherKind.Blizzard);

        Assert.Equal(WeatherParticlePresentationKind.Snow, WeatherParticlePresentation.Resolve(snow));
        Assert.Equal(WeatherParticlePresentationKind.Blizzard, WeatherParticlePresentation.Resolve(blizzard));
        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(snow with { AllowsFrozenPrecipitation = false }));
        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(snow with { IsUnderground = true }));
        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(blizzard with { IsUnderground = true }));
    }

    [Fact]
    public void Resolve_DoesNotInferSnowFromBiomeOrSpriteNames()
    {
        var ordinaryRainWithMisleadingMetadata = CreateFrozenSnapshot(WeatherKind.Rain);

        Assert.Equal(
            WeatherParticlePresentationKind.Rain,
            WeatherParticlePresentation.Resolve(ordinaryRainWithMisleadingMetadata));
        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(
                ordinaryRainWithMisleadingMetadata with { Weather = WeatherKind.Fog }));
        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(
                ordinaryRainWithMisleadingMetadata with { WeatherIntensity = float.NaN }));
    }

    [Fact]
    public void PremultiplyAlpha_ProducesAlphaBlendSafeColors()
    {
        var color = WeatherParticlePresentation.PremultiplyAlpha(
            new Color(232, 241, 248),
            0.42f);

        Assert.InRange(color.A, 105, 108);
        Assert.True(color.R <= color.A);
        Assert.True(color.G <= color.A);
        Assert.True(color.B <= color.A);
        Assert.Equal(Color.Transparent, WeatherParticlePresentation.PremultiplyAlpha(Color.White, float.NaN));
    }

    [Theory]
    [InlineData(WeatherParticlePresentationKind.Snow, 4)]
    [InlineData(WeatherParticlePresentationKind.Blizzard, 28)]
    public void FrozenDepthParticles_AreClippedSparseAndNeverBecomeScreenFills(
        WeatherParticlePresentationKind kind,
        int maximumArea)
    {
        var viewport = new Rectangle(40, 30, 1920, 1080);
        var count = WeatherParticlePresentation.ResolveDepthPrimitiveCount(kind, 1f);
        var built = 0;
        var coveredPixels = 0;

        for (var index = 0; index < count; index++)
        {
            if (!WeatherParticlePresentation.TryBuildDepthPrimitive(
                    kind,
                    viewport,
                    totalSeconds: 87.25,
                    wind: 0.9f,
                    intensity: 1f,
                    index,
                    out var primitive))
            {
                continue;
            }

            built++;
            var area = primitive.Bounds.Width * primitive.Bounds.Height;
            coveredPixels += area;
            Assert.InRange(primitive.Bounds.Width, 1, WeatherParticlePresentation.MaximumPrimitiveWidth);
            Assert.InRange(primitive.Bounds.Height, 1, WeatherParticlePresentation.MaximumPrimitiveHeight);
            Assert.InRange(area, 1, maximumArea);
            Assert.True(viewport.Contains(primitive.Bounds.Left, primitive.Bounds.Top));
            Assert.True(primitive.Bounds.Right <= viewport.Right);
            Assert.True(primitive.Bounds.Bottom <= viewport.Bottom);
            Assert.InRange(primitive.Color.A, 1, 180);
            Assert.True(primitive.Color.R <= primitive.Color.A);
            Assert.True(primitive.Color.G <= primitive.Color.A);
            Assert.True(primitive.Color.B <= primitive.Color.A);
        }

        Assert.InRange(built, 1, WeatherParticlePresentation.MaximumDepthPrimitiveCount);
        Assert.InRange(
            coveredPixels,
            1,
            WeatherParticlePresentation.MaximumDepthPrimitiveCount * maximumArea);
        Assert.True(coveredPixels < viewport.Width * viewport.Height / 1_000);
    }

    [Fact]
    public void ResolveAndBuild_AreAllocationFreeInSteadyState()
    {
        var living = CreateFrozenSnapshot(WeatherKind.Blizzard);
        var viewport = new Rectangle(-320, -180, 1920, 1080);
        _ = WeatherParticlePresentation.Resolve(living);
        _ = WeatherParticlePresentation.TryBuildDepthPrimitive(
            WeatherParticlePresentationKind.Blizzard,
            viewport,
            1d,
            0.2f,
            0.8f,
            0,
            out _);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var kind = WeatherParticlePresentation.Resolve(living);
            _ = WeatherParticlePresentation.TryBuildDepthPrimitive(
                kind,
                viewport,
                iteration / 60d,
                0.2f,
                0.8f,
                iteration % 32,
                out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static LivingWorldFrameSnapshot CreateFrozenSnapshot(WeatherKind weather)
    {
        return default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "frostwood",
            Weather = weather,
            WeatherIntensity = 0.9f,
            AllowsFrozenPrecipitation = true,
            Presentation = default(LivingWorldPresentationFrameSnapshot) with
            {
                AmbientParticleSpriteId = "effects/weather/snow_flurry"
            }
        };
    }
}