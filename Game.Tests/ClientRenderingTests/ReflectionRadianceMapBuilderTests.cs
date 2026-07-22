using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class ReflectionRadianceMapBuilderTests
{
    [Fact]
    public void Build_ProjectsColoredRadianceOntoBoundedWaterSurface()
    {
        var viewport = new Rectangle(0, 0, 160, 80);
        var profile = CreateProfile(viewport, width: 10, height: 5);
        var red = new float[profile.MaskPixelCount];
        var green = new float[profile.MaskPixelCount];
        var blue = new float[profile.MaskPixelCount];
        var shadow = new float[profile.MaskPixelCount];
        var destination = new Color[profile.MaskPixelCount];
        for (var x = 2; x <= 7; x++)
        {
            red[2 * profile.MaskSize.X + x] = 1f;
        }

        var surfaces = new[]
        {
            new WaterReflectionSurface(
                new Rectangle(32, 48, 96, 16),
                ReflectionSurfaceKind.Water,
                Color.White,
                Reflectivity: 0.8f,
                Phase: 71)
        };

        var telemetry = ReflectionRadianceMapBuilder.Build(
            viewport,
            profile,
            CreateFrame(),
            surfaces,
            red,
            green,
            blue,
            shadow,
            reflectionStrength: 1f,
            destination);

        var reflected = destination[3 * profile.MaskSize.X + 4];
        Assert.Equal(1, telemetry.SurfaceCount);
        Assert.InRange(telemetry.PixelsShaded, 6, profile.MaskPixelCount);
        Assert.True(reflected.R > reflected.G + 20);
        Assert.True(reflected.R > reflected.B + 20);
        Assert.Equal(0, destination[4].R);
    }

    [Fact]
    public void Build_WaterAnimationIsDeterministicAndAdvancesEverySixFrames()
    {
        var viewport = new Rectangle(0, 0, 160, 80);
        var profile = CreateProfile(viewport, width: 10, height: 5);
        var red = new float[profile.MaskPixelCount];
        var green = new float[profile.MaskPixelCount];
        var blue = new float[profile.MaskPixelCount];
        var shadow = new float[profile.MaskPixelCount];
        for (var x = 2; x <= 7; x++)
        {
            red[2 * profile.MaskSize.X + x] = (x - 1) / 6f;
            blue[2 * profile.MaskSize.X + x] = (8 - x) / 6f;
        }

        var surfaces = new[]
        {
            new WaterReflectionSurface(
                new Rectangle(32, 48, 96, 16),
                ReflectionSurfaceKind.Water,
                Color.White,
                Reflectivity: 1f,
                Phase: 19)
        };
        var first = new Color[profile.MaskPixelCount];
        var repeated = new Color[profile.MaskPixelCount];
        var advanced = new Color[profile.MaskPixelCount];

        _ = ReflectionRadianceMapBuilder.Build(
            viewport, profile, CreateFrame(), surfaces, red, green, blue, shadow, 1f, first);
        _ = ReflectionRadianceMapBuilder.Build(
            viewport, profile, CreateFrame(), surfaces, red, green, blue, shadow, 1f, repeated);
        _ = ReflectionRadianceMapBuilder.Build(
            viewport,
            profile,
            CreateFrame() with { FrameIndex = CreateFrame().FrameIndex + 6 },
            surfaces,
            red,
            green,
            blue,
            shadow,
            1f,
            advanced);

        Assert.True(first.AsSpan().SequenceEqual(repeated));
        Assert.False(first.AsSpan().SequenceEqual(advanced));
    }

    [Fact]
    public void Build_WetSurfaceSamplesWithoutHorizontalRefraction()
    {
        var viewport = new Rectangle(0, 0, 160, 80);
        var profile = CreateProfile(viewport, width: 10, height: 5);
        var red = new float[profile.MaskPixelCount];
        var values = new float[profile.MaskPixelCount];
        red[2 * profile.MaskSize.X + 4] = 1f;
        var destination = new Color[profile.MaskPixelCount];
        var surfaces = new[]
        {
            new WaterReflectionSurface(
                new Rectangle(32, 48, 96, 16),
                ReflectionSurfaceKind.WetSolid,
                Color.White,
                Reflectivity: 1f,
                Phase: 19)
        };

        _ = ReflectionRadianceMapBuilder.Build(
            viewport, profile, CreateFrame(), surfaces, red, values, values, values, 1f, destination);

        var row = 3 * profile.MaskSize.X;
        Assert.True(destination[row + 4].R > destination[row + 3].R + 10);
        Assert.True(destination[row + 4].R > destination[row + 5].R + 10);
    }

    [Fact]
    public void Build_ClearsStaleDataAndClampsSurfaceCount()
    {
        var viewport = new Rectangle(0, 0, 64, 64);
        var profile = CreateProfile(viewport, width: 4, height: 4) with
        {
            Budget = CreateProfile(viewport, 4, 4).Budget with
            {
                MaxReflectionSurfaces = 1
            }
        };
        var values = new float[profile.MaskPixelCount];
        var destination = new Color[profile.MaskPixelCount];
        destination.AsSpan().Fill(Color.Red);
        var surfaces = new[]
        {
            new WaterReflectionSurface(new Rectangle(0, 32, 32, 16), ReflectionSurfaceKind.Water, Color.White, 1f, 1),
            new WaterReflectionSurface(new Rectangle(32, 32, 32, 16), ReflectionSurfaceKind.Water, Color.White, 1f, 2)
        };

        var telemetry = ReflectionRadianceMapBuilder.Build(
            viewport,
            profile,
            CreateFrame(),
            surfaces,
            values,
            values,
            values,
            values,
            1f,
            destination);

        Assert.Equal(1, telemetry.SurfaceCount);
        Assert.True(telemetry.WasBudgetClamped);
        Assert.Equal(0, destination[3].R);

        _ = ReflectionRadianceMapBuilder.Build(
            viewport,
            profile,
            CreateFrame(),
            ReadOnlySpan<WaterReflectionSurface>.Empty,
            values,
            values,
            values,
            values,
            1f,
            destination);
        Assert.All(destination, pixel => Assert.Equal(Color.Transparent, pixel));
    }

    [Fact]
    public void Build_ReusesCallerBuffersWithoutSteadyStateAllocation()
    {
        var viewport = new Rectangle(0, 0, 160, 80);
        var profile = CreateProfile(viewport, width: 10, height: 5);
        var values = new float[profile.MaskPixelCount];
        var destination = new Color[profile.MaskPixelCount];
        var surfaces = new[]
        {
            new WaterReflectionSurface(
                new Rectangle(16, 48, 128, 16),
                ReflectionSurfaceKind.Water,
                Color.White,
                0.5f,
                44)
        };
        _ = ReflectionRadianceMapBuilder.Build(
            viewport, profile, CreateFrame(), surfaces, values, values, values, values, 1f, destination);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = ReflectionRadianceMapBuilder.Build(
                viewport, profile, CreateFrame(), surfaces, values, values, values, values, 1f, destination);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ResolveFresnelReflectance_IncreasesTowardGrazingAngles()
    {
        var normalIncidence = ReflectionRadianceMapBuilder.ResolveFresnelReflectance(0.02f, 1f);
        var oblique = ReflectionRadianceMapBuilder.ResolveFresnelReflectance(0.02f, 0.4f);
        var grazing = ReflectionRadianceMapBuilder.ResolveFresnelReflectance(0.02f, 0.05f);

        Assert.InRange(normalIncidence, 0.019f, 0.021f);
        Assert.True(oblique > normalIncidence);
        Assert.True(grazing > oblique);
        Assert.InRange(grazing, 0f, 1f);
    }

    private static PresentationQualityProfile CreateProfile(Rectangle viewport, int width, int height)
    {
        var profile = PresentationQualityProfile.Create(PresentationQualityTier.High, viewport);
        return profile with
        {
            MaskSize = new Point(width, height),
            Budget = profile.Budget with
            {
                MaxMaskPixels = width * height,
                MaxReflectionStripsPerSurface = 4
            },
            EnableReflections = true
        };
    }

    private static LightingFrameParameters CreateFrame()
    {
        return new LightingFrameParameters(
            NormalizedTimeOfDay: 0.5f,
            AmbientLight: 0.08f,
            SkyLightMultiplier: 1f,
            EmissiveLightMultiplier: 1f,
            CaveBlend: 0f,
            CaveResidualLight: 0.08f,
            ShadowStrength: 1f,
            BloomStrength: 0.6f,
            WeatherOcclusion: 0f,
            FrameIndex: 120);
    }
}
