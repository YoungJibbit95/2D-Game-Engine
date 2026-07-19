using Game.Core.Runtime;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Effects;

public enum ParallaxDetailKind : byte
{
    HazeBand,
    CloudWisp,
    Star,
    CaveStrata,
    AmbientMote
}

public enum ParallaxDetailDepth : byte
{
    Backdrop,
    Overlay
}

public readonly record struct ParallaxDetailCommand(
    ParallaxDetailKind Kind,
    ParallaxDetailDepth Depth,
    Rectangle Bounds,
    Color Color,
    uint Variation);

public readonly record struct ParallaxDetailBudget(
    int MaximumCommands,
    int HazeBands,
    int CloudWisps,
    int Stars,
    int CaveDetails)
{
    public static ParallaxDetailBudget ForQuality(int quality)
    {
        return quality switch
        {
            <= 0 => default,
            1 => new ParallaxDetailBudget(24, 2, 4, 8, 10),
            2 => new ParallaxDetailBudget(64, 4, 10, 24, 26),
            _ => new ParallaxDetailBudget(128, 6, 18, 52, 52)
        };
    }
}

public static class ParallaxDetailPlanner
{
    public const int MaximumCommandCount = 128;

    public static int Build(
        in LivingWorldFrameSnapshot livingWorld,
        in ParallaxSceneProfile scene,
        bool isNight,
        in Rectangle viewport,
        float cameraX,
        double animationSeconds,
        int quality,
        Span<ParallaxDetailCommand> destination)
    {
        if (destination.Length < MaximumCommandCount)
        {
            throw new ArgumentException(
                $"Parallax detail destination must contain at least {MaximumCommandCount} entries.",
                nameof(destination));
        }

        var budget = ParallaxDetailBudget.ForQuality(quality);
        if (budget.MaximumCommands == 0 || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return 0;
        }

        var seed = BuildSeed(livingWorld);
        var surfaceVisibility = 1f - Math.Clamp(scene.UndergroundBlend, 0f, 1f);
        var count = 0;
        if (!scene.AuthoredPanoramaActive)
        {
            AddHazeBands(scene, viewport, surfaceVisibility, budget, seed, destination, ref count);
        }

        if (surfaceVisibility > 0.02f)
        {
            if (!scene.AuthoredPanoramaActive)
            {
                AddCloudWisps(
                    livingWorld,
                    viewport,
                    cameraX,
                    animationSeconds,
                    surfaceVisibility,
                    budget,
                    seed,
                    destination,
                    ref count);
            }

            if (isNight)
            {
                AddStars(
                    viewport,
                    animationSeconds,
                    surfaceVisibility,
                    budget,
                    seed,
                    destination,
                    ref count);
            }
        }

        if (scene.UndergroundBlend > 0.02f)
        {
            AddCaveDetails(
                livingWorld,
                scene,
                viewport,
                cameraX,
                animationSeconds,
                budget,
                seed,
                destination,
                ref count);
        }

        return count;
    }

    private static void AddHazeBands(
        in ParallaxSceneProfile scene,
        in Rectangle viewport,
        float surfaceVisibility,
        in ParallaxDetailBudget budget,
        uint seed,
        Span<ParallaxDetailCommand> destination,
        ref int count)
    {
        var bandHeight = Math.Max(2, viewport.Height / 36);
        var baseY = viewport.Top + viewport.Height * 7 / 16;
        for (var index = 0; index < budget.HazeBands && count < budget.MaximumCommands; index++)
        {
            var hash = Hash(seed, index, 0x68E31DA4u);
            var y = baseY + index * Math.Max(3, viewport.Height / 24) +
                SignedRange(hash, Math.Max(1, viewport.Height / 80));
            var opacity = (byte)Math.Clamp(
                10 + index * 2 + (int)MathF.Round(surfaceVisibility * 12f),
                0,
                byte.MaxValue);
            var color = Color.Lerp(scene.SkyMiddle, scene.SkyBottom, index / (float)Math.Max(1, budget.HazeBands));
            color.A = opacity;
            destination[count++] = new ParallaxDetailCommand(
                ParallaxDetailKind.HazeBand,
                ParallaxDetailDepth.Backdrop,
                new Rectangle(viewport.Left, y, viewport.Width, bandHeight + index),
                color,
                hash);
        }
    }

