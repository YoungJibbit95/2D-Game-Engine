using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Game.Core.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParallaxDetailPlannerTests
{
    [Fact]
    public void Build_HighQualityNightSurfaceUsesBoundedLayeredDetailPlan()
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            RegionIndex = -27,
            BiomeId = "amber_grove",
            CloudCover = 0.75f,
            Wind = 0.4f
        };
        var scene = new ParallaxSceneProfile(
            0f,
            0f,
            1,
            new Color(12, 19, 42),
            new Color(24, 30, 57),
            new Color(38, 39, 63));
        var commands = new ParallaxDetailCommand[ParallaxDetailPlanner.MaximumCommandCount];

        var count = ParallaxDetailPlanner.Build(
            living,
            scene,
            isNight: true,
            new Rectangle(0, 0, 3840, 2160),
            cameraX: -8_192f,
            animationSeconds: 42.25d,
            quality: 3,
            commands);

        Assert.InRange(count, 1, ParallaxDetailPlanner.MaximumCommandCount);
        Assert.True(Contains(commands, count, ParallaxDetailKind.HazeBand));
        Assert.True(Contains(commands, count, ParallaxDetailKind.CloudWisp));
        Assert.True(Contains(commands, count, ParallaxDetailKind.Star));
        Assert.DoesNotContain(
            commands.AsSpan(0, count).ToArray(),
            command => command.Bounds.Width <= 0 || command.Bounds.Height <= 0);
    }

    [Theory]
    [InlineData("crystal_depths", ParallaxDetailKind.AmbientMote)]
    [InlineData("mushroom_hollows", ParallaxDetailKind.CaveStrata)]
    public void Build_CaveProfilesAddAnimatedDetailWithoutSurfaceStars(
        string subBiomeId,
        ParallaxDetailKind expectedKind)
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            RegionIndex = 18,
            BiomeId = "forest",
            SubBiomeId = subBiomeId,
            IsUnderground = true
        };
        var scene = new ParallaxSceneProfile(
            1f,
            0.6f,
            3,
            new Color(18, 27, 46),
            new Color(27, 43, 61),
            new Color(37, 58, 69));
        var commands = new ParallaxDetailCommand[ParallaxDetailPlanner.MaximumCommandCount];

        var count = ParallaxDetailPlanner.Build(
            living,
            scene,
            isNight: true,
            new Rectangle(0, 0, 2560, 1440),
            cameraX: 16_384f,
            animationSeconds: 12d,
            quality: 3,
            commands);

        Assert.True(Contains(commands, count, expectedKind));
        Assert.False(Contains(commands, count, ParallaxDetailKind.Star));
    }

    [Fact]
    public void Build_AuthoredPanoramaSuppressesGeometricBackdropBands()
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            RegionIndex = 7,
            BiomeId = "forest",
            CloudCover = 0.9f,
            Wind = 0.7f
        };
        var scene = new ParallaxSceneProfile(
            0f,
            0f,
            1,
            new Color(68, 114, 152),
            new Color(91, 139, 166),
            new Color(143, 166, 169),
            AuthoredPanoramaActive: true);
        var commands = new ParallaxDetailCommand[ParallaxDetailPlanner.MaximumCommandCount];

        var count = ParallaxDetailPlanner.Build(
            living,
            scene,
            isNight: true,
            new Rectangle(0, 0, 1920, 1080),
            cameraX: 4_096f,
            animationSeconds: 24d,
            quality: 3,
            commands);

        Assert.False(Contains(commands, count, ParallaxDetailKind.HazeBand));
        Assert.False(Contains(commands, count, ParallaxDetailKind.CloudWisp));
        Assert.True(Contains(commands, count, ParallaxDetailKind.Star));
    }

    [Fact]
    public void Build_IsDeterministicAndAllocationFreeAcrossLongCameraTrace()
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            RegionIndex = -91,
            BiomeId = "forest",
            SubBiomeId = "crystal_depths",
            CloudCover = 0.3f,
            Wind = -0.65f
        };
        var scene = new ParallaxSceneProfile(
            0.5f,
            0.2f,
            5,
            new Color(42, 65, 89),
            new Color(53, 72, 90),
            new Color(62, 70, 82));
        var first = new ParallaxDetailCommand[ParallaxDetailPlanner.MaximumCommandCount];
        var second = new ParallaxDetailCommand[ParallaxDetailPlanner.MaximumCommandCount];
        var viewport = new Rectangle(0, 0, 2560, 1440);
        _ = Build(living, scene, viewport, -1_000_000f, 15d, first);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var firstCount = 0;
        var secondCount = 0;
        for (var iteration = 0; iteration < 5_000; iteration++)
        {
            var cameraX = -1_000_000f + iteration * 31.25f;
            var seconds = 15d + iteration / 120d;
            firstCount = Build(living, scene, viewport, cameraX, seconds, first);
            secondCount = Build(living, scene, viewport, cameraX, seconds, second);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(firstCount, secondCount);
        for (var index = 0; index < firstCount; index++)
        {
            Assert.Equal(first[index], second[index]);
        }
    }

    private static int Build(
        in LivingWorldFrameSnapshot living,
        in ParallaxSceneProfile scene,
        in Rectangle viewport,
        float cameraX,
        double seconds,
        ParallaxDetailCommand[] commands)
    {
        return ParallaxDetailPlanner.Build(
            living,
            scene,
            isNight: true,
            viewport,
            cameraX,
            seconds,
            quality: 3,
            commands);
    }

    private static bool Contains(
        ParallaxDetailCommand[] commands,
        int count,
        ParallaxDetailKind kind)
    {
        for (var index = 0; index < count; index++)
        {
            if (commands[index].Kind == kind)
            {
                return true;
            }
        }

        return false;
    }
}
