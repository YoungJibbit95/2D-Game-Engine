using Game.Client.Rendering.Effects;
using Game.Tests.PerformanceTests;
using Game.Core.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

[Collection(LongSessionPerformanceCollection.Name)]
public sealed class ParallaxLayerPlannerTests
{
    [Fact]
    public void Build_V3PanoramasCreateIndependentFarMidAndNearPlanes()
    {
        var layers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var profile = ParallaxLayerPlanner.Build(
            default(LivingWorldFrameSnapshot) with { BiomeId = "forest" },
            isNight: false,
            cameraDepthPixels: 0f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            defaultSurfaceSpriteId: "world/backgrounds/forest_parallax_layer_v3",
            defaultCaveSpriteId: "world/backgrounds/cave_parallax_layer_v3",
            layers);

        Assert.True(Contains(layers, profile.LayerCount, "world/backgrounds/depth_v6/forest_far"));
        Assert.Equal(3, profile.LayerCount);
        Assert.True(profile.AuthoredPanoramaActive);
        Assert.Null(layers[0].AlternateSpriteId);
        Assert.Equal(ParallaxLandmarkStyle.None, layers[0].LandmarkStyle);
        Assert.True(layers[0].PreserveAuthoredRepeat);
        Assert.Equal(ParallaxVerticalFillMode.ExtendOuterEdges, layers[0].VerticalFillMode);
        Assert.Equal(ParallaxProjectionMode.DistantHorizonBand, layers[0].ProjectionMode);
        Assert.NotEqual(Color.White, layers[0].Tint);
        Assert.Equal(Color.Transparent, layers[0].TopFillColor);
        Assert.Equal(ParallaxDepthPlane.Far, layers[0].DepthPlane);
        Assert.False(layers[0].FeatherTop);
        Assert.Equal(ParallaxDepthPlane.Mid, layers[1].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Near, layers[2].DepthPlane);
        Assert.InRange(layers[0].HorizontalParallax, 0.0143f, 0.0145f);
        Assert.Equal(0.0025f, layers[0].VerticalParallax);
        Assert.True(layers[0].HorizontalParallax < layers[1].HorizontalParallax);
        Assert.True(layers[1].HorizontalParallax < layers[2].HorizontalParallax);
        Assert.True(layers[0].ScaleMultiplier < layers[1].ScaleMultiplier);
        Assert.True(layers[1].ScaleMultiplier < layers[2].ScaleMultiplier);
        Assert.Equal("world/backgrounds/depth_v6/forest_mid", layers[1].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/forest_near", layers[2].SpriteId);
        Assert.Equal(0f, layers[0].RepeatOverlap);
        Assert.Equal(0, layers[0].VerticalJitter);
        Assert.DoesNotContain(
            layers.AsSpan(0, profile.LayerCount).ToArray(),
            layer => string.Equals(
                layer.AlternateSpriteId,
                "world/backgrounds/wave04/forest_parallax_layer",
                StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("world/backgrounds/forest_parallax_layer_v3")]
    [InlineData("world/backgrounds/forest_parallax_layer_v4")]
    [InlineData("world/backgrounds/forest_parallax_layer_v5")]
    [InlineData("world/backgrounds/forest_panorama")]
    public void Build_CompositePanoramaUsesIndependentAuthoredDepthStack(string spriteId)
    {
        var layers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];

        var profile = ParallaxLayerPlanner.Build(
            default(LivingWorldFrameSnapshot) with
            {
                BiomeId = "forest",
                Presentation = new LivingWorldPresentationFrameSnapshot(spriteId, null, null, null, null, 0f, 0f, 0f, 0f)
            },
            isNight: false,
            cameraDepthPixels: 0f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            defaultSurfaceSpriteId: spriteId,
            defaultCaveSpriteId: "world/backgrounds/cave_parallax_layer_v3",
            layers);

        Assert.Equal(3, profile.LayerCount);
        Assert.True(profile.AuthoredPanoramaActive);
        Assert.Equal("world/backgrounds/depth_v6/forest_far", layers[0].SpriteId);
        Assert.Equal(0, Count(layers, profile.LayerCount, spriteId));
        for (var index = 0; index < profile.LayerCount; index++)
        {
            Assert.True(layers[index].PreserveAuthoredRepeat);
            Assert.Equal(ParallaxVerticalFillMode.ExtendOuterEdges, layers[index].VerticalFillMode);
            Assert.Equal(ParallaxProjectionMode.DistantHorizonBand, layers[index].ProjectionMode);
            Assert.NotEqual(ParallaxDepthPlane.Unspecified, layers[index].DepthPlane);
        }

        Assert.Equal(ParallaxDepthPlane.Far, layers[0].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Mid, layers[1].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Near, layers[2].DepthPlane);
        Assert.NotEqual(Color.White, layers[0].Tint);
    }

    [Fact]
    public void Build_CompositeCavePanoramaCreatesIndependentDepthStack()
    {
        const string spriteId = "world/backgrounds/crystal_depths_parallax_layer_v5";
        var layers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var living = default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "crystal_depths",
            SubBiomeId = "crystal_depths",
            IsUnderground = true,
            Presentation = new LivingWorldPresentationFrameSnapshot(
                spriteId,
                null,
                null,
                null,
                null,
                0f,
                0f,
                0f,
                0f)
        };

        var profile = ParallaxLayerPlanner.Build(
            living,
            isNight: false,
            cameraDepthPixels: 700f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            defaultSurfaceSpriteId: "world/backgrounds/forest_parallax_layer_v5",
            defaultCaveSpriteId: "world/backgrounds/cave_parallax_layer_v3",
            layers);

        Assert.True(profile.AuthoredPanoramaActive);
        Assert.Equal(0, Count(layers, profile.LayerCount, spriteId));
        var layer = Find(layers, profile.LayerCount, "world/backgrounds/depth_v6/crystal_far");
        Assert.True(layer.PreserveAuthoredRepeat);
        Assert.Equal(ParallaxVerticalFillMode.ExtendOuterEdges, layer.VerticalFillMode);
        Assert.Null(layer.AlternateSpriteId);
        Assert.Equal(0f, layer.RepeatOverlap);
        Assert.Equal(0, layer.VerticalJitter);
        Assert.Equal(ParallaxLandmarkStyle.None, layer.LandmarkStyle);
        Assert.NotEqual(Color.White, layer.Tint);
        Assert.Equal(Color.Transparent, layer.TopFillColor);
        Assert.Equal(3, profile.LayerCount);
        Assert.Equal("world/backgrounds/depth_v6/crystal_mid", layers[1].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/crystal_near", layers[2].SpriteId);
        Assert.Equal(ParallaxDepthPlane.Far, layers[0].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Mid, layers[1].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Near, layers[2].DepthPlane);
    }

    [Fact]
    public void Build_SurfacePanoramaRemainsActiveNearTerrainWhenTileClassificationIsUnderground()
    {
        const string spriteId = "world/backgrounds/forest_parallax_layer_v5";
        var layers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var living = default(LivingWorldFrameSnapshot) with
        {
            BiomeId = "forest",
            IsUnderground = true,
            Presentation = new LivingWorldPresentationFrameSnapshot(
                spriteId,
                null,
                null,
                null,
                null,
                0f,
                0f,
                0f,
                0f)
        };

        var profile = ParallaxLayerPlanner.Build(
            living,
            isNight: false,
            cameraDepthPixels: 96f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            defaultSurfaceSpriteId: "world/backgrounds/forest_parallax_layer_v3",
            defaultCaveSpriteId: "world/backgrounds/cave_parallax_layer_v3",
            layers);

        Assert.Equal(3, profile.LayerCount);
        Assert.Equal("world/backgrounds/depth_v6/forest_far", layers[0].SpriteId);
        Assert.True(layers[0].PreserveAuthoredRepeat);
    }

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

    [Fact]
    public void Build_UsesBiomeSpecificVariantsLandmarksAndSkyPalette()
    {
        var amber = default(LivingWorldFrameSnapshot) with
        {
            RegionIndex = -17,
            BiomeId = "amber_grove",
            SubBiomeId = "resin_hollows",
            Presentation = new LivingWorldPresentationFrameSnapshot(
                "world/backgrounds/wave05/amber_grove",
                null,
                null,
                null,
                null,
                0f,
                0f,
                0f,
                0f)
        };
        var marsh = amber with
        {
            RegionIndex = 18,
            BiomeId = "twilight_marsh",
            SubBiomeId = "lantern_bog",
            Presentation = amber.Presentation with
            {
                BackgroundSpriteId = "world/backgrounds/wave05/twilight_marsh"
            }
        };
        var amberLayers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var marshLayers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];

        var amberProfile = Build(amber, amberLayers);
        var marshProfile = Build(marsh, marshLayers);

        Assert.NotEqual(amberProfile.SkyMiddle, marshProfile.SkyMiddle);
        Assert.True(ContainsAlternate(amberLayers, amberProfile.LayerCount, "world/backgrounds/magical_grove_parallax_layer"));
        Assert.True(ContainsLandmark(amberLayers, amberProfile.LayerCount, ParallaxLandmarkStyle.AmberWorkshop));
        Assert.True(ContainsLandmark(marshLayers, marshProfile.LayerCount, ParallaxLandmarkStyle.Mangrove));
        Assert.NotEqual(amberLayers[0].VariationSeed, marshLayers[0].VariationSeed);
    }

    [Fact]
    public void Build_IsAllocationFreeForStableLivingWorldSnapshot()
    {
        var living = default(LivingWorldFrameSnapshot) with
        {
            RegionIndex = -8,
            BiomeId = "forest",
            SubBiomeId = "old_growth"
        };
        var layers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        _ = Build(living, layers);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 5_000; iteration++)
        {
            _ = Build(living, layers);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static ParallaxSceneProfile Build(
        in LivingWorldFrameSnapshot living,
        ParallaxLayerDescriptor[] layers)
    {
        return ParallaxLayerPlanner.Build(
            living,
            isNight: false,
            cameraDepthPixels: 0f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            "world/backgrounds/forest_parallax_layer",
            "world/backgrounds/cave_parallax_layer",
            layers);
    }

    private static bool ContainsAlternate(ParallaxLayerDescriptor[] layers, int count, string spriteId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(layers[index].AlternateSpriteId, spriteId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLandmark(
        ParallaxLayerDescriptor[] layers,
        int count,
        ParallaxLandmarkStyle style)
    {
        for (var index = 0; index < count; index++)
        {
            if (layers[index].LandmarkStyle == style)
            {
                return true;
            }
        }

        return false;
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

    private static int Count(ParallaxLayerDescriptor[] layers, int count, string spriteId)
    {
        var matches = 0;
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(layers[index].SpriteId, spriteId, StringComparison.OrdinalIgnoreCase))
            {
                matches++;
            }
        }

        return matches;
    }

    private static ParallaxLayerDescriptor Find(
        ParallaxLayerDescriptor[] layers,
        int count,
        string spriteId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(layers[index].SpriteId, spriteId, StringComparison.OrdinalIgnoreCase))
            {
                return layers[index];
            }
        }

        throw new InvalidOperationException($"Layer '{spriteId}' was not found.");
    }
}
