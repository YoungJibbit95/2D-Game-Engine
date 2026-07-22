using Game.Core.Runtime;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public enum ParallaxLandmarkStyle : byte
{
    None,
    Canopy,
    Ruin,
    CaveSpire,
    CrystalCluster,
    MushroomColony,
    Mangrove,
    AmberWorkshop
}

public enum ParallaxVerticalFillMode : byte
{
    None,
    ExtendBottomEdge,
    ExtendOuterEdges
}

public enum ParallaxProjectionMode : byte
{
    ViewportBackdrop,
    DistantHorizonBand,
    FullscreenDepthPlane
}

public enum ParallaxDepthPlane : byte
{
    Unspecified,
    Far,
    Mid,
    Near
}

public readonly record struct ParallaxLayerDescriptor(
    string SpriteId,
    string? AlternateSpriteId,
    string? LandmarkSpriteId,
    int LandmarkFrameIndex,
    ParallaxLandmarkStyle LandmarkStyle,
    float HorizontalParallax,
    float VerticalParallax,
    float Opacity,
    int VerticalOffset,
    float ScaleMultiplier,
    float RepeatOverlap,
    int VerticalJitter,
    int LandmarkPeriod,
    uint VariationSeed,
    bool PreserveAuthoredRepeat,
    ParallaxVerticalFillMode VerticalFillMode,
    Color Tint,
    Color LandmarkTint)
{
    public ParallaxProjectionMode ProjectionMode { get; init; } = ParallaxProjectionMode.ViewportBackdrop;

    public ParallaxDepthPlane DepthPlane { get; init; } = ParallaxDepthPlane.Unspecified;

    public Color TopFillColor { get; init; } = Color.Transparent;

    public Color BottomFillColor { get; init; } = Color.Transparent;

    public bool FeatherTop { get; init; }
}

