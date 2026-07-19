using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public readonly record struct BackdropBlurPlan(
    Point TargetSize,
    int DownsampleDivisor,
    int Iterations,
    int RadiusPerIteration,
    long LegacySampleCount,
    long PlannedSampleCount)
{
    public bool IsEnabled => TargetSize.X > 0 && TargetSize.Y > 0 && Iterations > 0;

    public long SavedSampleCount => Math.Max(0L, LegacySampleCount - PlannedSampleCount);
}

public static class BackdropBlurPlanner
{
    public const int LegacyFullscreenTapCount = 8;
    public const int KawaseTapCount = 4;

    public static BackdropBlurPlan Build(
        PresentationQualityTier quality,
        Rectangle viewport,
        int radiusPixels)
    {
        if (quality == PresentationQualityTier.Disabled ||
            viewport.Width <= 0 ||
            viewport.Height <= 0 ||
            radiusPixels <= 0)
        {
            return default;
        }

        var divisor = quality switch
        {
            PresentationQualityTier.High => 2,
            PresentationQualityTier.Medium => 3,
            _ => 4
        };
        var iterations = quality == PresentationQualityTier.High && radiusPixels >= 6 ? 2 : 1;
        var width = DivideRoundUp(viewport.Width, divisor);
        var height = DivideRoundUp(viewport.Height, divisor);
        var radiusPerIteration = Math.Max(1, DivideRoundUp(Math.Clamp(radiusPixels, 1, 24), divisor * iterations));
        var targetPixels = SaturatingMultiply(width, height);
        var viewportPixels = SaturatingMultiply(viewport.Width, viewport.Height);
        var plannedSamples = SaturatingAdd(
            viewportPixels,
            SaturatingMultiply(targetPixels, (long)iterations * KawaseTapCount));

        return new BackdropBlurPlan(
            new Point(width, height),
            divisor,
            iterations,
            radiusPerIteration,
            SaturatingMultiply(viewportPixels, LegacyFullscreenTapCount),
            plannedSamples);
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return value / divisor + (value % divisor == 0 ? 0 : 1);
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