    private static void AddCloudWisps(
        in LivingWorldFrameSnapshot livingWorld,
        in Rectangle viewport,
        float cameraX,
        double animationSeconds,
        float surfaceVisibility,
        in ParallaxDetailBudget budget,
        uint seed,
        Span<ParallaxDetailCommand> destination,
        ref int count)
    {
        var cloudFactor = Math.Clamp(0.2f + livingWorld.CloudCover, 0f, 1.2f);
        var desired = Math.Clamp(
            (int)MathF.Ceiling(budget.CloudWisps * cloudFactor),
            1,
            budget.CloudWisps);
        var wind = float.IsFinite(livingWorld.Wind) ? Math.Clamp(livingWorld.Wind, -1f, 1f) : 0f;
        var travelWidth = Math.Max(1, viewport.Width + viewport.Width / 3);
        for (var index = 0; index < desired && count < budget.MaximumCommands; index++)
        {
            var hash = Hash(seed, index, 0xA511E9B3u);
            var width = Math.Clamp(viewport.Width / 9 + (int)(hash % (uint)Math.Max(1, viewport.Width / 7)), 24, viewport.Width);
            var height = Math.Clamp(viewport.Height / 90 + (int)((hash >> 8) % (uint)Math.Max(2, viewport.Height / 38)), 3, 42);
            var baseX = (int)(hash % (uint)travelWidth);
            var drift = animationSeconds * (5d + Math.Abs(wind) * 14d) * (wind < 0f ? -1d : 1d);
            var parallax = float.IsFinite(cameraX) ? cameraX * 0.015f : 0f;
            var x = viewport.Left - width / 2 + PositiveModulo(
                SaturatingRound(baseX + drift - parallax),
                travelWidth);
            var y = viewport.Top + viewport.Height / 12 +
                (int)((hash >> 16) % (uint)Math.Max(1, viewport.Height * 3 / 10));
            var alpha = (byte)Math.Clamp(
                (int)MathF.Round((18f + livingWorld.CloudCover * 30f) * surfaceVisibility),
                0,
                72);
            destination[count++] = new ParallaxDetailCommand(
                ParallaxDetailKind.CloudWisp,
                ParallaxDetailDepth.Backdrop,
                new Rectangle(x, y, width, height),
                new Color((byte)218, (byte)226, (byte)229, alpha),
                hash);
        }
    }

    private static void AddStars(
        in Rectangle viewport,
        double animationSeconds,
        float surfaceVisibility,
        in ParallaxDetailBudget budget,
        uint seed,
        Span<ParallaxDetailCommand> destination,
        ref int count)
    {
        for (var index = 0; index < budget.Stars && count < budget.MaximumCommands; index++)
        {
            var hash = Hash(seed, index, 0xB5297A4Du);
            var x = viewport.Left + (int)(hash % (uint)Math.Max(1, viewport.Width));
            var y = viewport.Top + (int)((hash >> 12) % (uint)Math.Max(1, viewport.Height * 5 / 9));
            var phase = animationSeconds * (1.2d + (hash & 7u) * 0.11d) + index * 0.73d;
            var twinkle = 0.55f + MathF.Sin((float)phase) * 0.35f;
            var alpha = (byte)Math.Clamp(
                (int)MathF.Round(150f * twinkle * surfaceVisibility),
                0,
                180);
            var size = (hash & 15u) == 0u ? 2 : 1;
            destination[count++] = new ParallaxDetailCommand(
                ParallaxDetailKind.Star,
                ParallaxDetailDepth.Overlay,
                new Rectangle(x, y, size, size),
                new Color((byte)224, (byte)235, byte.MaxValue, alpha),
                hash);
        }
    }

