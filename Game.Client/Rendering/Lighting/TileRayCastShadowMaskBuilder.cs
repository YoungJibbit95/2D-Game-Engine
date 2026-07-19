using Game.Client.Rendering.Effects;
using Game.Core;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering.Lighting;

internal static class TileRayCastShadowMaskBuilder
{
    private const float MinimumLight = 0.001f;
    private const float SolidSampleMarker = 2f;

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
        var rayPlan = LightingRaySamplePlanner.Build(profile, pointLights.Length);
        var skyIllumination = ResolveSkyIllumination(frame.NormalizedTimeOfDay);
        var sunDirection = ResolveDirectionTowardSun(frame.NormalizedTimeOfDay);
        var skyStrength = skyIllumination *
            ClampFinite(frame.SkyLightMultiplier, 0f, 2f, 1f) *
            (1f - ClampFinite(frame.WeatherOcclusion, 0f, 0.9f, 0f));
        var caveBlend = ClampFinite(frame.CaveBlend, 0f, 1f, 0f);
        // Living-world ambient describes the sky/atmosphere, not a global minimum
        // illumination. Feeding it directly into the floor bleaches every solid tile
        // at daytime and removes the visual distinction between surface and caves.
        var atmosphericAmbient = ClampFinite(frame.AmbientLight, 0f, 1f, 0.08f);
        var surfaceResidual = 0.035f + atmosphericAmbient * 0.055f;
        var caveResidual = ClampFinite(frame.CaveResidualLight, 0f, 0.65f, 0.12f) * caveBlend;
        var ambient = Math.Clamp(Math.Max(surfaceResidual, caveResidual), 0.025f, 0.28f);
        var raysCast = 0L;
        var occluderSamples = 0L;

        PopulateTileSamples(world, visibleWorld, profile.MaskSize, scratch);
        Span<int> lightMaskX = stackalloc int[lightCount];
        Span<int> lightMaskY = stackalloc int[lightCount];
        for (var lightIndex = 0; lightIndex < lightCount; lightIndex++)
        {
            lightMaskX[lightIndex] = WorldToMaskCoordinate(
                pointLights[lightIndex].WorldPosition.X,
                visibleWorld.X,
                visibleWorld.Width,
                profile.MaskSize.X);
            lightMaskY[lightIndex] = WorldToMaskCoordinate(
                pointLights[lightIndex].WorldPosition.Y,
                visibleWorld.Y,
                visibleWorld.Height,
                profile.MaskSize.Y);
        }

        if (profile.CastSunShadows && skyStrength > MinimumLight)
        {
            BuildDirectionalOcclusion(
                scratch,
                profile.MaskSize.X,
                profile.MaskSize.Y,
                sunDirection,
                profile.Budget.MaxRayStepsPerSample,
                bloom,
                ref occluderSamples);
            raysCast += pixelCount;
        }

