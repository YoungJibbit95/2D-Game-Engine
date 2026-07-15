using Game.Client.Rendering.Effects;
using Game.Core.Runtime;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class ParallaxLayerPlannerTests
{
    [Fact]
    public void Build_CombinesBiomeAndVerticalCaveLayersWithinFixedCapacity()
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "forest",
            SubBiomeId = "crystal_depths",
            CaveProfileId = "crystal",
            CloudCover = 0.35f
        };
        var layers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];

        var profile = ParallaxLayerPlanner.Build(
            living,
            isNight: false,
            cameraDepthPixels: 1_400f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            "world/backgrounds/forest_parallax_layer",
            "world/backgrounds/cave_parallax_layer",
            layers);

        Assert.InRange(profile.LayerCount, 2, ParallaxLayerPlanner.MaximumLayerCount);
        Assert.True(profile.UndergroundBlend > 0.9f);
        Assert.True(profile.DeepBlend > 0f);
        Assert.True(Contains(layers, profile.LayerCount, "world/backgrounds/crystal_depths_parallax_layer"));
        Assert.True(Contains(layers, profile.LayerCount, "world/backgrounds/deep_cave_parallax_layer"));
    }

    [Fact]
    public void ResolveSurfaceSpriteId_DoesNotAllocateForStableBiome()
    {
        _ = ParallaxLayerPlanner.ResolveSurfaceSpriteId("meadow", "fallback");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = ParallaxLayerPlanner.ResolveSurfaceSpriteId("meadow", "fallback");
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static bool Contains(ParallaxLayerDescriptor[] layers, int count, string spriteId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(layers[index].SpriteId, spriteId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
