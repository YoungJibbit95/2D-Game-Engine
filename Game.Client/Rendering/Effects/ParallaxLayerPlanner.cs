using Game.Core.Runtime;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public readonly record struct ParallaxLayerDescriptor(
    string SpriteId,
    float HorizontalParallax,
    float VerticalParallax,
    float Opacity,
    int VerticalOffset,
    float ScaleMultiplier,
    Color Tint);

public readonly record struct ParallaxSceneProfile(
    float UndergroundBlend,
    float DeepBlend,
    int LayerCount);

public static class ParallaxLayerPlanner
{
    public const int MaximumLayerCount = 8;

    public static ParallaxSceneProfile Build(
        in LivingWorldFrameSnapshot livingWorld,
        bool isNight,
        float cameraDepthPixels,
        float surfaceParallax,
        float caveParallax,
        string defaultSurfaceSpriteId,
        string defaultCaveSpriteId,
        Span<ParallaxLayerDescriptor> destination)
    {
        if (destination.Length < MaximumLayerCount)
        {
            throw new ArgumentException(
                $"Parallax destination must contain at least {MaximumLayerCount} entries.",
                nameof(destination));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(defaultSurfaceSpriteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCaveSpriteId);
        var underground = Math.Clamp((cameraDepthPixels - 220f) / 340f, 0f, 1f);
        var deep = Math.Clamp((cameraDepthPixels - 980f) / 620f, 0f, 1f);
        var presentationSprite = livingWorld.Presentation.BackgroundSpriteId;
        var hasPresentationSprite = !string.IsNullOrWhiteSpace(presentationSprite);
        var surface = !livingWorld.IsUnderground && hasPresentationSprite
            ? presentationSprite!
            : ResolveSurfaceSpriteId(livingWorld.BiomeId, defaultSurfaceSpriteId);
        var cave = livingWorld.IsUnderground && hasPresentationSprite
            ? presentationSprite!
            : ResolveCaveSpriteId(livingWorld.SubBiomeId, livingWorld.CaveProfileId, defaultCaveSpriteId);
        var weatherOpacity = Math.Clamp(1f - livingWorld.CloudCover * 0.22f, 0.72f, 1f);
        var storm = livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm
            ? livingWorld.WeatherIntensity
            : 0f;
        var surfaceTint = Color.Lerp(Color.White, new Color(155, 174, 188), storm * 0.42f);
        var caveTint = Color.Lerp(Color.White, ResolveCaveTint(livingWorld.SubBiomeId), 0.28f + deep * 0.18f);
        var count = 0;

        if (underground < 0.995f)
        {
            var distant = isNight && !hasPresentationSprite && string.Equals(livingWorld.BiomeId, "forest", StringComparison.OrdinalIgnoreCase)
                ? "world/backgrounds/night_forest_parallax_layer"
                : surface;
            destination[count++] = new ParallaxLayerDescriptor(
                distant,
                surfaceParallax * 0.32f,
                0.018f,
                0.28f * (1f - underground) * weatherOpacity,
                -72,
                0.92f,
                Color.Lerp(surfaceTint, new Color(164, 184, 205), 0.25f));
            destination[count++] = new ParallaxLayerDescriptor(
                distant,
                surfaceParallax * 0.58f,
                0.035f,
                0.38f * (1f - underground) * weatherOpacity,
                -50,
                1f,
                surfaceTint);

            if (Contains(livingWorld.SubBiomeId, "grove"))
            {
                destination[count++] = new ParallaxLayerDescriptor(
                    "world/backgrounds/magical_grove_parallax_layer",
                    surfaceParallax * 0.86f,
                    0.06f,
                    0.32f * (1f - underground),
                    -34,
                    1f,
                    new Color(203, 221, 231));
            }

            destination[count++] = new ParallaxLayerDescriptor(
                surface,
                surfaceParallax,
                0.082f,
                (1f - underground * 0.72f) * weatherOpacity,
                -24,
                1.04f,
                surfaceTint);
        }

        if (underground > 0.005f && count < MaximumLayerCount)
        {
            destination[count++] = new ParallaxLayerDescriptor(
                defaultCaveSpriteId,
                caveParallax * 0.38f,
                0.018f,
                underground * (1f - deep * 0.5f) * 0.42f,
                32,
                0.94f,
                new Color(135, 139, 151));
            destination[count++] = new ParallaxLayerDescriptor(
                cave,
                caveParallax,
                0.04f,
                underground * (1f - deep * 0.28f),
                22,
                1f,
                caveTint);
        }

        if (deep > 0.005f && count < MaximumLayerCount)
        {
            destination[count++] = new ParallaxLayerDescriptor(
                "world/backgrounds/deep_cave_parallax_layer",
                caveParallax * 0.45f,
                0.015f,
                deep * 0.72f,
                6,
                0.96f,
                new Color(134, 122, 151));
            if (!string.Equals(cave, defaultCaveSpriteId, StringComparison.OrdinalIgnoreCase) &&
                count < MaximumLayerCount)
            {
                destination[count++] = new ParallaxLayerDescriptor(
                    cave,
                    caveParallax * 0.7f,
                    0.026f,
                    deep * 0.46f,
                    12,
                    1.08f,
                    ResolveCaveTint(livingWorld.SubBiomeId));
            }
        }

        return new ParallaxSceneProfile(underground, deep, count);
    }

    internal static string ResolveSurfaceSpriteId(string? biomeId, string fallback)
    {
        if (string.Equals(biomeId, "meadow", StringComparison.OrdinalIgnoreCase))
        {
            return "world/backgrounds/meadow_parallax_layer";
        }

        return fallback;
    }

    internal static string ResolveCaveSpriteId(string? subBiomeId, string? caveProfileId, string fallback)
    {
        var identity = subBiomeId ?? caveProfileId;
        if (Contains(identity, "mushroom"))
        {
            return "world/backgrounds/mushroom_cave_parallax_layer";
        }

        if (Contains(identity, "crystal"))
        {
            return "world/backgrounds/crystal_depths_parallax_layer";
        }

        return fallback;
    }

    private static Color ResolveCaveTint(string? identity)
    {
        if (Contains(identity, "mushroom"))
        {
            return new Color(205, 153, 222);
        }

        return Contains(identity, "crystal")
            ? new Color(139, 207, 235)
            : new Color(174, 164, 186);
    }

    private static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    }
}
