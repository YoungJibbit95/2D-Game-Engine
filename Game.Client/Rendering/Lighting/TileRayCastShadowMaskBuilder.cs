using Game.Client.Rendering.Effects;
using Game.Core;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

internal static class TileRayCastShadowMaskBuilder
{
    private const float MinimumLight = 0.001f;

    public static LightingBuildTelemetry Build(
        World world,
        Rectangle visibleWorld,
        in PresentationQualityProfile profile,
        in LightingFrameParameters frame,
        ReadOnlySpan<ScreenSpaceLight> pointLights,
        Span<float> shadow,
        Span<float> lightRed,
        Span<float> lightGreen,
        Span<float> lightBlue,
        Span<float> bloom,
        Span<float> scratch)
    {
        ArgumentNullException.ThrowIfNull(world);
        var pixelCount = profile.MaskPixelCount;
        ValidateBuffer(shadow, pixelCount, nameof(shadow));
        ValidateBuffer(lightRed, pixelCount, nameof(lightRed));
        ValidateBuffer(lightGreen, pixelCount, nameof(lightGreen));
        ValidateBuffer(lightBlue, pixelCount, nameof(lightBlue));
        ValidateBuffer(bloom, pixelCount, nameof(bloom));
        ValidateBuffer(scratch, pixelCount, nameof(scratch));

        if (pixelCount == 0 || visibleWorld.Width <= 0 || visibleWorld.Height <= 0)
        {
            return new LightingBuildTelemetry(profile.Tier, profile.MaskSize, 0, 0, 0, false);
        }

        var lightCount = Math.Min(pointLights.Length, profile.Budget.MaxPointLights);
        var daylight = ResolveDaylight(frame.NormalizedTimeOfDay);
        var sunColor = ResolveDaylightColor(frame.NormalizedTimeOfDay);
        var sunDirection = ResolveDirectionTowardSun(frame.NormalizedTimeOfDay);
        var skyStrength = daylight *
            ClampFinite(frame.SkyLightMultiplier, 0f, 2f, 1f) *
            (1f - ClampFinite(frame.WeatherOcclusion, 0f, 0.9f, 0f));
        var caveBlend = ClampFinite(frame.CaveBlend, 0f, 1f, 0f);
        var ambient = Math.Max(
            ClampFinite(frame.AmbientLight, 0f, 1f, 0.08f),
            ClampFinite(frame.CaveResidualLight, 0f, 0.35f, 0.08f) * caveBlend);
        var raysCast = 0L;
        var occluderSamples = 0L;

        for (var maskY = 0; maskY < profile.MaskSize.Y; maskY++)
        {
            var worldY = SampleCoordinate(visibleWorld.Y, visibleWorld.Height, maskY, profile.MaskSize.Y);
            var tileY = WorldPixelToTile(worldY);
            for (var maskX = 0; maskX < profile.MaskSize.X; maskX++)
            {
                var index = maskY * profile.MaskSize.X + maskX;
                var worldX = SampleCoordinate(visibleWorld.X, visibleWorld.Width, maskX, profile.MaskSize.X);
                var tileX = WorldPixelToTile(worldX);
                var tileLight = world.TryGetTile(tileX, tileY, out var tile)
                    ? tile.Light / 255f
                    : 0f;
                var ao = ResolveAmbientOcclusion(
                    world,
                    tileX,
                    tileY,
                    profile.AmbientOcclusionRadius,
                    ref occluderSamples);

                var sunVisibility = skyStrength;
                if (profile.CastSunShadows && skyStrength > MinimumLight)
                {
                    raysCast++;
                    if (IsDirectionalRayOccluded(
                            world,
                            tileX,
                            tileY,
                            sunDirection,
                            profile.Budget.MaxRayStepsPerSample,
                            ref occluderSamples))
                    {
                        sunVisibility = 0f;
                    }
                }
                else if (!profile.CastSunShadows && IsSolid(world, tileX, tileY - 1))
                {
                    sunVisibility *= 0.35f;
                }

                // Open shafts keep directional sunlight; cave ambient is carried by tile light.
                sunVisibility *= 1f - caveBlend * 0.22f;
                var neutralLight = Math.Max(tileLight, ambient + sunVisibility * (1f - ambient));
                var coloredRed = sunVisibility * sunColor.R / 255f * 0.08f;
                var coloredGreen = sunVisibility * sunColor.G / 255f * 0.08f;
                var coloredBlue = sunVisibility * sunColor.B / 255f * 0.08f;
                var emissive = 0f;

                for (var lightIndex = 0; lightIndex < lightCount; lightIndex++)
                {
                    ref readonly var light = ref pointLights[lightIndex];
                    if (!float.IsFinite(light.WorldPosition.X) ||
                        !float.IsFinite(light.WorldPosition.Y) ||
                        !float.IsFinite(light.RadiusPixels) ||
                        !float.IsFinite(light.Intensity) ||
                        !float.IsFinite(light.EmissiveStrength) ||
                        !float.IsFinite(light.FlickerAmount))
                    {
                        continue;
                    }

                    var radius = Math.Clamp(light.RadiusPixels, 1f, 4096f);
                    var deltaX = (float)(light.WorldPosition.X - worldX);
                    var deltaY = (float)(light.WorldPosition.Y - worldY);
                    var distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (!float.IsFinite(distanceSquared) || distanceSquared >= radius * radius)
                    {
                        continue;
                    }

                    var distance = MathF.Sqrt(distanceSquared);
                    var attenuation = 1f - distance / radius;
                    attenuation *= attenuation;
                    var intensity = Math.Clamp(light.Intensity, 0f, 4f) * attenuation;
                    if (intensity <= MinimumLight)
                    {
                        continue;
                    }

                    if (light.CastsShadows && profile.CastPointLightShadows)
                    {
                        raysCast++;
                        var lightTileX = WorldPixelToTile(light.WorldPosition.X);
                        var lightTileY = WorldPixelToTile(light.WorldPosition.Y);
                        if (IsPointRayOccluded(
                                world,
                                tileX,
                                tileY,
                                lightTileX,
                                lightTileY,
                                profile.Budget.MaxRayStepsPerSample,
                                ref occluderSamples))
                        {
                            continue;
                        }
                    }

                    intensity *= ResolveDeterministicFlicker(light, frame.FrameIndex);
                    coloredRed += light.Color.R / 255f * intensity * 0.42f;
                    coloredGreen += light.Color.G / 255f * intensity * 0.42f;
                    coloredBlue += light.Color.B / 255f * intensity * 0.42f;
                    neutralLight = Math.Max(neutralLight, Math.Clamp(intensity * 0.92f, 0f, 1f));
                    emissive += Math.Clamp(light.EmissiveStrength, 0f, 3f) *
                        ClampFinite(frame.EmissiveLightMultiplier, 0f, 2f, 1f) *
                        attenuation;
                }

                var aoDarkening = ao * (0.18f + caveBlend * 0.2f);
                var darkness = (1f - Math.Clamp(neutralLight, 0f, 1f)) *
                    ClampFinite(frame.ShadowStrength, 0f, 1f, 1f);
                shadow[index] = Math.Clamp(darkness + aoDarkening, 0f, 0.94f);
                lightRed[index] = Math.Clamp(coloredRed, 0f, 1f);
                lightGreen[index] = Math.Clamp(coloredGreen, 0f, 1f);
                lightBlue[index] = Math.Clamp(coloredBlue, 0f, 1f);
                bloom[index] = Math.Clamp(
                    emissive * ClampFinite(frame.BloomStrength, 0f, 1.5f, 0f),
                    0f,
                    1f);
            }
        }

        if (profile.Budget.MaxPenumbraRadius > 0)
        {
            BoxBlur(shadow, scratch, profile.MaskSize.X, profile.MaskSize.Y, profile.Budget.MaxPenumbraRadius);
        }

        if (profile.EnableBloom &&
            profile.Budget.MaxBloomRadius > 0 &&
            ClampFinite(frame.BloomStrength, 0f, 1.5f, 0f) > MinimumLight)
        {
            BoxBlur(bloom, scratch, profile.MaskSize.X, profile.MaskSize.Y, profile.Budget.MaxBloomRadius);
        }

        return new LightingBuildTelemetry(
            profile.Tier,
            profile.MaskSize,
            lightCount,
            raysCast,
            occluderSamples,
            pointLights.Length > lightCount);
    }

