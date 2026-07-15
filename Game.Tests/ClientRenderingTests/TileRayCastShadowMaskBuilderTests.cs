using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class TileRayCastShadowMaskBuilderTests
{
    [Fact]
    public void Build_OccludesColoredPointLightBehindSolidTiles()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(73) with { SpawnTile = new TilePos(4, 2) });
        for (var y = 0; y < world.HeightTiles; y++)
        {
            world.SetTile(8, y, TileInstance.FromTileId(1));
        }

        var profile = CreateProfile(16, 8);
        var buffers = new Buffers(profile.MaskPixelCount);
        var frame = CreateFrame();
        var lights = new[]
        {
            new ScreenSpaceLight(
                new Vector2(4.5f * 16f, 4.5f * 16f),
                RadiusPixels: 14f * 16f,
                Color.Red,
                Intensity: 1f,
                EmissiveStrength: 1f,
                CastsShadows: true,
                StableId: 12,
                FlickerAmount: 0f)
        };

        var telemetry = Build(world, profile, frame, lights, buffers);

        var visibleSide = buffers.Red[4 * 16 + 6];
        var blockedSide = buffers.Red[4 * 16 + 12];
        Assert.True(visibleSide > blockedSide + 0.05f);
        Assert.True(telemetry.RaysCast > 0);
        Assert.True(telemetry.OccluderSamples > 0);
    }

    [Fact]
    public void Build_RemovingMinedOccluderClearsShadowOnNextPreparedFrame()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(74));
        for (var y = 0; y < world.HeightTiles; y++)
        {
            world.SetTile(8, y, TileInstance.FromTileId(1));
        }

        var profile = CreateProfile(16, 8);
        var buffers = new Buffers(profile.MaskPixelCount);
        var lights = new[]
        {
            new ScreenSpaceLight(
                new Vector2(4.5f * 16f, 4.5f * 16f),
                14f * 16f,
                Color.Red,
                1f,
                1f,
                CastsShadows: true,
                StableId: 13,
                FlickerAmount: 0f)
        };

        _ = Build(world, profile, CreateFrame(), lights, buffers);
        var blocked = buffers.Red[4 * 16 + 12];

        world.RemoveTile(8, 4);
        _ = Build(world, profile, CreateFrame(), lights, buffers);
        var opened = buffers.Red[4 * 16 + 12];

        Assert.True(opened > blocked + 0.05f);
    }

    [Fact]
    public void ResolveDaylightColor_IsWarmAtSunriseAndNeutralAtNoon()
    {
        var sunrise = TileRayCastShadowMaskBuilder.ResolveDaylightColor(0.27f);
        var noon = TileRayCastShadowMaskBuilder.ResolveDaylightColor(0.5f);

        Assert.True(sunrise.R - sunrise.B > noon.R - noon.B);
        Assert.True(noon.G > sunrise.G);
    }

    [Fact]
    public void Build_OpenSkyRemainsBrightWhenPlayerSnapshotIsUnderground()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(75));
        var profile = CreateProfile(16, 8);
        var buffers = new Buffers(profile.MaskPixelCount);

        _ = Build(
            world,
            profile,
            CreateFrame() with { CaveBlend = 0.84f },
            Array.Empty<ScreenSpaceLight>(),
            buffers);

        Assert.All(buffers.Shadow, value => Assert.InRange(value, 0f, 0.3f));
    }

    [Fact]
    public void Build_HandlesExtremeInfiniteWorldBoundsAndKeepsValuesFinite()
    {
        var world = new World(32, 8, WorldMetadata.CreateDefault(12), isHorizontallyInfinite: true);
        var profile = CreateProfile(8, 4);
        var buffers = new Buffers(profile.MaskPixelCount);

        _ = TileRayCastShadowMaskBuilder.Build(
            world,
            new Rectangle(int.MaxValue - 32, 0, 32, 64),
            profile,
            CreateFrame(),
            ReadOnlySpan<ScreenSpaceLight>.Empty,
            buffers.Shadow,
            buffers.Red,
            buffers.Green,
            buffers.Blue,
            buffers.Bloom,
            buffers.Scratch);

        Assert.All(buffers.Shadow, value => Assert.True(float.IsFinite(value)));
        Assert.All(buffers.Red, value => Assert.True(float.IsFinite(value)));
    }

    [Fact]
    public void Build_ReusesWorkBuffersWithoutSteadyStateAllocation()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(19));
        world.SetTile(4, 4, TileInstance.FromTileId(1));
        var profile = CreateProfile(16, 8);
        var buffers = new Buffers(profile.MaskPixelCount);
        var frame = CreateFrame();
        var lights = new[] { ScreenSpaceLight.Torch(new Vector2(48f, 48f), 4) };
        _ = Build(world, profile, frame, lights, buffers);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = Build(world, profile, frame, lights, buffers);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Build_SanitizesNonFiniteFrameAndLightInputs()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(88));
        var profile = CreateProfile(8, 4);
        var buffers = new Buffers(profile.MaskPixelCount);
        var frame = CreateFrame() with
        {
            AmbientLight = float.NaN,
            CaveBlend = float.PositiveInfinity,
            ShadowStrength = float.NaN,
            BloomStrength = float.NaN
        };
        var lights = new[]
        {
            new ScreenSpaceLight(
                new Vector2(float.NaN, 0f),
                float.PositiveInfinity,
                Color.White,
                float.NaN,
                float.NaN,
                CastsShadows: true,
                StableId: 0,
                FlickerAmount: float.NaN)
        };

        _ = Build(world, profile, frame, lights, buffers);

        Assert.All(buffers.Shadow, value => Assert.True(float.IsFinite(value)));
        Assert.All(buffers.Red, value => Assert.True(float.IsFinite(value)));
        Assert.All(buffers.Bloom, value => Assert.True(float.IsFinite(value)));
    }

    private static LightingBuildTelemetry Build(
        World world,
        PresentationQualityProfile profile,
        LightingFrameParameters frame,
        ScreenSpaceLight[] lights,
        Buffers buffers)
    {
        return TileRayCastShadowMaskBuilder.Build(
            world,
            new Rectangle(0, 0, 16 * 16, 8 * 16),
            profile,
            frame,
            lights,
            buffers.Shadow,
            buffers.Red,
            buffers.Green,
            buffers.Blue,
            buffers.Bloom,
            buffers.Scratch);
    }

    private static PresentationQualityProfile CreateProfile(int width, int height)
    {
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, width * 16, height * 16));
        return profile with
        {
            MaskSize = new Point(width, height),
            Budget = profile.Budget with
            {
                MaxMaskPixels = width * height,
                MaxPenumbraRadius = 0,
                MaxBloomRadius = 0
            },
            AmbientOcclusionRadius = 1,
            CastSunShadows = true,
            CastPointLightShadows = true,
            EnableBloom = false
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
