using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public enum PresentationQualityTier
{
    Disabled = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public readonly record struct PresentationBudget(
    int MaxMaskPixels,
    int MaxPointLights,
    int MaxRayStepsPerSample,
    int MaxPenumbraRadius,
    int MaxBloomRadius,
    int MaxReflectionSurfaces,
    int MaxReflectionStripsPerSurface,
    int MaxSceneCapturePixels,
    int MaxParticles)
{
    public static PresentationBudget ForTier(PresentationQualityTier tier)
    {
        return tier switch
        {
            PresentationQualityTier.Low => new PresentationBudget(
                MaxMaskPixels: 4_096,
                MaxPointLights: 2,
                MaxRayStepsPerSample: 0,
                MaxPenumbraRadius: 0,
                MaxBloomRadius: 0,
                MaxReflectionSurfaces: 8,
                MaxReflectionStripsPerSurface: 1,
                MaxSceneCapturePixels: 1_048_576,
                MaxParticles: 128),
            PresentationQualityTier.Medium => new PresentationBudget(
                MaxMaskPixels: 8_192,
                MaxPointLights: 6,
                MaxRayStepsPerSample: 20,
                MaxPenumbraRadius: 1,
                MaxBloomRadius: 1,
                MaxReflectionSurfaces: 24,
                MaxReflectionStripsPerSurface: 2,
                MaxSceneCapturePixels: 4_194_304,
                MaxParticles: 320),
            PresentationQualityTier.High => new PresentationBudget(
                MaxMaskPixels: 16_384,
                MaxPointLights: 12,
                MaxRayStepsPerSample: 36,
                MaxPenumbraRadius: 2,
                MaxBloomRadius: 3,
                MaxReflectionSurfaces: 48,
                MaxReflectionStripsPerSurface: 4,
                MaxSceneCapturePixels: 9_437_184,
                MaxParticles: 640),
            _ => default
        };
    }

    public PresentationBudget ClampTo(PresentationBudget maximum)
    {
        return new PresentationBudget(
            Math.Clamp(MaxMaskPixels, 0, maximum.MaxMaskPixels),
            Math.Clamp(MaxPointLights, 0, maximum.MaxPointLights),
            Math.Clamp(MaxRayStepsPerSample, 0, maximum.MaxRayStepsPerSample),
            Math.Clamp(MaxPenumbraRadius, 0, maximum.MaxPenumbraRadius),
            Math.Clamp(MaxBloomRadius, 0, maximum.MaxBloomRadius),
            Math.Clamp(MaxReflectionSurfaces, 0, maximum.MaxReflectionSurfaces),
            Math.Clamp(MaxReflectionStripsPerSurface, 0, maximum.MaxReflectionStripsPerSurface),
            Math.Clamp(MaxSceneCapturePixels, 0, maximum.MaxSceneCapturePixels),
            Math.Clamp(MaxParticles, 0, maximum.MaxParticles));
    }
}

public readonly record struct PresentationQualityProfile(
    PresentationQualityTier Tier,
    PresentationBudget Budget,
    Point MaskSize,
    int AmbientOcclusionRadius,
    bool CastSunShadows,
    bool CastPointLightShadows,
    bool EnableBloom,
    bool EnableReflections)
{
    public int MaskPixelCount => MaskSize.X * MaskSize.Y;

    public static PresentationQualityProfile Create(
        PresentationQualityTier tier,
        Rectangle viewport,
        PresentationBudget? requestedBudget = null)
    {
        if (viewport.Width < 0 || viewport.Height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewport));
        }

        var tierBudget = PresentationBudget.ForTier(tier);
        var budget = requestedBudget?.ClampTo(tierBudget) ?? tierBudget;
        if (tier == PresentationQualityTier.Disabled ||
            viewport.Width == 0 ||
            viewport.Height == 0 ||
            budget.MaxMaskPixels == 0)
        {
            return new PresentationQualityProfile(
                PresentationQualityTier.Disabled,
                default,
                Point.Zero,
                0,
                false,
                false,
                false,
                false);
        }

        var divisor = tier switch
        {
            PresentationQualityTier.Low => 16,
            PresentationQualityTier.Medium => 12,
            _ => 8
        };
        var desiredWidth = Math.Max(1, DivideRoundUp(viewport.Width, divisor));
        var desiredHeight = Math.Max(1, DivideRoundUp(viewport.Height, divisor));
        var size = ClampArea(desiredWidth, desiredHeight, budget.MaxMaskPixels);
        var hasRayBudget = budget.MaxRayStepsPerSample > 0;

        var captureArea = (long)viewport.Width * viewport.Height;
        return new PresentationQualityProfile(
            tier,
            budget,
            size,
            AmbientOcclusionRadius: tier == PresentationQualityTier.High ? 2 : 1,
            CastSunShadows: hasRayBudget,
            CastPointLightShadows: hasRayBudget && tier == PresentationQualityTier.High,
            EnableBloom: budget.MaxBloomRadius > 0,
            EnableReflections: budget.MaxReflectionSurfaces > 0 &&
                captureArea <= budget.MaxSceneCapturePixels);
    }

    private static Point ClampArea(int width, int height, int maxPixels)
    {
        var area = (long)width * height;
        if (area <= maxPixels)
        {
            return new Point(width, height);
        }

        var scale = Math.Sqrt(maxPixels / (double)area);
        var scaledWidth = Math.Max(1, (int)Math.Floor(width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Floor(height * scale));
        while ((long)scaledWidth * scaledHeight > maxPixels)
        {
            if (scaledWidth >= scaledHeight && scaledWidth > 1)
            {
                scaledWidth--;
            }
            else if (scaledHeight > 1)
            {
                scaledHeight--;
            }
            else
            {
                break;
            }
        }

        return new Point(scaledWidth, scaledHeight);
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return value / divisor + (value % divisor == 0 ? 0 : 1);
    }
}