    internal static Color ResolveDaylightColor(float normalizedTimeOfDay)
    {
        var time = WrapUnit(normalizedTimeOfDay);
        var daylight = ResolveDaylight(time);
        if (daylight <= 0.001f)
        {
            return new Color(105, 132, 188);
        }

        var noonDistance = Math.Abs(time - 0.5f) / 0.25f;
        var warm = Math.Clamp(noonDistance, 0f, 1f);
        return Color.Lerp(new Color(255, 244, 218), new Color(255, 154, 92), warm);
    }

    private static float ResolveDaylight(float normalizedTimeOfDay)
    {
        var time = WrapUnit(normalizedTimeOfDay);
        return Math.Clamp(MathF.Sin((time - 0.25f) * MathF.PI * 2f), 0f, 1f);
    }

    private static Vector2 ResolveDirectionTowardSun(float normalizedTimeOfDay)
    {
        var time = WrapUnit(normalizedTimeOfDay);
        var daylightProgress = Math.Clamp((time - 0.25f) / 0.5f, 0f, 1f);
        var horizontal = MathHelper.Lerp(-0.82f, 0.82f, daylightProgress);
        return Vector2.Normalize(new Vector2(horizontal, -1f));
    }

    private static float ResolveAmbientOcclusion(
        World world,
        int tileX,
        int tileY,
        int radius,
        ref long samples)
    {
        if (radius <= 0)
        {
            return 0f;
        }

        var solid = 0;
        var total = 0;
        for (var offsetY = -radius; offsetY <= radius; offsetY++)
        {
            for (var offsetX = -radius; offsetX <= radius; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                total++;
                samples++;
                if (IsSolid(world, SaturatingAdd(tileX, offsetX), SaturatingAdd(tileY, offsetY)))
                {
                    solid++;
                }
            }
        }

        return total == 0 ? 0f : solid / (float)total;
    }