    private static void AddCaveDetails(
        in LivingWorldFrameSnapshot livingWorld,
        in ParallaxSceneProfile scene,
        in Rectangle viewport,
        float cameraX,
        double animationSeconds,
        in ParallaxDetailBudget budget,
        uint seed,
        Span<ParallaxDetailCommand> destination,
        ref int count)
    {
        var caveCount = Math.Min(budget.CaveDetails, budget.MaximumCommands - count);
        var identity = livingWorld.SubBiomeId ?? livingWorld.CaveProfileId;
        var isCrystal = Contains(identity, "crystal");
        var isMushroom = Contains(identity, "mushroom");
        var strataCount = Math.Min(Math.Max(2, caveCount / 5), 10);
        for (var index = 0; index < strataCount && count < budget.MaximumCommands; index++)
        {
            var hash = Hash(seed, index, 0x94D049BBu);
            var parallax = float.IsFinite(cameraX) ? cameraX * (0.01f + index * 0.002f) : 0f;
            var offset = PositiveModulo(SaturatingRound(parallax), Math.Max(1, viewport.Height / 7));
            var y = viewport.Top + viewport.Height / 5 + index * Math.Max(3, viewport.Height / 11) - offset;
            var alpha = (byte)Math.Clamp(
                8 + (int)MathF.Round(scene.UndergroundBlend * 18f),
                0,
                36);
            destination[count++] = new ParallaxDetailCommand(
                ParallaxDetailKind.CaveStrata,
                ParallaxDetailDepth.Backdrop,
                new Rectangle(viewport.Left, y, viewport.Width, 1 + (int)(hash & 1u)),
                isCrystal
                    ? new Color((byte)109, (byte)196, (byte)221, alpha)
                    : isMushroom
                        ? new Color((byte)184, (byte)119, (byte)201, alpha)
                        : new Color((byte)139, (byte)129, (byte)153, alpha),
                hash);
        }

        for (var index = strataCount; index < caveCount && count < budget.MaximumCommands; index++)
        {
            var hash = Hash(seed, index, 0xD1B54A35u);
            var x = viewport.Left + (int)(hash % (uint)Math.Max(1, viewport.Width));
            var baseY = viewport.Top + (int)((hash >> 12) % (uint)Math.Max(1, viewport.Height));
            var phase = animationSeconds * (0.35d + (hash & 3u) * 0.09d) + index * 0.31d;
            var y = baseY + SaturatingRound(Math.Sin(phase) * (4d + (hash & 7u)));
            var pulse = 0.65f + MathF.Sin((float)(phase * 1.7d)) * 0.25f;
            var alpha = (byte)Math.Clamp(
                (int)MathF.Round(scene.UndergroundBlend * pulse * 110f),
                0,
                128);
            var size = isCrystal && (hash & 7u) == 0u ? 2 : 1;
            destination[count++] = new ParallaxDetailCommand(
                ParallaxDetailKind.AmbientMote,
                ParallaxDetailDepth.Overlay,
                new Rectangle(x, y, size, size),
                isCrystal
                    ? new Color((byte)112, (byte)218, (byte)243, alpha)
                    : isMushroom
                        ? new Color((byte)211, (byte)143, (byte)225, alpha)
                        : new Color((byte)185, (byte)177, (byte)194, alpha),
                hash);
        }
    }

    private static uint BuildSeed(in LivingWorldFrameSnapshot livingWorld)
    {
        var region = unchecked((ulong)livingWorld.RegionIndex);
        var seed = unchecked((uint)region ^ (uint)(region >> 32));
        seed ^= StableHash(livingWorld.BiomeId);
        seed = RotateLeft(seed, 11) ^ StableHash(livingWorld.SubBiomeId);
        seed = RotateLeft(seed, 7) ^ StableHash(livingWorld.CaveProfileId);
        return seed == 0u ? 0x59E3A1C7u : seed;
    }

    private static uint StableHash(string? value)
    {
        var hash = 2166136261u;
        if (value is null)
        {
            return hash;
        }

        for (var index = 0; index < value.Length; index++)
        {
            hash = unchecked((hash ^ char.ToUpperInvariant(value[index])) * 16777619u);
        }

        return hash;
    }

    private static uint Hash(uint seed, int index, uint salt)
    {
        var value = unchecked(seed ^ (uint)index * 0x9E3779B9u ^ salt);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        return value ^ (value >> 16);
    }

    private static int SignedRange(uint value, int amplitude)
    {
        return amplitude <= 0
            ? 0
            : (int)(value % (uint)(amplitude * 2 + 1)) - amplitude;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static int SaturatingRound(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)Math.Round(value);
    }

    private static uint RotateLeft(uint value, int amount)
    {
        return value << amount | value >> (32 - amount);
    }

    private static bool Contains(string? value, string term)
    {
        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    }
}
