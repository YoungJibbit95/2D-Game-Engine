using Game.Client.Rendering;
using Game.Core.Runtime;
using Game.Core.Weather;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class PixelAtmosphereRendererTests
{
    [Fact]
    public void ResolveProfile_TransitionsContinuouslyAcrossDayNightBoundary()
    {
        var world = CreateWorld();
        var camera = new Camera2D { Position = new Vector2(160f, 40f * 16f) };
        var living = CreateLivingWorld();

        var before = PixelAtmosphereRenderer.ResolveProfile(
            world,
            camera,
            CreateTime(0.249d),
            living);
        var after = PixelAtmosphereRenderer.ResolveProfile(
            world,
            camera,
            CreateTime(0.251d),
            living);

        Assert.InRange(ColorDistance(before.GradeColor, after.GradeColor), 0, 8);
        Assert.InRange(Math.Abs(before.GradeStrength - after.GradeStrength), 0f, 0.01f);
    }

    [Fact]
    public void ResolveProfile_DepthGradeDoesNotJumpAtUndergroundClassificationThreshold()
    {
        var world = CreateWorld();
        var living = CreateLivingWorld() with { IsUnderground = true };
        var shallowCamera = new Camera2D { Position = new Vector2(160f, 47f * 16f) };
        var deeperCamera = new Camera2D { Position = new Vector2(160f, 49f * 16f) };

        var shallow = PixelAtmosphereRenderer.ResolveProfile(
            world,
            shallowCamera,
            CreateTime(0.5d),
            living);
        var deeper = PixelAtmosphereRenderer.ResolveProfile(
            world,
            deeperCamera,
            CreateTime(0.5d),
            living);

        Assert.InRange(ColorDistance(shallow.GradeColor, deeper.GradeColor), 0, 12);
        Assert.InRange(deeper.GradeStrength - shallow.GradeStrength, 0f, 0.02f);
        Assert.InRange(deeper.FogStrength - shallow.FogStrength, 0f, 0.03f);
    }

    [Fact]
    public void ResolveProfile_NightRemainsBlueAndReadableWithoutOpaqueFog()
    {
        var world = CreateWorld();
        var camera = new Camera2D { Position = new Vector2(160f, 40f * 16f) };
        var profile = PixelAtmosphereRenderer.ResolveProfile(
            world,
            camera,
            CreateTime(0d),
            CreateLivingWorld());

        Assert.True(profile.GradeColor.B > profile.GradeColor.R);
        Assert.InRange(profile.GradeStrength, 0.08f, 0.18f);
        Assert.InRange(profile.FogStrength, 0f, 0.02f);
    }

    private static World CreateWorld()
    {
        return new World(32, 128, WorldMetadata.CreateDefault(711) with
        {
            SpawnTile = new TilePos(10, 40)
        });
    }

    private static LivingWorldFrameSnapshot CreateLivingWorld()
    {
        return default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "forest",
            BiomeDisplayName = "Forest",
            BiomeLayerId = "surface",
            SoundscapeId = "forest_day",
            ColorGradeId = "forest",
            AmbientLight = 0.75f,
            Visibility = 1f,
            SkyLightMultiplier = 1f,
            EmissiveLightMultiplier = 1f,
            Weather = WeatherKind.Clear,
            SurfaceTileY = 40
        };
    }

    private static WorldTimeFrameSnapshot CreateTime(double normalizedTime)
    {
        return new WorldTimeFrameSnapshot(
            Day: 1,
            TimeOfDaySeconds: normalizedTime * 1440d,
            DayLengthSeconds: 1440d,
            NormalizedTimeOfDay: normalizedTime,
            IsNight: normalizedTime < 0.25d || normalizedTime >= 0.75d);
    }

    private static int ColorDistance(Color left, Color right)
    {
        return Math.Abs(left.R - right.R) +
            Math.Abs(left.G - right.G) +
            Math.Abs(left.B - right.B);
    }
}