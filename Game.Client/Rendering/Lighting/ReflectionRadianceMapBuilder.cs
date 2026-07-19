using Game.Client.Rendering.Effects;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

public readonly record struct LightingReflectionTelemetry(
    int SurfaceCount,
    int PixelsShaded,
    int RadianceSamples,
    bool WasBudgetClamped);

/// <summary>
/// Prepares a low-resolution reflection-radiance mask from the bounded
/// screen-space reflection surface plan. It augments the existing reflected
/// scene pass with light glints; it is not hardware ray tracing.
/// </summary>
internal static class ReflectionRadianceMapBuilder
{
    public static LightingReflectionTelemetry Build(
        Rectangle viewport,
        in PresentationQualityProfile profile,
        in LightingFrameParameters frame,
        ReadOnlySpan<WaterReflectionSurface> surfaces,
        ReadOnlySpan<float> lightRed,
        ReadOnlySpan<float> lightGreen,
        ReadOnlySpan<float> lightBlue,
        ReadOnlySpan<float> shadow,
        float reflectionStrength,
        Span<Color> destination)
    {
        var pixelCount = profile.MaskPixelCount;
        ValidateBuffer(lightRed, pixelCount, nameof(lightRed));
        ValidateBuffer(lightGreen, pixelCount, nameof(lightGreen));
        ValidateBuffer(lightBlue, pixelCount, nameof(lightBlue));
        ValidateBuffer(shadow, pixelCount, nameof(shadow));
        if (destination.Length < pixelCount)
        {
            throw new ArgumentException(
                "Reflection destination is smaller than the mask pixel count.",
                nameof(destination));
        }

        destination[..pixelCount].Clear();
        reflectionStrength = ClampFinite(reflectionStrength, 0f, 1f, 0f);
        if (pixelCount == 0 ||
            viewport.IsEmpty ||
            reflectionStrength <= 0.001f ||
            !profile.EnableReflections ||
            profile.Budget.MaxReflectionSurfaces <= 0 ||
            profile.Budget.MaxReflectionStripsPerSurface <= 0)
        {
            return default;
        }

        var surfaceCount = Math.Min(surfaces.Length, profile.Budget.MaxReflectionSurfaces);
        var daylight = TileRayCastShadowMaskBuilder.ResolveDaylightColor(frame.NormalizedTimeOfDay);
        var skyIllumination = TileRayCastShadowMaskBuilder.ResolveSkyIllumination(frame.NormalizedTimeOfDay);
        var pixelsShaded = 0;
        var radianceSamples = 0;
        for (var surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
        {
            ref readonly var surface = ref surfaces[surfaceIndex];
            var clipped = Rectangle.Intersect(surface.ScreenBounds, viewport);
            if (clipped.IsEmpty)
            {
                continue;
            }

            var minX = ScreenToMaskFloor(clipped.Left, viewport.X, viewport.Width, profile.MaskSize.X);
            var maxX = ScreenToMaskCeiling(clipped.Right, viewport.X, viewport.Width, profile.MaskSize.X) - 1;
            var minY = ScreenToMaskFloor(clipped.Top, viewport.Y, viewport.Height, profile.MaskSize.Y);
            var maxY = ScreenToMaskCeiling(clipped.Bottom, viewport.Y, viewport.Height, profile.MaskSize.Y) - 1;
            minX = Math.Clamp(minX, 0, profile.MaskSize.X - 1);
            maxX = Math.Clamp(maxX, 0, profile.MaskSize.X - 1);
            minY = Math.Clamp(minY, 0, profile.MaskSize.Y - 1);
            maxY = Math.Clamp(maxY, 0, profile.MaskSize.Y - 1);
            if (minX > maxX || minY > maxY)
            {
                continue;
            }

            var rows = maxY - minY + 1;
            var strips = Math.Min(profile.Budget.MaxReflectionStripsPerSurface, rows);
            var rowStep = Math.Max(1, DivideRoundUp(rows, strips));
            var surfaceTintRed = surface.Tint.R / 255f;
            var surfaceTintGreen = surface.Tint.G / 255f;
            var surfaceTintBlue = surface.Tint.B / 255f;
            var materialFactor = surface.Kind == ReflectionSurfaceKind.Water ? 1f : 0.58f;
            var strength = Math.Clamp(surface.Reflectivity, 0f, 1f) *
                reflectionStrength *
                materialFactor;

            for (var y = minY; y <= maxY; y++)
            {
                var strip = Math.Min(strips - 1, (y - minY) / rowStep);
                var sourceY = Math.Max(0, minY - 1 - strip * 2);
                var depthFade = 1f - strip / (float)Math.Max(1, strips) * 0.34f;
                for (var x = minX; x <= maxX; x++)
                {
                    var sourceIndex = sourceY * profile.MaskSize.X + x;
                    var destinationIndex = y * profile.MaskSize.X + x;
                    var skyVisibility = 1f - Math.Clamp(shadow[sourceIndex], 0f, 1f);
                    var skyRadiance = (0.045f + skyVisibility * 0.115f) * skyIllumination;
                    var ripple = ResolveStableRipple(surface.Phase, x, strip);
                    var scale = strength * depthFade * ripple;
                    var red = (lightRed[sourceIndex] * 0.68f + daylight.R / 255f * skyRadiance) *
                        surfaceTintRed * scale;
                    var green = (lightGreen[sourceIndex] * 0.68f + daylight.G / 255f * skyRadiance) *
                        surfaceTintGreen * scale;
                    var blue = (lightBlue[sourceIndex] * 0.68f + daylight.B / 255f * skyRadiance) *
                        surfaceTintBlue * scale;
                    destination[destinationIndex] = AdditiveColor(
                        destination[destinationIndex],
                        red,
                        green,
                        blue);
                    pixelsShaded++;
                    radianceSamples++;
                }
            }
        }

        return new LightingReflectionTelemetry(
            surfaceCount,
            pixelsShaded,
            radianceSamples,
            surfaces.Length > surfaceCount);
    }

    private static Color AdditiveColor(Color current, float red, float green, float blue)
    {
        return new Color(
            Math.Clamp(current.R / 255f + red, 0f, 1f),
            Math.Clamp(current.G / 255f + green, 0f, 1f),
            Math.Clamp(current.B / 255f + blue, 0f, 1f),
            1f);
    }

    private static float ResolveStableRipple(uint phase, int x, int strip)
    {
        var value = phase ^ unchecked((uint)x * 0x9E3779B9u) ^ unchecked((uint)strip * 0x85EBCA6Bu);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        return 0.84f + (value & 255u) / 255f * 0.16f;
    }

    private static int ScreenToMaskFloor(int value, int start, int length, int count)
    {
        if (length <= 0 || count <= 0)
        {
            return 0;
        }

        return SaturatingFloor((value - (double)start) * count / length);
    }

    private static int ScreenToMaskCeiling(int value, int start, int length, int count)
    {
        if (length <= 0 || count <= 0)
        {
            return 0;
        }

        var scaled = (value - (double)start) * count / length;
        return scaled >= int.MaxValue
            ? int.MaxValue
            : scaled <= int.MinValue
                ? int.MinValue
                : (int)Math.Ceiling(scaled);
    }

    private static int SaturatingFloor(double value)
    {
        return value >= int.MaxValue
            ? int.MaxValue
            : value <= int.MinValue
                ? int.MinValue
                : (int)Math.Floor(value);
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return value / divisor + (value % divisor == 0 ? 0 : 1);
    }

    private static float ClampFinite(float value, float minimum, float maximum, float fallback)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }

    private static void ValidateBuffer(ReadOnlySpan<float> buffer, int required, string name)
    {
        if (buffer.Length < required)
        {
            throw new ArgumentException("Lighting input buffer is too small.", name);
        }
    }
}
