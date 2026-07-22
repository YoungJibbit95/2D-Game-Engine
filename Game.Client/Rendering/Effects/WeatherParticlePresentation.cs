using Game.Core.Runtime;
using Game.Core.Weather;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public enum WeatherParticlePresentationKind : byte
{
    None,
    Rain,
    Storm,
    Fog,
    Snow,
    Blizzard
}

public readonly record struct WeatherDepthPrimitive(Rectangle Bounds, Color Color);

public static class WeatherParticlePresentation
{
    public const int MaximumDepthPrimitiveCount = 48;
    public const int MaximumPrimitiveWidth = 2;
    public const int MaximumPrimitiveHeight = 14;

    public static WeatherParticlePresentationKind Resolve(in LivingWorldFrameSnapshot livingWorld)
    {
        if (livingWorld.IsUnderground ||
            !float.IsFinite(livingWorld.WeatherIntensity) ||
            livingWorld.WeatherIntensity <= 0.001f)
        {
            return WeatherParticlePresentationKind.None;
        }

        // Fog belongs to the bounded atmosphere pass. It must never become precipitation
        // geometry or a full-screen veil in the weather particle path.
        if (livingWorld.Weather is WeatherKind.Snow or WeatherKind.Blizzard &&
            !livingWorld.AllowsFrozenPrecipitation)
        {
            return WeatherParticlePresentationKind.None;
        }

        return livingWorld.Weather switch
        {
            WeatherKind.Rain => WeatherParticlePresentationKind.Rain,
            WeatherKind.Storm => WeatherParticlePresentationKind.Storm,
            WeatherKind.Snow => WeatherParticlePresentationKind.Snow,
            WeatherKind.Blizzard => WeatherParticlePresentationKind.Blizzard,
            _ => WeatherParticlePresentationKind.None
        };
    }

    public static int ResolveDepthPrimitiveCount(
        WeatherParticlePresentationKind kind,
        float intensity)
    {
        intensity = ClampFinite(intensity, 0f, 1f);
        if (intensity <= 0.001f)
        {
            return 0;
        }

        var count = kind switch
        {
            WeatherParticlePresentationKind.Storm => 20 + (int)MathF.Ceiling(intensity * 12f),
            WeatherParticlePresentationKind.Rain => 12 + (int)MathF.Ceiling(intensity * 8f),
            WeatherParticlePresentationKind.Snow => 14 + (int)MathF.Ceiling(intensity * 14f),
            WeatherParticlePresentationKind.Blizzard => 28 + (int)MathF.Ceiling(intensity * 20f),
            _ => 0
        };
        return Math.Clamp(count, 0, MaximumDepthPrimitiveCount);
    }

