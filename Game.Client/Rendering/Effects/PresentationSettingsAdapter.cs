using Game.Client.Rendering.Lighting;
using Game.Core.Runtime;
using Game.Core.Settings;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public readonly record struct PresentationRuntimeConfiguration(
    PresentationQualityProfile Lighting,
    PresentationQualityProfile Reflections,
    PresentationQualityTier UiEffects,
    bool AmbientOcclusion,
    bool SunLightShafts,
    bool SoftShadows,
    float ReflectionStrength);

public static class PresentationSettingsAdapter
{
    public static PresentationRuntimeConfiguration Create(
        RenderingSettings settings,
        Rectangle viewport)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var lightingTier = ResolveTier(settings.LightingQuality);
        var shadowTier = ResolveTier(settings.ShadowQuality);
        var reflectionTier = settings.ScreenSpaceReflections
            ? ResolveTier(settings.ReflectionQuality)
            : PresentationQualityTier.Disabled;
        var lightingBudget = CreateLightingBudget(settings, lightingTier, shadowTier, viewport);
        var lighting = PresentationQualityProfile.Create(lightingTier, viewport, lightingBudget);
        lighting = lighting with
        {
            AmbientOcclusionRadius = settings.AmbientOcclusion
                ? lighting.AmbientOcclusionRadius
                : 0,
            CastSunShadows = settings.SunLightShafts &&
                shadowTier != PresentationQualityTier.Disabled &&
                lighting.Budget.MaxRayStepsPerSample > 0,
            CastPointLightShadows = shadowTier >= PresentationQualityTier.Medium &&
                lighting.Budget.MaxRayStepsPerSample > 0,
            EnableBloom = settings.TorchBloomStrength > 0.001f &&
                lighting.Budget.MaxBloomRadius > 0
        };

        var reflections = PresentationQualityProfile.Create(reflectionTier, viewport);
        reflections = reflections with
        {
            EnableReflections = reflections.EnableReflections &&
                settings.ScreenSpaceReflections &&
                reflectionTier != PresentationQualityTier.Disabled &&
                settings.ReflectionStrength > 0.001f
        };
        return new PresentationRuntimeConfiguration(
            lighting,
            reflections,
            ResolveTier(settings.UiEffectQuality),
            settings.AmbientOcclusion,
            settings.SunLightShafts,
            settings.SoftShadows,
            Math.Clamp(settings.ReflectionStrength, 0f, 1f));
    }

    public static LightingFrameParameters CreateLightingFrame(
        RenderingSettings settings,
        World world,
        Camera2D camera,
        in WorldTimeFrameSnapshot time,
        in LivingWorldFrameSnapshot livingWorld,
        long frameIndex)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return LightingFrameParameters.FromSnapshots(
            world,
            camera,
            time,
            livingWorld,
            frameIndex,
            shadowStrength: settings.LightingBlendStrength,
            bloomStrength: settings.TorchBloomStrength) with
        {
            CaveResidualLight = ResolveCaveResidualLight(settings, livingWorld),
            ShadowStrength = Math.Clamp(settings.LightingBlendStrength, 0f, 1f),
            BloomStrength = Math.Clamp(settings.TorchBloomStrength, 0f, 1f)
        };
    }

    internal static float ResolveCaveResidualLight(
        RenderingSettings settings,
        in LivingWorldFrameSnapshot livingWorld)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var configuredFloor = Math.Clamp(settings.CaveAmbientLight, 0f, 0.65f);
        if (!livingWorld.IsUnderground)
        {
            return configuredFloor;
        }

        // Authored cave ambience is a biome contract (crystal glow, mushroom
        // luminescence, etc.). The player setting remains a global minimum,
        // while intentionally dark biomes continue to use that minimum.
        var authoredFloor = Math.Clamp(livingWorld.AmbientLight * 0.9f, 0f, 0.65f);
        return Math.Max(configuredFloor, authoredFloor);
    }

    public static PresentationQualityTier ResolveTier(int quality)
    {
        return quality switch
        {
            <= 0 => PresentationQualityTier.Disabled,
            1 => PresentationQualityTier.Low,
            2 => PresentationQualityTier.Medium,
            _ => PresentationQualityTier.High
        };
    }

    private static PresentationBudget CreateLightingBudget(
        RenderingSettings settings,
        PresentationQualityTier lightingTier,
        PresentationQualityTier shadowTier,
        Rectangle viewport)
    {
        var maximum = PresentationBudget.ForTier(lightingTier);
        var shadowMaximum = PresentationBudget.ForTier(shadowTier);
        if (lightingTier == PresentationQualityTier.Disabled)
        {
            return default;
        }

        var divisor = lightingTier switch
        {
            PresentationQualityTier.Low => 16,
            PresentationQualityTier.Medium => 12,
            _ => 8
        };
        var requestedBlurTexels = settings.BlurRadiusPixels == 0
            ? 0
            : Math.Max(1, DivideRoundUp(settings.BlurRadiusPixels, divisor));
        var softnessRadius = settings.SoftShadows
            ? (int)MathF.Ceiling(
                requestedBlurTexels * (0.75f + Math.Clamp(settings.ShadowSoftness, 0f, 1f)))
            : 0;
        var raySteps = Math.Min(
            settings.RaymarchStepBudget,
            Math.Min(maximum.MaxRayStepsPerSample, shadowMaximum.MaxRayStepsPerSample));
        var viewportArea = Math.Max(0L, (long)viewport.Width * viewport.Height);
        var maskPixels = (int)Math.Min(
            maximum.MaxMaskPixels,
            Math.Max(1L, viewportArea / ((long)divisor * divisor)));
        return maximum with
        {
            MaxMaskPixels = maskPixels,
            MaxRayStepsPerSample = Math.Max(0, raySteps),
            MaxPenumbraRadius = Math.Min(
                Math.Min(maximum.MaxPenumbraRadius, shadowMaximum.MaxPenumbraRadius),
                softnessRadius),
            MaxBloomRadius = Math.Min(maximum.MaxBloomRadius, requestedBlurTexels)
        };
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return value / divisor + (value % divisor == 0 ? 0 : 1);
    }
}