        for (var maskY = 0; maskY < profile.MaskSize.Y; maskY++)
        {
            var worldY = SampleCoordinate(visibleWorld.Y, visibleWorld.Height, maskY, profile.MaskSize.Y);
            for (var maskX = 0; maskX < profile.MaskSize.X; maskX++)
            {
                var index = maskY * profile.MaskSize.X + maskX;
                var worldX = SampleCoordinate(visibleWorld.X, visibleWorld.Width, maskX, profile.MaskSize.X);
                var tileLight = DecodeTileLight(scratch[index]);
                var isSolid = IsSolidSample(scratch, profile.MaskSize.X, profile.MaskSize.Y, maskX, maskY);
                var hasOpenFaceAbove = isSolid &&
                    !IsSolidSample(scratch, profile.MaskSize.X, profile.MaskSize.Y, maskX, maskY - 1);
                var effectiveTileLight = isSolid && !hasOpenFaceAbove
                    ? Math.Min(tileLight, ambient)
                    : tileLight;
                var coreSkyVisibility = ResolveCoreSkyVisibility(effectiveTileLight, ambient);
                var ao = ResolveAmbientOcclusion(
                    scratch,
                    profile.MaskSize.X,
                    profile.MaskSize.Y,
                    maskX,
                    maskY,
                    profile.AmbientOcclusionRadius,
                    ref occluderSamples);

                var sunVisibility = skyStrength * coreSkyVisibility;
                if (profile.CastSunShadows && skyStrength > MinimumLight)
                {
                    if (bloom[index] > 0f)
                    {
                        sunVisibility = 0f;
                    }
                }
                else if (!profile.CastSunShadows &&
                         IsSolidSample(scratch, profile.MaskSize.X, profile.MaskSize.Y, maskX, maskY - 1))
                {
                    sunVisibility *= 0.35f;
                }

                // Open shafts keep directional sunlight; cave ambient is carried by tile light.
                sunVisibility *= 1f - caveBlend * 0.22f;
                var neutralLight = Math.Max(effectiveTileLight, ambient + sunVisibility * (1f - ambient));
                // Direct sky color is folded into the shadow tint by LightingRenderer. Keeping
                // this buffer point-light-only avoids two redundant GPU uploads in unlit scenes.
                var coloredRed = 0f;
                var coloredGreen = 0f;
                var coloredBlue = 0f;
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

                    if (light.CastsShadows && rayPlan.PointShadowSamples > 0)
                    {
                        var visibility = ResolvePointRayVisibility(
                                scratch,
                                profile.MaskSize.X,
                                profile.MaskSize.Y,
                                maskX,
                                maskY,
                                lightMaskX[lightIndex],
                                lightMaskY[lightIndex],
                                rayPlan,
                                ref raysCast,
                                ref occluderSamples);
                        if (visibility <= MinimumLight)
                        {
                            continue;
                        }

                        intensity *= visibility;
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

                var aoDarkening = ao * (0.08f + caveBlend * 0.1f);
                var unlit = 1f - Math.Clamp(neutralLight, 0f, 1f);
                var darkness = MathF.Pow(unlit, 1.35f) *
                    ClampFinite(frame.ShadowStrength, 0f, 1f, 1f);
                shadow[index] = Math.Clamp(darkness + aoDarkening, 0f, 0.9f);
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
            pointLights.Length > lightCount || rayPlan.WasBudgetClamped,
            rayPlan.PointShadowSamples,
            rayPlan.MaximumPointShadowRays);
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

    internal static float ResolveSkyIllumination(float normalizedTimeOfDay)
    {
        var time = WrapUnit(normalizedTimeOfDay);
        var solarElevation = MathF.Sin((time - 0.25f) * MathF.PI * 2f);
        var twilight = Math.Clamp((solarElevation + 0.5f) / 0.68f, 0f, 1f);
        twilight *= twilight * (3f - 2f * twilight);
        return MathHelper.Lerp(0.12f, 1f, twilight);
    }

    private static Vector2 ResolveDirectionTowardSun(float normalizedTimeOfDay)
    {
        var time = WrapUnit(normalizedTimeOfDay);
        var daylightProgress = Math.Clamp((time - 0.25f) / 0.5f, 0f, 1f);
        var horizontal = MathHelper.Lerp(-0.82f, 0.82f, daylightProgress);
        return Vector2.Normalize(new Vector2(horizontal, -1f));
    }

    private static void PopulateTileSamples(
        World world,
        Rectangle visibleWorld,
        Point maskSize,
        Span<float> destination)
    {
        for (var maskY = 0; maskY < maskSize.Y; maskY++)
        {
            var worldY = SampleCoordinate(visibleWorld.Y, visibleWorld.Height, maskY, maskSize.Y);
            var tileY = WorldPixelToTile(worldY);
            for (var maskX = 0; maskX < maskSize.X; maskX++)
            {
                var worldX = SampleCoordinate(visibleWorld.X, visibleWorld.Width, maskX, maskSize.X);
                var tileX = WorldPixelToTile(worldX);
                var packed = 0f;
                if (world.TryGetTile(tileX, tileY, out var tile))
                {
                    packed = tile.Light / 255f;
                    if ((tile.Flags & TileFlags.Solid) != 0)
                    {
                        packed += SolidSampleMarker;
                    }
                }

                destination[maskY * maskSize.X + maskX] = packed;
            }
        }
    }

    private static float ResolveAmbientOcclusion(
        ReadOnlySpan<float> tileSamples,
        int width,
        int height,
        int sampleX,
        int sampleY,
        int radius,
        ref long sampleCounter)
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
                sampleCounter++;
                if (IsSolidSample(
                        tileSamples,
                        width,
                        height,
                        sampleX + offsetX,
                        sampleY + offsetY))
                {
                    solid++;
                }
            }
        }