    public static bool TryBuildDepthPrimitive(
        WeatherParticlePresentationKind kind,
        in Rectangle viewport,
        double totalSeconds,
        float wind,
        float intensity,
        int index,
        out WeatherDepthPrimitive primitive)
    {
        primitive = default;
        var count = ResolveDepthPrimitiveCount(kind, intensity);
        if (viewport.Width <= 0 ||
            viewport.Height <= 0 ||
            (uint)index >= (uint)count)
        {
            return false;
        }

        intensity = ClampFinite(intensity, 0f, 1f);
        wind = ClampFinite(wind, -1f, 1f);
        var seconds = double.IsFinite(totalSeconds) ? totalSeconds : 0d;
        var frameRate = kind is WeatherParticlePresentationKind.Snow or
            WeatherParticlePresentationKind.Blizzard
            ? 18
            : 24;
        var frame = SaturatingFloor(seconds * frameRate);
        Rectangle bounds;
        Color color;
        if (kind == WeatherParticlePresentationKind.Snow)
        {
            var phase = index * 0.73f + (float)seconds * 0.82f;
            var drift = SaturatingRound(MathF.Sin(phase) * (4f + MathF.Abs(wind) * 9f));
            var localX = PositiveModulo(index * 89L + frame * 2L, (long)viewport.Width + 32L) - 16;
            var localY = PositiveModulo(index * 47L + frame * 3L, (long)viewport.Height + 24L) - 12;
            var size = 1 + (int)(Hash(index, frame) & 1u);
            bounds = new Rectangle(
                SaturatingAdd(viewport.X, SaturatingAdd(localX, drift)),
                SaturatingAdd(viewport.Y, localY),
                size,
                size);
            color = PremultiplyAlpha(
                new Color(232, 241, 248),
                intensity * (0.38f + (Hash(index + 17, frame) & 7u) / 70f));
        }
        else if (kind == WeatherParticlePresentationKind.Blizzard)
        {
            var localX = PositiveModulo(index * 101L + frame * 9L, (long)viewport.Width + 48L) - 24;
            var localY = PositiveModulo(index * 61L + frame * 13L, (long)viewport.Height + 32L) - 16;
            var windOffset = SaturatingRound(wind * 18f);
            var height = 4 + (int)(Hash(index + 31, frame) & 3u);
            bounds = new Rectangle(
                SaturatingAdd(viewport.X, SaturatingAdd(localX, windOffset)),
                SaturatingAdd(viewport.Y, localY),
                1 + (int)(Hash(index + 43, frame) & 1u),
                height);
            color = PremultiplyAlpha(
                new Color(224, 238, 248),
                intensity * (0.44f + (Hash(index + 59, frame) & 7u) / 64f));
        }
        else
        {
            var storm = kind == WeatherParticlePresentationKind.Storm;
            const int margin = 24;
            var localX = PositiveModulo(index * 97L + frame * 5L, (long)viewport.Width + margin * 2L) - margin;
            var localY = PositiveModulo(index * 53L + frame * 11L, (long)viewport.Height + 64L) - 64;
            var windOffset = SaturatingRound(wind * (storm ? 10f : 6f));
            bounds = new Rectangle(
                SaturatingAdd(viewport.X, SaturatingAdd(localX, windOffset)),
                SaturatingAdd(viewport.Y, localY),
                1,
                storm ? 14 : 9);
            color = PremultiplyAlpha(
                new Color(154, 192, 215),
                intensity * (storm ? 0.42f : 0.32f));
        }

        if (bounds.Width > MaximumPrimitiveWidth ||
            bounds.Height > MaximumPrimitiveHeight ||
            !TryClipToViewport(bounds, viewport, out var clipped))
        {
            return false;
        }

        primitive = new WeatherDepthPrimitive(clipped, color);
        return color.A > 0;
    }

    public static Color PremultiplyAlpha(Color opaqueColor, float opacity)
    {
        opacity = ClampFinite(opacity, 0f, 1f);
        return new Color(opaqueColor.R, opaqueColor.G, opaqueColor.B) * opacity;
    }

    public static bool TryClipToViewport(
        in Rectangle bounds,
        in Rectangle viewport,
        out Rectangle clipped)
    {
        clipped = Rectangle.Intersect(bounds, viewport);
        return clipped.Width > 0 && clipped.Height > 0;
    }

    private static uint Hash(int index, long frame)
    {
        var value = unchecked((uint)index * 0x9E3779B9u) ^
            unchecked((uint)frame) ^
            unchecked((uint)(frame >> 32));
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        return value ^ (value >> 16);
    }

    private static int PositiveModulo(long value, long modulo)
    {
        var safeModulo = Math.Max(1L, modulo);
        var remainder = value % safeModulo;
        return SaturateToInt(remainder < 0 ? remainder + safeModulo : remainder);
    }

    private static long SaturatingFloor(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0L;
        }

        return value <= long.MinValue
            ? long.MinValue
            : value >= long.MaxValue
                ? long.MaxValue
                : (long)Math.Floor(value);
    }

    private static int SaturatingRound(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0;
        }

        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)MathF.Round(value);
    }

    private static int SaturatingAdd(int left, int right)
    {
        return SaturateToInt((long)left + right);
    }

    private static int SaturateToInt(long value)
    {
        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)value;
    }

    private static float ClampFinite(float value, float minimum, float maximum)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
    }
}