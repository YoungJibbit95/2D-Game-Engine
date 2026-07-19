using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public enum ParallaxCompositionCommandKind : byte
{
    Repeat,
    Landmark
}

public readonly record struct ParallaxCompositionCommand(
    ParallaxCompositionCommandKind Kind,
    Rectangle Bounds,
    long CellIndex,
    bool UseAlternateSprite,
    bool FlipHorizontally,
    ParallaxLandmarkStyle LandmarkStyle);

public static class ParallaxCompositionPlanner
{
    public const int MaximumRepeatCommandCount = 48;
    public const int MaximumLandmarkCommandCount = 4;
    public const int MaximumCommandCount = MaximumRepeatCommandCount + MaximumLandmarkCommandCount;

    public static int Build(
        in ParallaxLayerDescriptor layer,
        float cameraX,
        Rectangle viewport,
        int repeatWidth,
        int repeatHeight,
        int baseY,
        Span<ParallaxCompositionCommand> destination)
    {
        if (destination.Length < MaximumCommandCount)
        {
            throw new ArgumentException(
                $"Parallax command destination must contain at least {MaximumCommandCount} entries.",
                nameof(destination));
        }

        if (repeatWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(repeatWidth));
        }

        if (repeatHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(repeatHeight));
        }

        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return 0;
        }

        var overlap = Math.Clamp((int)MathF.Round(repeatWidth * layer.RepeatOverlap), 0, repeatWidth - 1);
        var stride = Math.Max(1, repeatWidth - overlap);
        var phase = layer.PreserveAuthoredRepeat
            ? 0
            : (int)(((ulong)layer.VariationSeed * (uint)stride) >> 32);
        var scroll = SaturatingRoundToLong((double)cameraX * layer.HorizontalParallax);
        var firstCell = FloorDivide((long)viewport.Left + scroll - phase, stride) - 1;
        var paddedLeft = (long)viewport.Left - repeatWidth;
        var paddedRight = (long)viewport.Right + repeatWidth;
        var commandCount = 0;
        var repeatCount = 0;
        var landmarkCount = 0;

        for (var offset = 0; offset < MaximumRepeatCommandCount + 2; offset++)
        {
            var cell = SaturatingAdd(firstCell, offset);
            var cellX = SaturatingAdd(SaturatingMultiply(cell, stride), phase);
            var x = SaturatingAdd(cellX, -scroll);
            if (x >= paddedRight)
            {
                break;
            }

            if (SaturatingAdd(x, repeatWidth) <= paddedLeft)
            {
                continue;
            }

            var hash = HashCell(layer.VariationSeed, cell);
            var jitter = layer.PreserveAuthoredRepeat
                ? 0
                : ResolveSignedJitter(hash >> 8, layer.VerticalJitter);
            var bounds = new Rectangle(
                SaturatingToInt(x),
                SaturatingAdd(baseY, jitter),
                repeatWidth,
                repeatHeight);
            var alternate = !layer.PreserveAuthoredRepeat &&
                !string.IsNullOrWhiteSpace(layer.AlternateSpriteId) &&
                ((cell ^ layer.VariationSeed) & 1L) != 0;
            destination[commandCount++] = new ParallaxCompositionCommand(
                ParallaxCompositionCommandKind.Repeat,
                bounds,
                cell,
                alternate,
                !layer.PreserveAuthoredRepeat && (hash & 2u) != 0,
                ParallaxLandmarkStyle.None);
            repeatCount++;

            if (landmarkCount >= MaximumLandmarkCommandCount ||
                layer.LandmarkStyle == ParallaxLandmarkStyle.None ||
                layer.LandmarkPeriod <= 0 ||
                PositiveModulo(cell + layer.VariationSeed, layer.LandmarkPeriod) != 0)
            {
                if (repeatCount >= MaximumRepeatCommandCount)
                {
                    break;
                }

                continue;
            }

            var landmarkBounds = BuildLandmarkBounds(layer.LandmarkStyle, bounds, hash);
            if (landmarkBounds.Right > viewport.Left &&
                landmarkBounds.Left < viewport.Right &&
                landmarkBounds.Bottom > viewport.Top &&
                landmarkBounds.Top < viewport.Bottom)
            {
                destination[commandCount++] = new ParallaxCompositionCommand(
                    ParallaxCompositionCommandKind.Landmark,
                    landmarkBounds,
                    cell,
                    false,
                    (hash & 4u) != 0,
                    layer.LandmarkStyle);
                landmarkCount++;
            }

            if (repeatCount >= MaximumRepeatCommandCount)
            {
                break;
            }
        }

        return commandCount;
    }

    private static Rectangle BuildLandmarkBounds(
        ParallaxLandmarkStyle style,
        in Rectangle repeatBounds,
        uint hash)
    {
        var widthFactor = style switch
        {
            ParallaxLandmarkStyle.CaveSpire => 0.2f,
            ParallaxLandmarkStyle.CrystalCluster => 0.24f,
            ParallaxLandmarkStyle.MushroomColony => 0.3f,
            ParallaxLandmarkStyle.Mangrove => 0.32f,
            ParallaxLandmarkStyle.AmberWorkshop => 0.36f,
            _ => 0.28f
        };
        var heightFactor = style switch
        {
            ParallaxLandmarkStyle.CaveSpire => 0.62f,
            ParallaxLandmarkStyle.CrystalCluster => 0.48f,
            ParallaxLandmarkStyle.MushroomColony => 0.38f,
            ParallaxLandmarkStyle.Mangrove => 0.72f,
            ParallaxLandmarkStyle.AmberWorkshop => 0.46f,
            _ => 0.54f
        };
        var width = Math.Clamp((int)MathF.Round(repeatBounds.Width * widthFactor), 12, repeatBounds.Width);
        var height = Math.Clamp((int)MathF.Round(repeatBounds.Height * heightFactor), 12, repeatBounds.Height);
        var travel = Math.Max(1, repeatBounds.Width - width);
        var localX = (int)((hash >> 16) % (uint)travel);
        return new Rectangle(
            SaturatingAdd(repeatBounds.X, localX),
            SaturatingAdd(repeatBounds.Bottom, -height),
            width,
            height);
    }

    private static uint HashCell(uint seed, long cell)
    {
        var value = unchecked(seed ^ (uint)cell ^ (uint)((ulong)cell >> 32));
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        return value ^ (value >> 16);
    }

    private static int ResolveSignedJitter(uint value, int amplitude)
    {
        if (amplitude <= 0)
        {
            return 0;
        }

        return (int)(value % (uint)(amplitude * 2 + 1)) - amplitude;
    }

    private static long FloorDivide(long value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }

    private static long PositiveModulo(long value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private static long SaturatingRoundToLong(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return value <= long.MinValue
            ? long.MinValue
            : value >= long.MaxValue
                ? long.MaxValue
                : (long)Math.Round(value);
    }

    private static long SaturatingMultiply(long value, int multiplier)
    {
        if (value == 0 || multiplier == 0)
        {
            return 0;
        }

        if (value > 0 && value > long.MaxValue / multiplier)
        {
            return long.MaxValue;
        }

        if (value < 0 && value < long.MinValue / multiplier)
        {
            return long.MinValue;
        }

        return value * multiplier;
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            return long.MaxValue;
        }

        if (right < 0 && left < long.MinValue - right)
        {
            return long.MinValue;
        }

        return left + right;
    }

    private static int SaturatingAdd(int left, int right)
    {
        var result = (long)left + right;
        return SaturatingToInt(result);
    }

    private static int SaturatingToInt(long value)
    {
        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)value;
    }
}