public readonly record struct ParallaxSceneProfile(
    float UndergroundBlend,
    float DeepBlend,
    int LayerCount,
    Color SkyTop,
    Color SkyMiddle,
    Color SkyBottom,
    bool AuthoredPanoramaActive = false);

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
        return Build(
            livingWorld,
            isNight ? 0f : 0.5f,
            cameraDepthPixels,
            surfaceParallax,
            caveParallax,
            defaultSurfaceSpriteId,
            defaultCaveSpriteId,
            destination);
    }

    public static ParallaxSceneProfile Build(
        in LivingWorldFrameSnapshot livingWorld,
        float normalizedTimeOfDay,
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
        var presentationTargetsCave = hasPresentationSprite && IsCavePanorama(presentationSprite);
        var hasSurfacePresentation = hasPresentationSprite && !presentationTargetsCave;
        var surface = hasSurfacePresentation
            ? presentationSprite!
            : ResolveSurfaceSpriteId(livingWorld.BiomeId, defaultSurfaceSpriteId);
        var cave = presentationTargetsCave
            ? presentationSprite!
            : ResolveCaveSpriteId(livingWorld.SubBiomeId, livingWorld.CaveProfileId, defaultCaveSpriteId);
        var surfaceIsComposite = IsCompositePanorama(surface);
        var caveIsComposite = IsCompositePanorama(cave);
        var defaultCaveIsComposite = IsCompositePanorama(defaultCaveSpriteId);
        var nightBlend = SolarIlluminationCurve.ResolveNightBlend(normalizedTimeOfDay);
        var useNightVariant = nightBlend >= 0.52f;
        var surfaceAlternate = ResolveSurfaceAlternateSpriteId(livingWorld.BiomeId, useNightVariant, surface);
        var caveAlternate = ResolveCaveAlternateSpriteId(livingWorld.SubBiomeId, livingWorld.CaveProfileId, cave);
        var landmark = ResolveLandmark(livingWorld.BiomeId, livingWorld.SubBiomeId, livingWorld.CaveProfileId);
        var baseSeed = BuildVariationSeed(livingWorld);
        var weatherOpacity = Math.Clamp(1f - livingWorld.CloudCover * 0.22f, 0.72f, 1f);
        var storm = livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm
            ? livingWorld.WeatherIntensity
            : 0f;
        var surfaceTint = Color.Lerp(
            ResolveSurfaceTint(livingWorld.BiomeId),
            new Color(112, 132, 168),
            nightBlend * 0.72f);
        surfaceTint = Color.Lerp(surfaceTint, new Color(145, 160, 176), storm * 0.42f);
        var caveTint = Color.Lerp(Color.White, ResolveCaveTint(livingWorld.SubBiomeId, livingWorld.CaveProfileId), 0.34f + deep * 0.2f);
        var count = 0;

        if (underground < 0.995f && surfaceIsComposite)
        {
            AddAuthoredSurfaceDepthStack(
                destination,
                ref count,
                livingWorld.BiomeId,
                ResolveAuthoredFarSpriteId(surface, livingWorld.BiomeId, livingWorld.SubBiomeId, false),
                ResolveAuthoredMidSpriteId(surface, livingWorld.BiomeId, livingWorld.SubBiomeId, false),
                ResolveAuthoredNearSpriteId(surface, livingWorld.BiomeId, livingWorld.SubBiomeId, false),
                surfaceParallax,
                1f - underground,
                Mix(baseSeed, 0xC8013EA4u),
                surfaceTint,
                ResolveAuthoredTopFillColor(surface));
        }
        else if (underground < 0.995f)
        {
            var distant = useNightVariant && !hasSurfacePresentation && Is(livingWorld.BiomeId, "forest")
                ? "world/backgrounds/night_forest_parallax_layer"
                : surfaceAlternate;
            AddLayer(
                destination,
                ref count,
                distant,
                surface,
                landmark.SpriteId,
                landmark.FrameIndex,
                landmark.Style,
                surfaceParallax * 0.3f,
                0.014f,
                0.3f * (1f - underground) * weatherOpacity,
                -76,
                0.88f,
                0.1f,
                7,
                landmark.Period + 2,
                Mix(baseSeed, 0xA341316Cu),
                false,
                Color.Lerp(surfaceTint, ResolveHorizonTint(livingWorld.BiomeId), 0.45f),
                Color.Lerp(landmark.Tint, surfaceTint, 0.35f));
            AddLayer(
                destination,
                ref count,
                surface,
                surfaceAlternate,
                landmark.SpriteId,
                landmark.FrameIndex,
                landmark.Style,
                surfaceParallax * 0.55f,
                0.032f,
                0.43f * (1f - underground) * weatherOpacity,
                -52,
                0.97f,
                0.085f,
                9,
                landmark.Period,
                Mix(baseSeed, 0xC8013EA4u),
                false,
                Color.Lerp(surfaceTint, ResolveHorizonTint(livingWorld.BiomeId), 0.18f),
                landmark.Tint);

            if (Contains(livingWorld.SubBiomeId, "grove") || Is(livingWorld.BiomeId, "twilight_marsh"))
            {
                AddLayer(
                    destination,
                    ref count,
                    Is(livingWorld.BiomeId, "twilight_marsh")
                        ? "world/backgrounds/wave05/twilight_marsh"
                        : "world/backgrounds/magical_grove_parallax_layer",
                    surfaceAlternate,
                    landmark.SpriteId,
                    landmark.FrameIndex,
                    landmark.Style,
                    surfaceParallax * 0.8f,
                    0.052f,
                    0.34f * (1f - underground),
                    -35,
                    1f,
                    0.075f,
                    6,
                    landmark.Period + 1,
                    Mix(baseSeed, 0xAD90777Du),
                    false,
                    Color.Lerp(surfaceTint, new Color(190, 219, 218), 0.25f),
                    landmark.Tint);
            }

            AddLayer(
                destination,
                ref count,
                surface,
                surfaceAlternate,
                landmark.SpriteId,
                landmark.FrameIndex,
                landmark.Style,
                surfaceParallax,
                0.08f,
                (1f - underground * 0.72f) * weatherOpacity,
                -23,
                1.05f,
                0.06f,
                5,
                Math.Max(3, landmark.Period - 1),
                Mix(baseSeed, 0x7E95761Eu),
                false,
                surfaceTint,
                Color.Lerp(landmark.Tint, Color.White, 0.08f));
        }

        if (underground > 0.005f && count < MaximumLayerCount)
        {
            if (!caveIsComposite && (!defaultCaveIsComposite || !Is(cave, defaultCaveSpriteId)))
            {
                AddLayer(
                    destination,
                    ref count,
                    defaultCaveSpriteId,
                    defaultCaveIsComposite ? null : "world/backgrounds/deep_cave_parallax_layer",
                    null,
                    0,
                    defaultCaveIsComposite ? ParallaxLandmarkStyle.None : ParallaxLandmarkStyle.CaveSpire,
                    caveParallax * 0.36f,
                    0.016f,
                    underground * (1f - deep * 0.5f) * 0.44f,
                    34,
                    0.92f,
                    defaultCaveIsComposite ? 0f : 0.11f,
                    defaultCaveIsComposite ? 0 : 8,
                    defaultCaveIsComposite ? 0 : 6,
                    Mix(baseSeed, 0x9E3779B9u),
                    defaultCaveIsComposite,
                    new Color(126, 132, 148),
                    Color.Lerp(caveTint, new Color(48, 49, 61), 0.54f));
            }

            if (caveIsComposite)
            {
                AddAuthoredDepthStack(
                    destination,
                    ref count,
                    ResolveAuthoredFarSpriteId(cave, livingWorld.BiomeId, livingWorld.SubBiomeId, true),
                    ResolveAuthoredMidSpriteId(cave, livingWorld.BiomeId, livingWorld.SubBiomeId, true),
                    ResolveAuthoredNearSpriteId(cave, livingWorld.BiomeId, livingWorld.SubBiomeId, true),
                    caveParallax,
                    underground * (1f - deep * 0.28f),
                    Mix(baseSeed, 0xD1B54A35u),
                    caveTint,
                    ResolveAuthoredTopFillColor(cave));
            }
            else
            {
                AddLayer(
                    destination,
                    ref count,
                    cave,
                    caveAlternate,
                    landmark.SpriteId,
                    landmark.FrameIndex,
                    landmark.UndergroundStyle,
                    caveParallax,
                    0.038f,
                    underground * (1f - deep * 0.28f),
                    22,
                    1f,
                    0.075f,
                    6,
                    Math.Max(3, landmark.Period),
                    Mix(baseSeed, 0xD1B54A35u),
                    false,
                    caveTint,
                    Color.Lerp(landmark.Tint, caveTint, 0.25f));
            }
        }

        if (deep > 0.005f && count < MaximumLayerCount)
        {
            var deepStyle = ResolveDeepLandmarkStyle(livingWorld.SubBiomeId, livingWorld.CaveProfileId);
            AddLayer(
                destination,
                ref count,
                "world/backgrounds/deep_cave_parallax_layer",
                caveAlternate,
                null,
                0,
                deepStyle,
                caveParallax * 0.43f,
                0.014f,
                deep * 0.74f,
                6,
                0.95f,
                0.12f,
                10,
                5,
                Mix(baseSeed, 0x94D049BBu),
                false,
                Color.Lerp(new Color(125, 113, 146), caveTint, 0.35f),
                Color.Lerp(caveTint, new Color(35, 32, 48), 0.48f));
            if (!caveIsComposite && !Is(cave, defaultCaveSpriteId) && count < MaximumLayerCount)
            {
                AddLayer(
                    destination,
                    ref count,
                    cave,
                    "world/backgrounds/deep_cave_parallax_layer",
                    landmark.SpriteId,
                    landmark.FrameIndex,
                    landmark.UndergroundStyle,
                    caveParallax * 0.7f,
                    0.025f,
                    deep * 0.48f,
                    12,
                    1.08f,
                    0.07f,
                    7,
                    Math.Max(3, landmark.Period - 1),
                    Mix(baseSeed, 0x369DEA0Fu),
                    false,
                    ResolveCaveTint(livingWorld.SubBiomeId, livingWorld.CaveProfileId),
                    landmark.Tint);
            }
        }

        var palette = ResolveSkyPalette(livingWorld, nightBlend, underground, deep);
        return new ParallaxSceneProfile(
            underground,
            deep,
            count,
            palette.Top,
            palette.Middle,
            palette.Bottom,
            (surfaceIsComposite && underground < 0.995f) ||
            ((defaultCaveIsComposite || caveIsComposite) && underground > 0.005f));
    }

    internal static string ResolveSurfaceSpriteId(string? biomeId, string fallback)
    {
        return Is(biomeId, "meadow")
            ? "world/backgrounds/meadow_parallax_layer"
            : fallback;
    }

    internal static string ResolveCaveSpriteId(string? subBiomeId, string? caveProfileId, string fallback)
    {
        var identity = subBiomeId ?? caveProfileId;
        if (Contains(identity, "mushroom"))
        {
            return "world/backgrounds/mushroom_cave_parallax_layer";
        }

        return Contains(identity, "crystal")
            ? "world/backgrounds/crystal_depths_parallax_layer"
            : fallback;
    }

    private static void AddLayer(
        Span<ParallaxLayerDescriptor> destination,
        ref int count,
        string spriteId,
        string? alternateSpriteId,
        string? landmarkSpriteId,
        int landmarkFrameIndex,
        ParallaxLandmarkStyle landmarkStyle,
        float horizontalParallax,
        float verticalParallax,
        float opacity,
        int verticalOffset,
        float scaleMultiplier,
        float repeatOverlap,
        int verticalJitter,
        int landmarkPeriod,
        uint variationSeed,
        bool preserveAuthoredRepeat,
        Color tint,
        Color landmarkTint,
        ParallaxProjectionMode projectionMode = ParallaxProjectionMode.ViewportBackdrop,
        ParallaxDepthPlane depthPlane = ParallaxDepthPlane.Unspecified,
        Color? topFillColor = null,
        bool featherTop = false,
        ParallaxVerticalFillMode? verticalFillMode = null,
        Color? bottomFillColor = null)
    {
        if (count >= destination.Length || opacity <= 0.001f)
        {
            return;
        }

        var resolvedFillMode = projectionMode == ParallaxProjectionMode.FullscreenDepthPlane
            ? ParallaxVerticalFillMode.None
            : preserveAuthoredRepeat
                ? verticalFillMode ?? ParallaxVerticalFillMode.ExtendOuterEdges
                : ParallaxVerticalFillMode.None;
        destination[count++] = new ParallaxLayerDescriptor(
            spriteId,
            Is(spriteId, alternateSpriteId) ? null : alternateSpriteId,
            landmarkSpriteId,
            Math.Max(0, landmarkFrameIndex),
            landmarkStyle,
            horizontalParallax,
            verticalParallax,
            Math.Clamp(opacity, 0f, 1f),
            verticalOffset,
            scaleMultiplier,
            Math.Clamp(repeatOverlap, 0f, 0.2f),
            Math.Clamp(verticalJitter, 0, 24),
            Math.Clamp(landmarkPeriod, 0, 16),
            variationSeed,
            preserveAuthoredRepeat,
            resolvedFillMode,
            tint,
            landmarkTint)
        {
            ProjectionMode = projectionMode,
            DepthPlane = depthPlane,
            TopFillColor = resolvedFillMode == ParallaxVerticalFillMode.ExtendOuterEdges
                ? topFillColor ?? ResolveAuthoredTopFillColor(spriteId)
                : Color.Transparent,
            BottomFillColor = projectionMode != ParallaxProjectionMode.FullscreenDepthPlane &&
                (resolvedFillMode is ParallaxVerticalFillMode.ExtendBottomEdge or
                    ParallaxVerticalFillMode.ExtendOuterEdges)
                ? bottomFillColor ?? ResolveAuthoredBottomFillColor(spriteId)
                : Color.Transparent,
            FeatherTop = featherTop
        };
    }

    private static void AddAuthoredSurfaceDepthStack(
        Span<ParallaxLayerDescriptor> destination,
        ref int count,
        string? biomeId,
        string farSpriteId,
        string midSpriteId,
        string nearSpriteId,
        float baseHorizontalParallax,
        float opacity,
        uint variationSeed,
        Color environmentTint,
        Color topFillColor)
    {
        AddLayer(
            destination,
            ref count,
            ResolveMountainFeatureSpriteId(biomeId, farSpriteId),
            null,
            null,
            0,
            ParallaxLandmarkStyle.None,
            baseHorizontalParallax * 0.035f,
            0.0012f,
            opacity * 0.62f,
            -18,
            1f,
            0f,
            5,
            0,
            Mix(variationSeed, 0xA24BAED5u),
            true,
            Color.Lerp(Color.White, environmentTint, 0.18f),
            Color.White,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Far,
            Color.Transparent,
            verticalFillMode: ParallaxVerticalFillMode.None);
        AddLayer(
            destination,
            ref count,
            ResolveFloatingIslandFeatureSpriteId(biomeId, farSpriteId),
            null,
            null,
            0,
            ParallaxLandmarkStyle.None,
            baseHorizontalParallax * 0.11f,
            0.004f,
            opacity * 0.78f,
            -5,
            1f,
            0f,
            8,
            0,
            Mix(variationSeed, 0x9FB21C65u),
            true,
            Color.Lerp(Color.White, environmentTint, 0.08f),
            Color.White,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Mid,
            Color.Transparent,
            verticalFillMode: ParallaxVerticalFillMode.None);
        AddAuthoredDepthStack(
            destination,
            ref count,
            farSpriteId,
            midSpriteId,
            nearSpriteId,
            baseHorizontalParallax,
            opacity,
            variationSeed,
            environmentTint,
            topFillColor);
    }

    private static void AddAuthoredDepthStack(
        Span<ParallaxLayerDescriptor> destination,
        ref int count,
        string farSpriteId,
        string midSpriteId,
        string nearSpriteId,
        float baseHorizontalParallax,
        float opacity,
        uint variationSeed,
        Color environmentTint,
        Color topFillColor)
    {
        AddLayer(
            destination,
            ref count,
            farSpriteId,
            null,
            null,
            0,
            ParallaxLandmarkStyle.None,
            baseHorizontalParallax * 0.08f,
            0.0025f,
            opacity,
            -8,
            0.5f,
            0f,
            0,
            0,
            Mix(variationSeed, 0x2C1B3C6Du),
            true,
            Color.Lerp(Color.White, environmentTint, 0.22f),
            Color.White,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Far,
            Color.Transparent,
            featherTop: false,
            verticalFillMode: ParallaxVerticalFillMode.None);
        AddLayer(
            destination,
            ref count,
            midSpriteId,
            null,
            null,
            0,
            ParallaxLandmarkStyle.None,
            baseHorizontalParallax * 0.18f,
            0.006f,
            opacity * 0.24f,
            0,
            0.68f,
            0f,
            0,
            0,
            Mix(variationSeed, 0x6A09E667u),
            true,
            Color.Lerp(Color.White, environmentTint, 0.14f),
            Color.White,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Mid,
            Color.Transparent,
            verticalFillMode: ParallaxVerticalFillMode.None);
        AddLayer(
            destination,
            ref count,
            nearSpriteId,
            null,
            null,
            0,
            ParallaxLandmarkStyle.None,
            baseHorizontalParallax * 0.32f,
            0.012f,
            opacity * 0.14f,
            8,
            0.92f,
            0f,
            0,
            0,
            Mix(variationSeed, 0xBB67AE85u),
            true,
            Color.Lerp(Color.White, environmentTint, 0.22f),
            Color.White,
            ParallaxProjectionMode.FullscreenDepthPlane,
            ParallaxDepthPlane.Near,
            Color.Transparent,
            verticalFillMode: ParallaxVerticalFillMode.None);
    }

    private static string ResolveMountainFeatureSpriteId(string? biomeId, string farSpriteId)
    {
        if (Contains(farSpriteId, "amber") || Is(biomeId, "amber_grove"))
        {
            return "world/backgrounds/features_v7/amber_mountains";
        }

        if (Contains(farSpriteId, "twilight") || Contains(farSpriteId, "marsh") || Is(biomeId, "twilight_marsh"))
        {
            return "world/backgrounds/features_v7/twilight_mountains";
        }

        return Contains(farSpriteId, "crystal") || Contains(biomeId, "crystal")
            ? "world/backgrounds/features_v7/crystal_mountains"
            : "world/backgrounds/features_v7/forest_mountains";
    }

    private static string ResolveFloatingIslandFeatureSpriteId(string? biomeId, string farSpriteId)
    {
        if (Contains(farSpriteId, "amber") || Is(biomeId, "amber_grove"))
        {
            return "world/backgrounds/features_v7/amber_floating_islands";
        }

        if (Contains(farSpriteId, "twilight") || Contains(farSpriteId, "marsh") || Is(biomeId, "twilight_marsh"))
        {
            return "world/backgrounds/features_v7/twilight_floating_islands";
        }

        return Contains(farSpriteId, "crystal") || Contains(biomeId, "crystal")
            ? "world/backgrounds/features_v7/crystal_floating_islands"
            : "world/backgrounds/features_v7/forest_floating_islands";
    }

    private static Color ScaleColor(Color color, float scale)
    {
        scale = Math.Clamp(scale, 0f, 1f);
        return new Color(
            (byte)Math.Clamp((int)MathF.Round(color.R * scale), 0, byte.MaxValue),
            (byte)Math.Clamp((int)MathF.Round(color.G * scale), 0, byte.MaxValue),
            (byte)Math.Clamp((int)MathF.Round(color.B * scale), 0, byte.MaxValue),
            color.A);
    }

    internal static string ResolveAuthoredFarSpriteId(
        string primary,
        string? biomeId,
        string? subBiomeId,
        bool isCave)
    {
        if (isCave)
        {
            return Contains(primary, "crystal") || Contains(subBiomeId, "crystal")
                ? "world/backgrounds/depth_v6/crystal_far"
                : primary;
        }

        if (Contains(primary, "amber") || Is(biomeId, "amber_grove"))
        {
            return "world/backgrounds/depth_v6/amber_far";
        }

        if (Contains(primary, "twilight") || Contains(primary, "marsh") || Is(biomeId, "twilight_marsh"))
        {
            return "world/backgrounds/depth_v6/twilight_far";
        }

        return Contains(primary, "forest") || Is(biomeId, "forest") || Is(biomeId, "meadow")
            ? "world/backgrounds/depth_v6/forest_far"
            : primary;
    }

    internal static string ResolveAuthoredMidSpriteId(
        string primary,
        string? biomeId,
        string? subBiomeId,
        bool isCave)
    {
        if (isCave)
        {
            if (Contains(primary, "crystal") || Contains(subBiomeId, "crystal"))
            {
                return "world/backgrounds/depth_v6/crystal_mid";
            }

            if (Contains(primary, "mushroom") || Contains(subBiomeId, "mushroom"))
            {
                return "world/backgrounds/mushroom_cave_parallax_layer";
            }

            return "world/backgrounds/cave_parallax_layer_v3";
        }

        if (Contains(primary, "amber") || Is(biomeId, "amber_grove"))
        {
            return "world/backgrounds/depth_v6/amber_mid";
        }

        if (Contains(primary, "twilight") || Contains(primary, "marsh") || Is(biomeId, "twilight_marsh"))
        {
            return "world/backgrounds/depth_v6/twilight_mid";
        }

        if (Contains(primary, "forest") || Is(biomeId, "forest") || Is(biomeId, "meadow"))
        {
            return "world/backgrounds/depth_v6/forest_mid";
        }

        return Contains(primary, "_v3")
            ? "world/backgrounds/forest_parallax_layer"
            : "world/backgrounds/forest_parallax_layer_v4";
    }

    internal static string ResolveAuthoredNearSpriteId(
        string primary,
        string? biomeId,
        string? subBiomeId,
        bool isCave)
    {
        if (isCave)
        {
            if (Contains(primary, "crystal") || Contains(subBiomeId, "crystal"))
            {
                return "world/backgrounds/depth_v6/crystal_near";
            }

            if (Contains(primary, "mushroom") || Contains(subBiomeId, "mushroom"))
            {
                return "world/backgrounds/wave04/mushroom_parallax_layer";
            }

            return "world/backgrounds/wave04/cave_parallax_layer";
        }

        if (Contains(primary, "amber") || Is(biomeId, "amber_grove"))
        {
            return "world/backgrounds/depth_v6/amber_near";
        }

        if (Contains(primary, "twilight") || Contains(primary, "marsh") || Is(biomeId, "twilight_marsh"))
        {
            return "world/backgrounds/depth_v6/twilight_near";
        }

        return Contains(primary, "forest") || Is(biomeId, "forest") || Is(biomeId, "meadow")
            ? "world/backgrounds/depth_v6/forest_near"
            : "world/backgrounds/forest_parallax_layer_v3";
    }

    internal static Color ResolveAuthoredBottomFillColor(string spriteId)
    {
        if (Contains(spriteId, "amber"))
        {
            return new Color(64, 49, 42);
        }

        if (Contains(spriteId, "twilight") || Contains(spriteId, "marsh"))
        {
            return new Color(31, 38, 58);
        }

        if (Contains(spriteId, "crystal"))
        {
            return new Color(17, 25, 52);
        }

        if (Contains(spriteId, "forest") || Contains(spriteId, "meadow"))
        {
            return new Color(42, 61, 54);
        }

        if (Contains(spriteId, "cave") || Contains(spriteId, "depth"))
        {
            return new Color(20, 26, 39);
        }

        return new Color(42, 61, 54);
    }

    internal static Color ResolveAuthoredTopFillColor(string spriteId)
    {
        if (Contains(spriteId, "amber"))
        {
            return new Color(253, 214, 153);
        }

        if (Contains(spriteId, "twilight") || Contains(spriteId, "marsh"))
        {
            return new Color(100, 102, 189);
        }

        if (Contains(spriteId, "crystal") || Contains(spriteId, "depth"))
        {
            return new Color(17, 25, 61);
        }

        return new Color(175, 235, 241);
    }

    private static string ResolveSurfaceAlternateSpriteId(string? biomeId, bool isNight, string primary)
    {
        if (IsCompositePanorama(primary))
        {
            return primary;
        }

        if (isNight && Is(biomeId, "forest"))
        {
            return "world/backgrounds/night_forest_parallax_layer";
        }

        if (Is(biomeId, "meadow"))
        {
            return "world/backgrounds/wave04/meadow_parallax_layer";
        }

        if (Is(biomeId, "amber_grove"))
        {
            return "world/backgrounds/magical_grove_parallax_layer";
        }

        if (Is(biomeId, "twilight_marsh"))
        {
            return "world/backgrounds/mushroom_cave_parallax_layer";
        }

        return Is(primary, "world/backgrounds/wave04/forest_parallax_layer")
            ? "world/backgrounds/forest_parallax_layer"
            : "world/backgrounds/wave04/forest_parallax_layer";
    }

    private static string ResolveCaveAlternateSpriteId(string? subBiomeId, string? caveProfileId, string primary)
    {
        if (IsCompositePanorama(primary))
        {
            return primary;
        }

        var identity = subBiomeId ?? caveProfileId;
        if (Contains(identity, "mushroom"))
        {
            return Is(primary, "world/backgrounds/wave04/mushroom_parallax_layer")
                ? "world/backgrounds/mushroom_cave_parallax_layer"
                : "world/backgrounds/wave04/mushroom_parallax_layer";
        }

        if (Contains(identity, "crystal"))
        {
            return Is(primary, "world/backgrounds/wave04/crystal_parallax_layer")
                ? "world/backgrounds/crystal_depths_parallax_layer"
                : "world/backgrounds/wave04/crystal_parallax_layer";
        }

        return Is(primary, "world/backgrounds/wave04/cave_parallax_layer")
            ? "world/backgrounds/cave_parallax_layer"
            : "world/backgrounds/wave04/cave_parallax_layer";
    }

    private static LandmarkProfile ResolveLandmark(string? biomeId, string? subBiomeId, string? caveProfileId)
    {
        var caveIdentity = subBiomeId ?? caveProfileId;
        if (Contains(caveIdentity, "mushroom"))
        {
            return new LandmarkProfile(
                "world/decor/mushrooms",
                0,
                ParallaxLandmarkStyle.Canopy,
                ParallaxLandmarkStyle.MushroomColony,
                4,
                new Color(119, 76, 139));
        }

        if (Contains(caveIdentity, "crystal"))
        {
            return new LandmarkProfile(
                null,
                0,
                ParallaxLandmarkStyle.Canopy,
                ParallaxLandmarkStyle.CrystalCluster,
                4,
                new Color(74, 153, 186));
        }

        if (Is(biomeId, "twilight_marsh"))
        {
            return new LandmarkProfile(
                "world/wave05/hanging_lantern_chain",
                3,
                ParallaxLandmarkStyle.Mangrove,
                ParallaxLandmarkStyle.CaveSpire,
                5,
                new Color(58, 99, 91));
        }

        if (Is(biomeId, "amber_grove"))
        {
            return new LandmarkProfile(
                "world/wave05/amber_workshop_set",
                1,
                ParallaxLandmarkStyle.AmberWorkshop,
                ParallaxLandmarkStyle.CaveSpire,
                5,
                new Color(125, 84, 42));
        }

        return new LandmarkProfile(
            "world/decor/tree_species_props",
            0,
            ParallaxLandmarkStyle.Canopy,
            ParallaxLandmarkStyle.CaveSpire,
            5,
            Is(biomeId, "meadow") ? new Color(67, 114, 73) : new Color(42, 79, 62));
    }

    private static ParallaxLandmarkStyle ResolveDeepLandmarkStyle(string? subBiomeId, string? caveProfileId)
    {
        var identity = subBiomeId ?? caveProfileId;
        if (Contains(identity, "crystal"))
        {
            return ParallaxLandmarkStyle.CrystalCluster;
        }

        return Contains(identity, "mushroom")
            ? ParallaxLandmarkStyle.MushroomColony
            : ParallaxLandmarkStyle.CaveSpire;
    }

    private static SkyPalette ResolveSkyPalette(
        in LivingWorldFrameSnapshot livingWorld,
        float nightBlend,
        float underground,
        float deep)
    {
        var surface = ResolveSurfacePalette(livingWorld.BiomeId);
        var night = Math.Clamp(nightBlend, 0f, 0.82f);
        var nightTop = Color.Lerp(surface.Top, new Color(12, 19, 42), night);
        var nightMiddle = Color.Lerp(surface.Middle, new Color(24, 30, 57), night);
        var nightBottom = Color.Lerp(surface.Bottom, new Color(38, 39, 63), night);
        var storm = livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm
            ? livingWorld.WeatherIntensity
            : 0f;
        var fog = livingWorld.Weather == Game.Core.Weather.WeatherKind.Fog
            ? livingWorld.WeatherIntensity
            : 0f;
        var weatherBlend = Math.Clamp(livingWorld.CloudCover * 0.32f + storm * 0.24f + fog * 0.12f, 0f, 0.58f);
        var weatherColor = Color.Lerp(new Color(86, 103, 116), new Color(49, 55, 69), storm);
        var cave = ResolveCavePalette(livingWorld.SubBiomeId, livingWorld.CaveProfileId, deep);
        return new SkyPalette(
            Color.Lerp(Color.Lerp(nightTop, weatherColor, weatherBlend), cave.Top, underground),
            Color.Lerp(Color.Lerp(nightMiddle, weatherColor, weatherBlend), cave.Middle, underground),
            Color.Lerp(Color.Lerp(nightBottom, weatherColor, weatherBlend), cave.Bottom, underground));
    }

    private static SkyPalette ResolveSurfacePalette(string? biomeId)
    {
        if (Is(biomeId, "meadow"))
        {
            return new SkyPalette(new Color(104, 178, 218), new Color(111, 173, 194), new Color(174, 178, 127));
        }

        if (Is(biomeId, "amber_grove"))
        {
            return new SkyPalette(new Color(98, 143, 183), new Color(145, 139, 134), new Color(194, 132, 76));
        }

        if (Is(biomeId, "twilight_marsh"))
        {
            return new SkyPalette(new Color(48, 91, 118), new Color(66, 104, 115), new Color(87, 95, 93));
        }

        return new SkyPalette(
            new Color(75, 145, 199),
            new Color(104, 183, 207),
            new Color(151, 211, 209));
    }

    private static SkyPalette ResolveCavePalette(string? subBiomeId, string? caveProfileId, float deep)
    {
        var identity = subBiomeId ?? caveProfileId;
        if (Contains(identity, "mushroom"))
        {
            return new SkyPalette(new Color(31, 22, 43), new Color(48, 28, 54), new Color(68, 39, 67));
        }

        if (Contains(identity, "crystal"))
        {
            return new SkyPalette(new Color(18, 27, 46), new Color(27, 43, 61), new Color(37, 58, 69));
        }

        return new SkyPalette(
            Color.Lerp(new Color(25, 25, 34), new Color(16, 15, 25), deep),
            Color.Lerp(new Color(35, 31, 40), new Color(24, 21, 34), deep),
            Color.Lerp(new Color(46, 37, 45), new Color(34, 27, 39), deep));
    }

    private static Color ResolveSurfaceTint(string? biomeId)
    {
        if (Is(biomeId, "meadow"))
        {
            return new Color(237, 240, 207);
        }

        if (Is(biomeId, "amber_grove"))
        {
            return new Color(235, 202, 154);
        }

        return Is(biomeId, "twilight_marsh")
            ? new Color(163, 203, 191)
            : new Color(218, 232, 221);
    }

    private static Color ResolveHorizonTint(string? biomeId)
    {
        if (Is(biomeId, "amber_grove"))
        {
            return new Color(215, 151, 91);
        }

        return Is(biomeId, "twilight_marsh")
            ? new Color(86, 141, 139)
            : new Color(142, 180, 196);
    }

    private static Color ResolveCaveTint(string? subBiomeId, string? caveProfileId)
    {
        var identity = subBiomeId ?? caveProfileId;
        if (Contains(identity, "mushroom"))
        {
            return new Color(205, 153, 222);
        }

        return Contains(identity, "crystal")
            ? new Color(139, 207, 235)
            : new Color(174, 164, 186);
    }

    private static uint BuildVariationSeed(in LivingWorldFrameSnapshot livingWorld)
    {
        var region = unchecked((ulong)livingWorld.RegionIndex);
        var seed = unchecked((uint)region ^ (uint)(region >> 32));
        seed = Mix(seed, StableHash(livingWorld.BiomeId));
        seed = Mix(seed, StableHash(livingWorld.SubBiomeId));
        seed = Mix(seed, StableHash(livingWorld.CaveProfileId));
        return seed == 0 ? 0xA511E9B3u : seed;
    }

    private static uint StableHash(string? value)
    {
        var hash = 2166136261u;
        if (value is null)
        {
            return hash;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = char.ToUpperInvariant(value[index]);
            hash = unchecked((hash ^ character) * 16777619u);
        }

        return hash;
    }

    private static uint Mix(uint value, uint salt)
    {
        value ^= salt + 0x9E3779B9u + (value << 6) + (value >> 2);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        return value ^ (value >> 16);
    }

    private static bool Is(string? value, string? expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsCompositePanorama(string? spriteId)
    {
        if (spriteId?.Contains("panorama", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (string.IsNullOrEmpty(spriteId))
        {
            return false;
        }

        var versionMarker = spriteId.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
        if (versionMarker < 0 || versionMarker + 2 >= spriteId.Length)
        {
            return false;
        }

        for (var index = versionMarker + 2; index < spriteId.Length; index++)
        {
            if (!char.IsAsciiDigit(spriteId[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCavePanorama(string? spriteId)
    {
        return Contains(spriteId, "cave") ||
               Contains(spriteId, "depth") ||
               Contains(spriteId, "hollow") ||
               Contains(spriteId, "underground");
    }

    private readonly record struct LandmarkProfile(
        string? SpriteId,
        int FrameIndex,
        ParallaxLandmarkStyle Style,
        ParallaxLandmarkStyle UndergroundStyle,
        int Period,
        Color Tint);

    private readonly record struct SkyPalette(Color Top, Color Middle, Color Bottom);
}