        return total == 0 ? 0f : solid / (float)total;
    }

    private static void BuildDirectionalOcclusion(
        ReadOnlySpan<float> tileSamples,
        int width,
        int height,
        Vector2 direction,
        int maxSteps,
        Span<float> destination,
        ref long sampleCounter)
    {
        var stepX = MathF.Abs(direction.X) < 0.25f ? 0 : Math.Sign(direction.X);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var sourceX = x + stepX;
                var sourceY = y - 1;
                if ((uint)sourceX >= (uint)width || sourceY < 0 || maxSteps <= 0)
                {
                    destination[index] = 0f;
                    continue;
                }

                sampleCounter++;
                var sourceIndex = sourceY * width + sourceX;
                if (IsSolidSample(tileSamples, width, height, sourceX, sourceY))
                {
                    destination[index] = 1f;
                    continue;
                }

                var previousDistance = destination[sourceIndex];
                destination[index] = previousDistance > 0f
                    ? Math.Min(maxSteps, previousDistance + 1f)
                    : 0f;
            }
        }
    }

    private static bool IsPointRayOccluded(
        ReadOnlySpan<float> tileSamples,
        int width,
        int height,
        int originX,
        int originY,
        int lightX,
        int lightY,
        int maxSteps,
        ref long sampleCounter)
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
            sampleCounter++;
            if (IsSolidSample(tileSamples, width, height, sampleX, sampleY))
            {
                return true;
            }
        }

        return false;
    }

    internal static float ResolvePointRayVisibility(
        ReadOnlySpan<float> tileSamples,
        int width,
        int height,
        int originX,
        int originY,
        int lightX,
        int lightY,
        in LightingRaySamplePlan plan,
        ref long rayCounter,
        ref long sampleCounter)
    {
        var deltaX = lightX - originX;
        var deltaY = lightY - originY;
        var length = MathF.Sqrt((float)deltaX * deltaX + (float)deltaY * deltaY);
        var tangentX = length > 0.001f ? -deltaY / length : 0f;
        var tangentY = length > 0.001f ? deltaX / length : 1f;
        var visible = 0;
        for (var ray = 0; ray < plan.PointShadowSamples; ray++)
        {
            var kernel = ray switch
            {
                1 => 1f,
                2 => -1f,
                _ => 0f
            };
            var endpointX = lightX + (int)MathF.Round(tangentX * plan.EndpointSpreadMaskPixels * kernel);
            var endpointY = lightY + (int)MathF.Round(tangentY * plan.EndpointSpreadMaskPixels * kernel);
            endpointX = Math.Clamp(endpointX, 0, width - 1);
            endpointY = Math.Clamp(endpointY, 0, height - 1);
            rayCounter++;
            if (!IsPointRayOccluded(
                    tileSamples,
                    width,
                    height,
                    originX,
                    originY,
                    endpointX,
                    endpointY,
                    plan.MaxStepsPerRay,
                    ref sampleCounter))
            {
                visible++;
            }
        }

        return visible / (float)plan.PointShadowSamples;
    }

    private static float DecodeTileLight(float packedSample)
    {
        var light = packedSample >= SolidSampleMarker
            ? packedSample - SolidSampleMarker
            : packedSample;
        return Math.Clamp(light, 0f, 1f);
    }

    private static float ResolveCoreSkyVisibility(float tileLight, float ambient)
    {
        var denominator = Math.Max(0.001f, 1f - ambient);
        var normalized = Math.Clamp((tileLight - ambient) / denominator, 0f, 1f);
        return normalized * normalized * (3f - 2f * normalized);
    }

    private static bool IsSolidSample(
        ReadOnlySpan<float> tileSamples,
        int width,
        int height,
        int x,
        int y)
    {
        return (uint)x < (uint)width &&
            (uint)y < (uint)height &&
            tileSamples[y * width + x] >= SolidSampleMarker;
    }

    private static int WorldToMaskCoordinate(double worldPosition, int start, int length, int count)
    {
        if (!double.IsFinite(worldPosition) || length <= 0 || count <= 1)
        {
            return 0;
        }

        var normalized = (worldPosition - start) / length;
        return Math.Clamp(SaturatingFloor(normalized * count), 0, count - 1);
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

    private static void BoxBlur(
        Span<float> values,
        Span<float> scratch,
        int width,
        int height,
        int radius)
    {
        if (radius <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var sum = 0f;
            for (var sampleX = 0; sampleX <= Math.Min(width - 1, radius); sampleX++)
            {
                sum += values[row + sampleX];
            }

            for (var x = 0; x < width; x++)
            {
                if (x > 0)
                {
                    var removed = x - radius - 1;
                    if (removed >= 0)
                    {
                        sum -= values[row + removed];
                    }

                    var added = x + radius;
                    if (added < width)
                    {
                        sum += values[row + added];
                    }
                }

                var start = Math.Max(0, x - radius);
                var end = Math.Min(width - 1, x + radius);
                scratch[row + x] = sum / (end - start + 1);
            }
        }

        for (var x = 0; x < width; x++)
        {
            var sum = 0f;
            for (var sampleY = 0; sampleY <= Math.Min(height - 1, radius); sampleY++)
            {
                sum += scratch[sampleY * width + x];
            }

            for (var y = 0; y < height; y++)
            {
                if (y > 0)
                {
                    var removed = y - radius - 1;
                    if (removed >= 0)
                    {
                        sum -= scratch[removed * width + x];
                    }

                    var added = y + radius;
                    if (added < height)
                    {
                        sum += scratch[added * width + x];
                    }
                }

                var start = Math.Max(0, y - radius);
                var end = Math.Min(height - 1, y + radius);
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
