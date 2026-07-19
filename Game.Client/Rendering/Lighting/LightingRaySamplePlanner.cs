using Game.Client.Rendering.Effects;

namespace Game.Client.Rendering.Lighting;

internal readonly record struct LightingRaySamplePlan(
    int PointShadowSamples,
    int EndpointSpreadMaskPixels,
    int MaxStepsPerRay,
    long MaximumPointShadowRays,
    bool WasBudgetClamped);

/// <summary>
/// Resolves a bounded CPU tile-mask ray budget. This is a screen-space 2D
/// approximation and deliberately does not represent hardware ray tracing.
/// </summary>
internal static class LightingRaySamplePlanner
{
    private const int MaximumSoftShadowSamples = 3;

    public static LightingRaySamplePlan Build(
        in PresentationQualityProfile profile,
        int requestedPointLights)
    {
        var requested = Math.Max(0, requestedPointLights);
        var lights = Math.Min(requested, profile.Budget.MaxPointLights);
        var maxSteps = profile.CastPointLightShadows
            ? Math.Max(0, profile.Budget.MaxRayStepsPerSample)
            : 0;
        if (lights == 0 || maxSteps == 0 || profile.MaskPixelCount == 0)
        {
            return new LightingRaySamplePlan(
                PointShadowSamples: 0,
                EndpointSpreadMaskPixels: 0,
                MaxStepsPerRay: maxSteps,
                MaximumPointShadowRays: 0,
                WasBudgetClamped: requested != lights);
        }

        var samples = profile.Tier == PresentationQualityTier.High &&
            profile.Budget.MaxPenumbraRadius >= 2
            ? MaximumSoftShadowSamples
            : 1;
        var spread = samples > 1
            ? Math.Clamp(profile.Budget.MaxPenumbraRadius / 2, 1, 2)
            : 0;
        var maximumRays = SaturatingMultiply(
            SaturatingMultiply(profile.MaskPixelCount, lights),
            samples);
        return new LightingRaySamplePlan(
            samples,
            spread,
            maxSteps,
            maximumRays,
            requested != lights);
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }
}
