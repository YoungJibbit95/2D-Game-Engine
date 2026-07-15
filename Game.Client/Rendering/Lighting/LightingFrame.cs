using Game.Client.Rendering.Effects;
using Game.Core;
using Game.Core.Runtime;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

public readonly record struct ScreenSpaceLight(
    Vector2 WorldPosition,
    float RadiusPixels,
    Color Color,
    float Intensity,
    float EmissiveStrength,
    bool CastsShadows,
    uint StableId,
    float FlickerAmount)
{
    public static ScreenSpaceLight Torch(
        Vector2 worldPosition,
        uint stableId,
        float radiusPixels = 112f,
        float intensity = 1f)
    {
        return new ScreenSpaceLight(
            worldPosition,
            radiusPixels,
            new Color(255, 164, 82),
            intensity,
            EmissiveStrength: 0.9f,
            CastsShadows: true,
            stableId,
            FlickerAmount: 0.08f);
    }
}

public readonly record struct LightingFrameParameters(
    float NormalizedTimeOfDay,
    float AmbientLight,
    float SkyLightMultiplier,
    float EmissiveLightMultiplier,
    float CaveBlend,
    float CaveResidualLight,
    float ShadowStrength,
    float BloomStrength,
    float WeatherOcclusion,
    long FrameIndex)
{
    public static LightingFrameParameters FromSnapshots(
        World world,
        Camera2D camera,
        in WorldTimeFrameSnapshot time,
        in LivingWorldFrameSnapshot livingWorld,
        long frameIndex,
        float shadowStrength = 1f,
        float bloomStrength = 0.55f)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);

        var caveBlend = livingWorld.IsUnderground
            ? Math.Clamp(0.32f + (1f - livingWorld.AmbientLight) * 0.52f, 0.32f, 0.84f)
            : 0f;
        var weatherOcclusion = Math.Clamp(
            livingWorld.CloudCover * 0.36f +
            (livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm
                ? livingWorld.WeatherIntensity * 0.18f
                : 0f),
            0f,
            0.65f);

        return new LightingFrameParameters(
            (float)time.NormalizedTimeOfDay,
            Math.Clamp(livingWorld.AmbientLight, 0f, 1f),
            Math.Clamp(livingWorld.SkyLightMultiplier, 0f, 2f),
            Math.Clamp(livingWorld.EmissiveLightMultiplier, 0f, 2f),
            caveBlend,
            CaveResidualLight: 0.085f,
            Math.Clamp(shadowStrength, 0f, 1f),
            Math.Clamp(bloomStrength, 0f, 1.5f),
            weatherOcclusion,
            Math.Max(0, frameIndex));
    }
}

public readonly record struct LightingBuildTelemetry(
    PresentationQualityTier Quality,
    Point MaskSize,
    int PointLightsUsed,
    long RaysCast,
    long OccluderSamples,
    bool WasBudgetClamped);
