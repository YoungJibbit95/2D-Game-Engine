using Game.Client.Rendering.Effects;
using Game.Core.Runtime;
using Game.Core.Weather;
using Game.Tests.PerformanceTests;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class WeatherPresentationRegressionMatrixTests
{
    [Theory]
    [InlineData("forest", WeatherKind.Snow)]
    [InlineData("forest", WeatherKind.Blizzard)]
    [InlineData("meadow", WeatherKind.Snow)]
    [InlineData("amber_grove", WeatherKind.Blizzard)]
    [InlineData("twilight_marsh", WeatherKind.Snow)]
    [InlineData("crystal_depths", WeatherKind.Blizzard)]
    public void NonFrostBiomes_RejectFrozenPrecipitationEvenWithMisleadingPresentationMetadata(
        string biomeId,
        WeatherKind weather)
    {
        var snapshot = CreateSnapshot(
            biomeId,
            weather,
            allowsFrozenPrecipitation: false) with
        {
            Presentation = default(LivingWorldPresentationFrameSnapshot) with
            {
                BackgroundSpriteId = "world/backgrounds/snow_panorama",
                AmbientParticleSpriteId = "effects/weather/snow_flurry"
            }
        };

        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(snapshot));
        Assert.Equal(
            WeatherParticlePresentationKind.Rain,
            WeatherParticlePresentation.Resolve(snapshot with { Weather = WeatherKind.Rain }));
    }

    [Fact]
    public void PrecipitationGeometry_MatrixIsSparseClippedAndNeverProducesFullscreenRectangles()
    {
        var viewports = new[]
        {
            new Rectangle(0, 0, 320, 180),
            new Rectangle(-40, 30, 1280, 720),
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(240, -120, 3440, 1440),
            new Rectangle(-512, 96, 7680, 2160)
        };
        var kinds = new[]
        {
            WeatherParticlePresentationKind.Rain,
            WeatherParticlePresentationKind.Storm,
            WeatherParticlePresentationKind.Snow,
            WeatherParticlePresentationKind.Blizzard
        };
        var sampleTimes = new[]
        {
            double.NegativeInfinity,
            -1_000_000d,
            0d,
            87.25d,
            1_000_000d,
            double.NaN,
            double.PositiveInfinity
        };

        foreach (var viewport in viewports)
        {
            foreach (var kind in kinds)
            {
                foreach (var sampleTime in sampleTimes)
                {
                    var count = WeatherParticlePresentation.ResolveDepthPrimitiveCount(kind, 1f);
                    var built = 0;
                    var coveredArea = 0L;

                    for (var index = 0; index < count; index++)
                    {
                        if (!WeatherParticlePresentation.TryBuildDepthPrimitive(
                                kind,
                                viewport,
                                sampleTime,
                                wind: 1f,
                                intensity: 1f,
                                index,
                                out var primitive))
                        {
                            continue;
                        }

                        built++;
                        var area = (long)primitive.Bounds.Width * primitive.Bounds.Height;
                        coveredArea += area;
                        Assert.InRange(
                            primitive.Bounds.Width,
                            1,
                            WeatherParticlePresentation.MaximumPrimitiveWidth);
                        Assert.InRange(
                            primitive.Bounds.Height,
                            1,
                            WeatherParticlePresentation.MaximumPrimitiveHeight);
                        Assert.NotEqual(viewport, primitive.Bounds);
                        Assert.True(primitive.Bounds.Left >= viewport.Left);
                        Assert.True(primitive.Bounds.Top >= viewport.Top);
                        Assert.True(primitive.Bounds.Right <= viewport.Right);
                        Assert.True(primitive.Bounds.Bottom <= viewport.Bottom);
                    }

                    Assert.InRange(built, 1, WeatherParticlePresentation.MaximumDepthPrimitiveCount);
                    Assert.True(coveredArea > 0);
                    Assert.True(coveredArea < (long)viewport.Width * viewport.Height / 25L);
                }
            }
        }
    }

    [Fact]
    public void NonPrecipitationKinds_NeverScheduleDepthGeometry()
    {
        var viewport = new Rectangle(0, 0, 1920, 1080);
        var kinds = new[]
        {
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentationKind.Fog
        };

        foreach (var kind in kinds)
        {
            Assert.Equal(0, WeatherParticlePresentation.ResolveDepthPrimitiveCount(kind, 1f));
            Assert.False(WeatherParticlePresentation.TryBuildDepthPrimitive(
                kind,
                viewport,
                totalSeconds: 10d,
                wind: 0f,
                intensity: 1f,
                index: 0,
                out _));
        }

        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(CreateSnapshot(
                "forest",
                WeatherKind.Fog,
                allowsFrozenPrecipitation: false)));
        Assert.Equal(
            WeatherParticlePresentationKind.None,
            WeatherParticlePresentation.Resolve(CreateSnapshot(
                "forest",
                WeatherKind.Clear,
                allowsFrozenPrecipitation: false)));
    }

    [Fact]
    public void ResolveAndBuild_FullWeatherTraceIsAllocationFreeAfterWarmup()
    {
        var viewport = new Rectangle(-320, 73, 3440, 1440);
        var snapshot = CreateSnapshot(
            "frostwood",
            WeatherKind.Blizzard,
            allowsFrozenPrecipitation: true);
        long checksum = 0;

        for (var warmup = 0; warmup < 1_024; warmup++)
        {
            checksum += BuildWeatherFrame(snapshot, viewport, warmup);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 20_000; iteration++)
        {
            checksum += BuildWeatherFrame(snapshot, viewport, iteration);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.NotEqual(0, checksum);
        Assert.Equal(0, allocated);
    }

    private static int BuildWeatherFrame(
        in LivingWorldFrameSnapshot snapshot,
        in Rectangle viewport,
        int iteration)
    {
        var weather = (iteration & 3) switch
        {
            0 => WeatherKind.Rain,
            1 => WeatherKind.Storm,
            2 => WeatherKind.Snow,
            _ => WeatherKind.Blizzard
        };
        var frame = snapshot with { Weather = weather };
        var kind = WeatherParticlePresentation.Resolve(frame);
        var count = WeatherParticlePresentation.ResolveDepthPrimitiveCount(kind, frame.WeatherIntensity);
        if (count == 0)
        {
            return 0;
        }

        return WeatherParticlePresentation.TryBuildDepthPrimitive(
            kind,
            viewport,
            iteration / 60d,
            frame.Wind,
            frame.WeatherIntensity,
            iteration % count,
            out var primitive)
            ? primitive.Bounds.X ^ primitive.Bounds.Y ^ primitive.Color.A
            : count;
    }

    private static LivingWorldFrameSnapshot CreateSnapshot(
        string biomeId,
        WeatherKind weather,
        bool allowsFrozenPrecipitation)
    {
        return default(LivingWorldFrameSnapshot) with
        {
            BiomeId = biomeId,
            Weather = weather,
            WeatherIntensity = 0.9f,
            Wind = 0.72f,
            AllowsFrozenPrecipitation = allowsFrozenPrecipitation
        };
    }
}