    private static bool IsDirectionalRayOccluded(
        World world,
        int originX,
        int originY,
        Vector2 direction,
        int maxSteps,
        ref long samples)
    {
        var x = originX + 0.5f;
        var y = originY + 0.5f;
        for (var step = 1; step <= maxSteps; step++)
        {
            x += direction.X;
            y += direction.Y;
            var sampleY = SaturatingFloor(y);
            if (sampleY < 0)
            {
                return false;
            }

            samples++;
            if (IsSolid(world, SaturatingFloor(x), sampleY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointRayOccluded(
        World world,
        int originX,
        int originY,
        int lightX,
        int lightY,
        int maxSteps,
        ref long samples)
    {
        var deltaX = (long)lightX - originX;
        var deltaY = (long)lightY - originY;
        var tileDistance = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        if (tileDistance <= 1)
        {
            return false;
        }

        var sampleCount = (int)Math.Min(tileDistance - 1, Math.Max(1, maxSteps));
        for (var sample = 1; sample <= sampleCount; sample++)
        {
            var progress = sample / (double)(sampleCount + 1);
            var sampleX = SaturatingRound(originX + deltaX * progress);
            var sampleY = SaturatingRound(originY + deltaY * progress);
            samples++;
            if (IsSolid(world, sampleX, sampleY))
            {
                return true;
            }
        }

        return false;
    }

    private static float ResolveDeterministicFlicker(in ScreenSpaceLight light, long frameIndex)
    {
        var amount = Math.Clamp(light.FlickerAmount, 0f, 0.5f);
        if (amount <= 0f)
        {
            return 1f;
        }

        var value = (uint)(frameIndex / 4) ^ light.StableId * 0x9E3779B9u;
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        var unit = (value & 0x00FFFFFFu) / 16777215f;
        return 1f - amount + unit * amount * 2f;
    }

    private static bool IsSolid(World world, int tileX, int tileY)
    {
        return world.TryGetTile(tileX, tileY, out var tile) &&
            (tile.Flags & TileFlags.Solid) != 0;
    }

    private static void BoxBlur(
        Span<float> values,
        Span<float> scratch,
        int width,
        int height,
        int radius)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var start = Math.Max(0, x - radius);
                var end = Math.Min(width - 1, x + radius);
                var sum = 0f;
                for (var sampleX = start; sampleX <= end; sampleX++)
                {
                    sum += values[y * width + sampleX];
                }

                scratch[y * width + x] = sum / (end - start + 1);
            }
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var start = Math.Max(0, y - radius);
                var end = Math.Min(height - 1, y + radius);
                var sum = 0f;
                for (var sampleY = start; sampleY <= end; sampleY++)
                {
                    sum += scratch[sampleY * width + x];
                }

                values[y * width + x] = sum / (end - start + 1);
            }
        }
    }

    private static double SampleCoordinate(int start, int length, int index, int count)
    {
        return start + (index + 0.5d) * length / count;
    }

    private static int WorldPixelToTile(double worldPixel)
    {
        return SaturatingFloor(worldPixel / GameConstants.TileSize);
    }

    private static int SaturatingFloor(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)Math.Floor(value);
    }

    private static int SaturatingRound(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static int SaturatingAdd(int value, int amount)
    {
        var result = (long)value + amount;
        return result <= int.MinValue
            ? int.MinValue
            : result >= int.MaxValue
                ? int.MaxValue
                : (int)result;
    }

    private static float WrapUnit(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0.5f;
        }

        return value - MathF.Floor(value);
    }

    private static float ClampFinite(float value, float minimum, float maximum, float fallback)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
    }

    private static void ValidateBuffer(Span<float> buffer, int required, string name)
    {
        if (buffer.Length < required)
        {
            throw new ArgumentException("Lighting work buffer is smaller than the mask.", name);
        }
    }
}
