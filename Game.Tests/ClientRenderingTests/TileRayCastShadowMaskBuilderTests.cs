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
    public void ResolveSkyIllumination_PreservesReadableTwilightAndNightFloor()
    {
        var midnight = TileRayCastShadowMaskBuilder.ResolveSkyIllumination(0f);
        var twilight = TileRayCastShadowMaskBuilder.ResolveSkyIllumination(0.213f);
        var noon = TileRayCastShadowMaskBuilder.ResolveSkyIllumination(0.5f);

        Assert.InRange(midnight, 0.11f, 0.14f);
        Assert.InRange(twilight, 0.35f, 0.55f);
        Assert.InRange(noon, 0.99f, 1f);
    }

    [Fact]
    public void Build_TwilightOpenSkyStaysReadableWhileRoofedCellsRemainShadowed()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(76));
        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                world.SetTile(x, y, new TileInstance { TileId = KnownTileIds.Air, Light = 255 });
            }
        }

        for (var x = 8; x < world.WidthTiles; x++)
        {
            world.SetTile(x, 2, TileInstance.FromTileId(1));
        }

        var profile = CreateProfile(16, 8);
        var buffers = new Buffers(profile.MaskPixelCount);
        _ = Build(
            world,
            profile,
            CreateFrame() with { NormalizedTimeOfDay = 0.213f },
            Array.Empty<ScreenSpaceLight>(),
            buffers);

        var openSky = buffers.Shadow[4 * 16 + 4];
        var underRoof = buffers.Shadow[4 * 16 + 12];
        Assert.InRange(openSky, 0f, 0.62f);
        Assert.True(underRoof > openSky + 0.12f);
    }

    [Fact]
    public void Build_OpenSkyRemainsBrightWhenPlayerSnapshotIsUnderground()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(75));
        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                world.SetTile(x, y, new TileInstance { TileId = KnownTileIds.Air, Light = 255 });
            }
        }

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
    public void Build_ViewportThatStartsUndergroundDoesNotInventOpenSky()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(79));
        var profile = CreateProfile(16, 8);
        var buffers = new Buffers(profile.MaskPixelCount);

        _ = Build(
            world,
            profile,
            CreateFrame() with { CaveBlend = 1f, CaveResidualLight = 0.12f },
            Array.Empty<ScreenSpaceLight>(),
            buffers);

        Assert.All(buffers.Shadow, value => Assert.InRange(value, 0.55f, 0.9f));
    }

    [Fact]
    public void Build_DaytimeAtmosphereDoesNotBleachBuriedTerrain()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(80));
        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                world.SetTile(x, y, y < 2
                    ? new TileInstance { TileId = KnownTileIds.Air, Light = 255 }
                    : new TileInstance { TileId = 1, Light = 220, Flags = TileFlags.Solid });
            }
        }

        var profile = CreateProfile(16, 8);
        var buffers = new Buffers(profile.MaskPixelCount);
        _ = Build(
            world,
            profile,
            CreateFrame() with { AmbientLight = 1f },
            Array.Empty<ScreenSpaceLight>(),
            buffers);

        Assert.InRange(buffers.Shadow[16 + 4], 0f, 0.15f);
        Assert.InRange(buffers.Shadow[6 * 16 + 4], 0.55f, 0.9f);
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
    public void Build_SoftShadowProfileUsesBoundedMultiRayVisibility()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(101));
        for (var y = 2; y < 7; y++)
        {
            world.SetTile(8, y, TileInstance.FromTileId(1));
        }

        var profile = CreateProfile(16, 8);
        profile = profile with
        {
            Budget = profile.Budget with { MaxPenumbraRadius = 4 }
        };
        var buffers = new Buffers(profile.MaskPixelCount);
        var lights = new[]
        {
            ScreenSpaceLight.Torch(new Vector2(5.5f * 16f, 3.5f * 16f), 77, 14f * 16f)
        };

        var telemetry = Build(world, profile, CreateFrame(), lights, buffers);

        Assert.Equal(3, telemetry.PointShadowSamples);
        Assert.InRange(
            telemetry.RaysCast,
            1,
            profile.MaskPixelCount + telemetry.MaximumPointShadowRays);
        Assert.True(telemetry.OccluderSamples > 0);
    }

    [Fact]
    public void ResolvePointRayVisibility_ProducesFractionalPenumbraAtOccluderEdge()
    {
        var tileSamples = new float[9 * 7];
        tileSamples[3 * 9 + 4] = 2f;
        var plan = new LightingRaySamplePlan(
            PointShadowSamples: 3,
            EndpointSpreadMaskPixels: 2,
            MaxStepsPerRay: 16,
            MaximumPointShadowRays: 3,
            WasBudgetClamped: false);
        var rays = 0L;
        var samples = 0L;

        var visibility = TileRayCastShadowMaskBuilder.ResolvePointRayVisibility(
            tileSamples,
            width: 9,
            height: 7,
            originX: 7,
            originY: 3,
            lightX: 1,
            lightY: 3,
            plan,
            ref rays,
            ref samples);

        Assert.InRange(visibility, 0.65f, 0.68f);
        Assert.Equal(3, rays);
        Assert.True(samples > 0);
    }

    [Fact]
    public void Build_SoftShadowMultiRayPathHasZeroSteadyStateAllocation()
    {
        var world = new World(16, 8, WorldMetadata.CreateDefault(102));
        world.SetTile(8, 4, TileInstance.FromTileId(1));
        var profile = CreateProfile(16, 8);
        profile = profile with
        {
            Budget = profile.Budget with { MaxPenumbraRadius = 4 }
        };
        var buffers = new Buffers(profile.MaskPixelCount);
        var lights = new[]
        {
            ScreenSpaceLight.Torch(new Vector2(5.5f * 16f, 3.5f * 16f), 78, 14f * 16f)
        };
        _ = Build(world, profile, CreateFrame(), lights, buffers);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 50; iteration++)
        {
            _ = Build(world, profile, CreateFrame(), lights, buffers);
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
