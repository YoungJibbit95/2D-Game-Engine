using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

public readonly record struct LightingTemporalStabilizationTelemetry(
    int ReprojectedPixels,
    int DisocclusionRejectedPixels,
    bool HistoryRejected);

/// <summary>
/// Reprojects the previous low-resolution light field through world space.
/// Large changes are treated as disocclusions so mined tiles and moving lights
/// update immediately instead of leaving a temporal trail.
/// </summary>
internal static class LightingTemporalStabilizer
{
    private const float ShadowHistoryWeight = 0.58f;
    private const float LightHistoryWeight = 0.32f;
    private const float ShadowDisocclusionThreshold = 0.24f;
    private const float LightDisocclusionThreshold = 0.34f;

    public static LightingTemporalStabilizationTelemetry Apply(
        Rectangle previousVisibleWorld,
        Rectangle currentVisibleWorld,
        Point maskSize,
        ReadOnlySpan<float> previousShadow,
        ReadOnlySpan<float> previousRed,
        ReadOnlySpan<float> previousGreen,
        ReadOnlySpan<float> previousBlue,
        Span<float> currentShadow,
        Span<float> currentRed,
        Span<float> currentGreen,
        Span<float> currentBlue)
    {
        if (maskSize.X < 0 || maskSize.Y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maskSize));
        }

        var pixelCount = checked(maskSize.X * maskSize.Y);
        Validate(previousShadow, pixelCount, nameof(previousShadow));
        Validate(previousRed, pixelCount, nameof(previousRed));
        Validate(previousGreen, pixelCount, nameof(previousGreen));
        Validate(previousBlue, pixelCount, nameof(previousBlue));
        Validate(currentShadow, pixelCount, nameof(currentShadow));
        Validate(currentRed, pixelCount, nameof(currentRed));
        Validate(currentGreen, pixelCount, nameof(currentGreen));
        Validate(currentBlue, pixelCount, nameof(currentBlue));

        if (pixelCount == 0 ||
            previousVisibleWorld.IsEmpty ||
            currentVisibleWorld.IsEmpty ||
            Rectangle.Intersect(previousVisibleWorld, currentVisibleWorld).IsEmpty)
        {
            return new LightingTemporalStabilizationTelemetry(0, 0, true);
        }

        var reprojected = 0;
        var rejected = 0;
        for (var y = 0; y < maskSize.Y; y++)
        {
            var worldY = currentVisibleWorld.Y +
                (y + 0.5d) * currentVisibleWorld.Height / maskSize.Y;
            var previousY = SaturatingFloor(
                (worldY - previousVisibleWorld.Y) * maskSize.Y / previousVisibleWorld.Height);
            if ((uint)previousY >= (uint)maskSize.Y)
            {
                continue;
            }

            for (var x = 0; x < maskSize.X; x++)
            {
                var worldX = currentVisibleWorld.X +
                    (x + 0.5d) * currentVisibleWorld.Width / maskSize.X;
                var previousX = SaturatingFloor(
                    (worldX - previousVisibleWorld.X) * maskSize.X / previousVisibleWorld.Width);
                if ((uint)previousX >= (uint)maskSize.X)
                {
                    continue;
                }

                var currentIndex = y * maskSize.X + x;
                var previousIndex = previousY * maskSize.X + previousX;
                var shadowDelta = MathF.Abs(currentShadow[currentIndex] - previousShadow[previousIndex]);
                var redDelta = MathF.Abs(currentRed[currentIndex] - previousRed[previousIndex]);
                var greenDelta = MathF.Abs(currentGreen[currentIndex] - previousGreen[previousIndex]);
                var blueDelta = MathF.Abs(currentBlue[currentIndex] - previousBlue[previousIndex]);
                if (!float.IsFinite(shadowDelta + redDelta + greenDelta + blueDelta))
                {
                    rejected++;
                    continue;
                }

                if (shadowDelta <= ShadowDisocclusionThreshold)
                {
                    currentShadow[currentIndex] = MathHelper.Lerp(
                        currentShadow[currentIndex],
                        previousShadow[previousIndex],
                        ShadowHistoryWeight);
                }
                else
                {
                    rejected++;
                }

                if (Math.Max(redDelta, Math.Max(greenDelta, blueDelta)) <= LightDisocclusionThreshold)
                {
                    currentRed[currentIndex] = MathHelper.Lerp(currentRed[currentIndex], previousRed[previousIndex], LightHistoryWeight);
                    currentGreen[currentIndex] = MathHelper.Lerp(currentGreen[currentIndex], previousGreen[previousIndex], LightHistoryWeight);
                    currentBlue[currentIndex] = MathHelper.Lerp(currentBlue[currentIndex], previousBlue[previousIndex], LightHistoryWeight);
                }

                reprojected++;
            }
        }

        return new LightingTemporalStabilizationTelemetry(reprojected, rejected, false);
    }

    private static int SaturatingFloor(double value)
    {
        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)Math.Floor(value);
    }

    private static void Validate(ReadOnlySpan<float> buffer, int required, string name)
    {
        if (buffer.Length < required)
        {
            throw new ArgumentException(
                $"Lighting buffer '{name}' requires at least {required} values but contains {buffer.Length}.",
                name);
        }
    }
}
