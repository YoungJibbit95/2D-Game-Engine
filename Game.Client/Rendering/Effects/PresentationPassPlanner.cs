namespace Game.Client.Rendering.Effects;

[Flags]
public enum PresentationFeature
{
    None = 0,
    Lighting = 1 << 0,
    AmbientOcclusion = 1 << 1,
    Bloom = 1 << 2,
    Reflections = 1 << 3,
    Atmosphere = 1 << 4
}

public enum PresentationPassKind
{
    LowQualityLightingFallback,
    CpuTileRayCastSunShadow,
    CpuTileRayCastPointLights,
    AmbientOcclusion,
    PenumbraHorizontal,
    PenumbraVertical,
    ColoredLightComposite,
    EmissiveBloomHorizontal,
    EmissiveBloomVertical,
    SceneColorCopy,
    WaterReflection,
    WetSurfaceSpecular,
    AtmosphereComposite
}

public readonly record struct PresentationPassDescriptor(
    PresentationPassKind Kind,
    int Width,
    int Height,
    int Iterations,
    long EstimatedWorkItems);

public readonly record struct PresentationPassRequest(
    PresentationFeature Features,
    int RequestedPointLights,
    int RequestedReflectionSurfaces,
    float BloomStrength,
    float ReflectionStrength);

public readonly record struct PresentationPassPlan(
    int PassCount,
    int PointLights,
    int ReflectionSurfaces,
    long EstimatedWorkItems,
    bool WasBudgetClamped);

public static class PresentationPassPlanner
{
    public const int MaximumPassCount = 13;

    public static PresentationPassPlan Build(
        in PresentationQualityProfile profile,
        in PresentationPassRequest request,
        Span<PresentationPassDescriptor> destination)
    {
        if (destination.Length < MaximumPassCount)
        {
            throw new ArgumentException(
                $"Pass-plan destination must contain at least {MaximumPassCount} entries.",
                nameof(destination));
        }

        var passCount = 0;
        var totalWork = 0L;
        var requestedLights = Math.Max(0, request.RequestedPointLights);
        var requestedSurfaces = Math.Max(0, request.RequestedReflectionSurfaces);
        var lights = Math.Min(requestedLights, profile.Budget.MaxPointLights);
        var surfaces = Math.Min(requestedSurfaces, profile.Budget.MaxReflectionSurfaces);
        var maskPixels = profile.MaskPixelCount;

        if (profile.Tier == PresentationQualityTier.Disabled)
        {
            return default;
        }

        if ((request.Features & PresentationFeature.Lighting) != 0)
        {
            if (profile.CastSunShadows)
            {
                AddPass(ref passCount, ref totalWork, destination, profile,
                    PresentationPassKind.CpuTileRayCastSunShadow,
                    profile.Budget.MaxRayStepsPerSample,
                    SaturatingMultiply(maskPixels, profile.Budget.MaxRayStepsPerSample));
            }
            else
            {
                AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.LowQualityLightingFallback, 1, maskPixels);
            }

            if (lights > 0)
            {
                var pointIterations = profile.CastPointLightShadows
                    ? profile.Budget.MaxRayStepsPerSample
                    : 1;
                AddPass(ref passCount, ref totalWork, destination, profile,
                    PresentationPassKind.CpuTileRayCastPointLights,
                    pointIterations,
                    SaturatingMultiply(SaturatingMultiply(maskPixels, lights), pointIterations));
            }

            if ((request.Features & PresentationFeature.AmbientOcclusion) != 0)
            {
                var aoSamples = (profile.AmbientOcclusionRadius * 2 + 1) *
                    (profile.AmbientOcclusionRadius * 2 + 1) - 1;
                AddPass(ref passCount, ref totalWork, destination, profile,
                    PresentationPassKind.AmbientOcclusion,
                    aoSamples,
                    SaturatingMultiply(maskPixels, aoSamples));
            }

            if (profile.Budget.MaxPenumbraRadius > 0)
            {
                var blurTaps = profile.Budget.MaxPenumbraRadius * 2 + 1;
                AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.PenumbraHorizontal, blurTaps, SaturatingMultiply(maskPixels, blurTaps));
                AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.PenumbraVertical, blurTaps, SaturatingMultiply(maskPixels, blurTaps));
            }

            AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.ColoredLightComposite, 1, maskPixels);
        }

        if ((request.Features & PresentationFeature.Bloom) != 0 &&
            profile.EnableBloom &&
            request.BloomStrength > 0.001f)
        {
            var bloomTaps = profile.Budget.MaxBloomRadius * 2 + 1;
            AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.EmissiveBloomHorizontal, bloomTaps, SaturatingMultiply(maskPixels, bloomTaps));
            AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.EmissiveBloomVertical, bloomTaps, SaturatingMultiply(maskPixels, bloomTaps));
        }

        if ((request.Features & PresentationFeature.Reflections) != 0 &&
            profile.EnableReflections &&
            surfaces > 0 &&
            request.ReflectionStrength > 0.001f)
        {
            AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.SceneColorCopy, 1, maskPixels);
            var strips = profile.Budget.MaxReflectionStripsPerSurface;
            AddPass(ref passCount, ref totalWork, destination, profile,
                PresentationPassKind.WaterReflection,
                strips,
                SaturatingMultiply(surfaces, strips));
            AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.WetSurfaceSpecular, 1, surfaces);
        }

        if ((request.Features & PresentationFeature.Atmosphere) != 0)
        {
            AddPass(ref passCount, ref totalWork, destination, profile, PresentationPassKind.AtmosphereComposite, 1, maskPixels);
        }

        return new PresentationPassPlan(
            passCount,
            lights,
            surfaces,
            totalWork,
            requestedLights != lights || requestedSurfaces != surfaces);
    }

    private static void AddPass(
        ref int passCount,
        ref long totalWork,
        Span<PresentationPassDescriptor> destination,
        in PresentationQualityProfile profile,
        PresentationPassKind kind,
        int iterations,
        long work)
    {
        destination[passCount++] = new PresentationPassDescriptor(
            kind,
            profile.MaskSize.X,
            profile.MaskSize.Y,
            iterations,
            work);
        totalWork = SaturatingAdd(totalWork, work);
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0 || right <= 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    private static long SaturatingAdd(long left, long right)
    {
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}
