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
    public void Build_TwilightPaletteChangesContinuouslyAcrossDayBoundary()
    {
        var beforeLayers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var afterLayers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var living = default(LivingWorldFrameSnapshot) with { BiomeId = "forest" };

        var before = ParallaxLayerPlanner.Build(
            living,
            normalizedTimeOfDay: 0.249f,
            cameraDepthPixels: 0f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            defaultSurfaceSpriteId: "world/backgrounds/forest_parallax_layer_v3",
            defaultCaveSpriteId: "world/backgrounds/cave_parallax_layer_v3",
            beforeLayers);
        var after = ParallaxLayerPlanner.Build(
            living,
            normalizedTimeOfDay: 0.251f,
            cameraDepthPixels: 0f,
            surfaceParallax: 0.18f,
            caveParallax: 0.08f,
            defaultSurfaceSpriteId: "world/backgrounds/forest_parallax_layer_v3",
            defaultCaveSpriteId: "world/backgrounds/cave_parallax_layer_v3",
            afterLayers);

        Assert.InRange(ColorDistance(before.SkyTop, after.SkyTop), 0f, 8f);
        Assert.InRange(ColorDistance(beforeLayers[0].Tint, afterLayers[0].Tint), 0f, 8f);
    }

    [Fact]
    public void Build_NoonPaletteIsBrighterThanMidnightWithoutChangingDepthStack()
    {
        var noonLayers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var nightLayers = new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];
        var living = default(LivingWorldFrameSnapshot) with { BiomeId = "forest" };

        var noon = ParallaxLayerPlanner.Build(
            living, 0.5f, 0f, 0.18f, 0.08f,
            "world/backgrounds/forest_parallax_layer_v3",
            "world/backgrounds/cave_parallax_layer_v3",
            noonLayers);
        var midnight = ParallaxLayerPlanner.Build(
            living, 0f, 0f, 0.18f, 0.08f,
            "world/backgrounds/forest_parallax_layer_v3",
            "world/backgrounds/cave_parallax_layer_v3",
            nightLayers);

        Assert.Equal(noon.LayerCount, midnight.LayerCount);
        Assert.True(Brightness(noon.SkyTop) > Brightness(midnight.SkyTop) + 70f);
        Assert.True(Brightness(noonLayers[4].Tint) > Brightness(nightLayers[4].Tint) + 8f);
    }

    [Fact]
    public void Build_V3PanoramasCreateFiveFullscreenDepthPlanesWithoutEdgeFill()
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

        Assert.Equal(5, profile.LayerCount);
        Assert.True(profile.AuthoredPanoramaActive);
        Assert.Equal("world/backgrounds/features_v7/forest_mountains", layers[0].SpriteId);
        Assert.Equal("world/backgrounds/features_v7/forest_floating_islands", layers[1].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/forest_far", layers[2].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/forest_mid", layers[3].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/forest_near", layers[4].SpriteId);
        Assert.All(
            layers.AsSpan(0, profile.LayerCount).ToArray(),
            layer => Assert.True(layer.PreserveAuthoredRepeat));
        for (var index = 0; index < profile.LayerCount; index++)
        {
            Assert.Equal(ParallaxProjectionMode.FullscreenDepthPlane, layers[index].ProjectionMode);
            Assert.Equal(ParallaxVerticalFillMode.None, layers[index].VerticalFillMode);
            Assert.Equal(Color.Transparent, layers[index].TopFillColor);
            Assert.Equal(Color.Transparent, layers[index].BottomFillColor);
            Assert.Equal(ParallaxLandmarkStyle.None, layers[index].LandmarkStyle);
        }

        Assert.Equal(ParallaxVerticalFillMode.None, layers[0].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[1].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[2].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[3].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[4].VerticalFillMode);

        Assert.Equal(ParallaxDepthPlane.Far, layers[0].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Mid, layers[1].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Far, layers[2].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Mid, layers[3].DepthPlane);
        Assert.Equal(ParallaxDepthPlane.Near, layers[4].DepthPlane);
        Assert.Equal(1f, layers[0].ScaleMultiplier);
        Assert.Equal(1f, layers[1].ScaleMultiplier);
        Assert.Equal(0.5f, layers[2].ScaleMultiplier);
        Assert.Equal(0.68f, layers[3].ScaleMultiplier);
        Assert.Equal(0.92f, layers[4].ScaleMultiplier);
        Assert.True(layers[0].HorizontalParallax < layers[2].HorizontalParallax);
        Assert.True(layers[2].HorizontalParallax < layers[1].HorizontalParallax);
        Assert.True(layers[1].HorizontalParallax < layers[3].HorizontalParallax);
        Assert.True(layers[3].HorizontalParallax < layers[4].HorizontalParallax);
    }

    [Theory]
    [InlineData("world/backgrounds/forest_parallax_layer_v3")]
    [InlineData("world/backgrounds/forest_parallax_layer_v4")]
    [InlineData("world/backgrounds/forest_parallax_layer_v5")]
    [InlineData("world/backgrounds/forest_panorama")]
    public void Build_CompositePanoramaUsesAuthoredDistanceFeatureAndDepthStack(string spriteId)
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

        Assert.Equal(5, profile.LayerCount);
        Assert.True(profile.AuthoredPanoramaActive);
        Assert.Equal("world/backgrounds/features_v7/forest_mountains", layers[0].SpriteId);
        Assert.Equal("world/backgrounds/features_v7/forest_floating_islands", layers[1].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/forest_far", layers[2].SpriteId);
        Assert.Equal(0, Count(layers, profile.LayerCount, spriteId));
        for (var index = 0; index < profile.LayerCount; index++)
        {
            Assert.Equal(ParallaxProjectionMode.FullscreenDepthPlane, layers[index].ProjectionMode);
            Assert.NotEqual(ParallaxDepthPlane.Unspecified, layers[index].DepthPlane);
            Assert.Equal(ParallaxVerticalFillMode.None, layers[index].VerticalFillMode);
            Assert.Equal(Color.Transparent, layers[index].TopFillColor);
            Assert.Equal(Color.Transparent, layers[index].BottomFillColor);
        }

        Assert.Equal(ParallaxVerticalFillMode.None, layers[0].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[1].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[2].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[3].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[4].VerticalFillMode);
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
                spriteId, null, null, null, null, 0f, 0f, 0f, 0f)
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
        Assert.Equal(3, profile.LayerCount);
        Assert.Equal("world/backgrounds/depth_v6/crystal_far", layers[0].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/crystal_mid", layers[1].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/crystal_near", layers[2].SpriteId);
        for (var index = 0; index < profile.LayerCount; index++)
        {
            Assert.True(layers[index].PreserveAuthoredRepeat);
            Assert.Equal(ParallaxProjectionMode.FullscreenDepthPlane, layers[index].ProjectionMode);
            Assert.Equal(ParallaxVerticalFillMode.None, layers[index].VerticalFillMode);
            Assert.Equal(Color.Transparent, layers[index].TopFillColor);
            Assert.Equal(Color.Transparent, layers[index].BottomFillColor);
        }

        Assert.Equal(ParallaxVerticalFillMode.None, layers[0].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[1].VerticalFillMode);
        Assert.Equal(ParallaxVerticalFillMode.None, layers[2].VerticalFillMode);
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
                spriteId, null, null, null, null, 0f, 0f, 0f, 0f)
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

        Assert.Equal(5, profile.LayerCount);
        Assert.Equal("world/backgrounds/features_v7/forest_mountains", layers[0].SpriteId);
        Assert.Equal("world/backgrounds/depth_v6/forest_far", layers[2].SpriteId);
        Assert.True(layers[2].PreserveAuthoredRepeat);
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

    private static float ColorDistance(Color left, Color right)
    {
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;
        return MathF.Sqrt(red * red + green * green + blue * blue);
    }

    private static float Brightness(Color color)
    {
        return color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
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
