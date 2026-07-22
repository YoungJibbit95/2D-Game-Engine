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
        Span<float> scratch,
        int? surfaceTileY = null,
        Func<int, int>? surfaceHeightResolver = null)
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
        var solar = SolarIlluminationCurve.Evaluate(frame.NormalizedTimeOfDay);
        var skyMultiplier = ClampFinite(frame.SkyLightMultiplier, 0f, 2f, 1f);
        var weatherOcclusion = ClampFinite(frame.WeatherOcclusion, 0f, 0.9f, 0f);
        var diffuseSkyStrength = solar.DiffuseIrradiance * skyMultiplier *
            (1f - weatherOcclusion * 0.38f);
        var directSunStrength = (solar.DirectIrradiance + solar.LunarIrradiance) * skyMultiplier *
            (1f - weatherOcclusion * 0.88f);
        var caveBlend = ClampFinite(frame.CaveBlend, 0f, 1f, 0f);
        // Living-world ambient describes the sky/atmosphere, not a global minimum
        // illumination. Feeding it directly into the floor bleaches every solid tile
        // at daytime and removes the visual distinction between surface and caves.
        var atmosphericAmbient = ClampFinite(frame.AmbientLight, 0f, 1f, 0.08f);
        var surfaceResidual = 0.04f + atmosphericAmbient * 0.07f;
        var caveResidual = ClampFinite(frame.CaveResidualLight, 0f, 0.65f, 0.12f);
        var ambient = Math.Clamp(
            MathHelper.Lerp(surfaceResidual, Math.Max(surfaceResidual, caveResidual), caveBlend),
            0.035f,
            0.65f);
        var raysCast = 0L;
        var occluderSamples = 0L;

        PopulateTileSamples(world, visibleWorld, profile.MaskSize, scratch);
        PopulateAmbientOcclusion(
            scratch,
            profile.MaskSize.X,
            profile.MaskSize.Y,
            profile.AmbientOcclusionRadius,
            shadow,
            ref occluderSamples);
        var fallbackSurfaceY = surfaceTileY.HasValue
            ? Math.Clamp(surfaceTileY.Value, 0, Math.Max(0, world.HeightTiles - 1))
            : int.MinValue;
        var hasSurfaceModel = fallbackSurfaceY != int.MinValue || surfaceHeightResolver is not null;
        Span<int> localSurfaceByMaskX = stackalloc int[profile.MaskSize.X];
        if (hasSurfaceModel)
        {
            for (var maskX = 0; maskX < profile.MaskSize.X; maskX++)
            {
                var worldX = SampleCoordinate(visibleWorld.X, visibleWorld.Width, maskX, profile.MaskSize.X);
                var tileX = WorldPixelToTile(worldX);
                var resolvedSurface = surfaceHeightResolver?.Invoke(tileX) ?? fallbackSurfaceY;
                localSurfaceByMaskX[maskX] = Math.Clamp(
                    resolvedSurface,
                    0,
                    Math.Max(0, world.HeightTiles - 1));
            }
        }

        var topTileY = WorldPixelToTile(visibleWorld.Top);
        PopulateDiffuseSkyPortalVisibility(
            scratch,
            profile.MaskSize.X,
            profile.MaskSize.Y,
            visibleWorld,
            localSurfaceByMaskX,
            hasSurfaceModel,
            lightRed);
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

        if (profile.CastSunShadows && directSunStrength > MinimumLight)
        {
            BuildDirectionalOcclusion(
                scratch,
                profile.MaskSize.X,
                profile.MaskSize.Y,
                solar.DirectionTowardPrimaryLight,
                profile.Budget.MaxRayStepsPerSample,
                bloom,
                ref occluderSamples);
            raysCast += pixelCount;
        }

        for (var maskY = 0; maskY < profile.MaskSize.Y; maskY++)
        {
            var worldY = SampleCoordinate(visibleWorld.Y, visibleWorld.Height, maskY, profile.MaskSize.Y);
            var tileY = WorldPixelToTile(worldY);
            for (var maskX = 0; maskX < profile.MaskSize.X; maskX++)
            {
                var index = maskY * profile.MaskSize.X + maskX;
                var localSurfaceY = hasSurfaceModel
                    ? localSurfaceByMaskX[maskX]
                    : int.MinValue;
                var worldX = SampleCoordinate(visibleWorld.X, visibleWorld.Width, maskX, profile.MaskSize.X);
                var tileLight = DecodeTileLight(scratch[index]);
                var isSolid = IsSolidSample(scratch, profile.MaskSize.X, profile.MaskSize.Y, maskX, maskY);
                var hasOpenFaceAbove = isSolid &&
                    !IsSolidSample(scratch, profile.MaskSize.X, profile.MaskSize.Y, maskX, maskY - 1);
                var effectiveTileLight = isSolid && !hasOpenFaceAbove
                    ? Math.Min(tileLight, ambient)
                    : tileLight;
                var coreSkyVisibility = Math.Max(
                    ResolveCoreSkyVisibility(effectiveTileLight, ambient),
                    lightRed[index] * 0.82f);
                var viewportContainsSky = hasSurfaceModel && topTileY <= localSurfaceY;
                var rayIsOpen = !profile.CastSunShadows || bloom[index] <= 0.001f;
                var exposedSurface = hasSurfaceModel &&
                    tileY <= localSurfaceY &&
                    (!isSolid || hasOpenFaceAbove);
                var openShaft = viewportContainsSky && rayIsOpen && !isSolid;
                var geometricSkyVisibility = exposedSurface || openShaft ? 1f : 0f;
                coreSkyVisibility = Math.Max(coreSkyVisibility, geometricSkyVisibility);
                var ao = shadow[index];

                var diffuseVisibility = diffuseSkyStrength * coreSkyVisibility;
                var directVisibility = directSunStrength * coreSkyVisibility;
                if (profile.CastSunShadows && directSunStrength > MinimumLight)
                {
                    directVisibility *= 1f - Math.Clamp(bloom[index], 0f, 1f);
                }
                else if (!profile.CastSunShadows &&
                         IsSolidSample(scratch, profile.MaskSize.X, profile.MaskSize.Y, maskX, maskY - 1))
                {
                    directVisibility *= 0.35f;
                }

                // Diffuse sky survives overcast conditions while the directional
                // term creates shafts and physically responsive occluder shadows.
                var caveSkyAttenuation = 1f - caveBlend * 0.22f;
                var skyRadiance = Math.Clamp(
                    (diffuseVisibility * 0.78f + directVisibility * 0.38f) * caveSkyAttenuation,
                    0f,
                    1f);
                var neutralLight = Math.Max(effectiveTileLight, ambient + skyRadiance * (1f - ambient));
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

                var aoDarkening = ao * (0.06f + caveBlend * 0.08f);
                var unlit = 1f - Math.Clamp(neutralLight, 0f, 1f);
                var darkness = MathF.Pow(unlit, 1.55f) *
                    ClampFinite(frame.ShadowStrength, 0f, 1f, 1f);
                shadow[index] = Math.Clamp(darkness + aoDarkening, 0f, 0.86f);
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
        var solar = SolarIlluminationCurve.Evaluate(time);
        var horizonBlend = Math.Clamp((solar.Elevation + 0.12f) / 0.2f, 0f, 1f);
        horizonBlend *= horizonBlend * (3f - 2f * horizonBlend);
        var noonDistance = Math.Abs(time - 0.5f) / 0.25f;
        var warm = Math.Clamp(noonDistance, 0f, 1f);
        var daylightColor = Color.Lerp(new Color(255, 244, 218), new Color(255, 154, 92), warm);
        return Color.Lerp(new Color(105, 132, 188), daylightColor, horizonBlend);
    }

    internal static float ResolveSkyIllumination(float normalizedTimeOfDay)
    {
        return SolarIlluminationCurve.ResolveSkyIllumination(normalizedTimeOfDay);
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
                    if (ResolveTileSampleOcclusion(tile, worldX, worldY))
                    {
                        packed += SolidSampleMarker;
                    }
                }

                destination[maskY * maskSize.X + maskX] = packed;
            }
        }
    }

    internal static bool ResolveTileSampleOcclusion(
        in TileInstance tile,
        double worldX,
        double worldY)
    {
        var collisionShape = tile.CollisionShape;
        if (collisionShape == TileCollisionShape.Empty)
        {
            return false;
        }

        if (collisionShape == TileCollisionShape.FullBlock)
        {
            return true;
        }

        if (!double.IsFinite(worldX) || !double.IsFinite(worldY))
        {
            return false;
        }

        var localX = PositiveTileModulo(worldX);
        var localY = PositiveTileModulo(worldY);
        return collisionShape switch
        {
            TileCollisionShape.OneWayPlatform => localY < 3d,
            TileCollisionShape.HalfBlock => localY >= GameConstants.TileSize * 0.5d,
            TileCollisionShape.SlopeAscendingLeft => localY >= localX,
            TileCollisionShape.SlopeAscendingRight => localY >= GameConstants.TileSize - localX,
            _ => false
        };
    }

    private static double PositiveTileModulo(double worldCoordinate)
    {
        var local = worldCoordinate % GameConstants.TileSize;
        return local < 0d ? local + GameConstants.TileSize : local;
    }

    private static void PopulateDiffuseSkyPortalVisibility(
        ReadOnlySpan<float> tileSamples,
        int width,
        int height,
        Rectangle visibleWorld,
        ReadOnlySpan<int> localSurfaceByMaskX,
        bool hasSurfaceModel,
        Span<float> destination)
    {
        var count = Math.Max(0, width * height);
        destination[..count].Clear();
        if (!hasSurfaceModel || width <= 0 || height <= 0 || localSurfaceByMaskX.Length < width)
        {
            return;
        }

        for (var y = 0; y < height; y++)
        {
            var worldY = SampleCoordinate(visibleWorld.Y, visibleWorld.Height, y, height);
            var tileY = WorldPixelToTile(worldY);
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = row + x;
                if (tileY <= localSurfaceByMaskX[x] && tileSamples[index] < SolidSampleMarker)
                {
                    destination[index] = 1f;
                }
            }
        }

        const float stepTransmission = 0.955f;
        for (var pass = 0; pass < 2; pass++)
        {
            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                var previousRow = row - width;
                for (var x = 0; x < width; x++)
                {
                    var index = row + x;
                    if (tileSamples[index] >= SolidSampleMarker)
                    {
                        continue;
                    }

                    var value = destination[index];
                    if (x > 0)
                    {
                        value = Math.Max(value, destination[index - 1] * stepTransmission);
                    }

                    if (y > 0)
                    {
                        value = Math.Max(value, destination[previousRow + x] * stepTransmission);
                    }

                    destination[index] = value;
                }

                for (var x = width - 2; x >= 0; x--)
                {
                    var index = row + x;
                    if (tileSamples[index] < SolidSampleMarker)
                    {
                        destination[index] = Math.Max(
                            destination[index],
                            destination[index + 1] * stepTransmission);
                    }
                }
            }

            for (var y = height - 1; y >= 0; y--)
            {
                var row = y * width;
                var nextRow = row + width;
                for (var x = width - 1; x >= 0; x--)
                {
                    var index = row + x;
                    if (tileSamples[index] >= SolidSampleMarker)
                    {
                        continue;
                    }

                    var value = destination[index];
                    if (x + 1 < width)
                    {
                        value = Math.Max(value, destination[index + 1] * stepTransmission);
                    }

                    if (y + 1 < height)
                    {
                        value = Math.Max(value, destination[nextRow + x] * stepTransmission);
                    }

                    destination[index] = value;
                }

                for (var x = 1; x < width; x++)
                {
                    var index = row + x;
                    if (tileSamples[index] < SolidSampleMarker)
                    {
                        destination[index] = Math.Max(
                            destination[index],
                            destination[index - 1] * stepTransmission);
                    }
                }
            }
        }
    }
    private static void PopulateAmbientOcclusion(
        ReadOnlySpan<float> tileSamples,
        int width,
        int height,
        int radius,
        Span<float> destination,
        ref long sampleCounter)
    {
        if (radius <= 0 || width <= 0 || height <= 0)
        {
            destination[..Math.Max(0, width * height)].Clear();
            return;
        }

        var boundedRadius = Math.Min(radius, 32);
        var diameter = boundedRadius * 2 + 1;
        var samplesPerPixel = diameter * diameter - 1;
        for (var y = 0; y < height; y++)
        {
            var solidCount = 0;
            for (var sampleY = y - boundedRadius; sampleY <= y + boundedRadius; sampleY++)
            {
                for (var sampleX = -boundedRadius; sampleX <= boundedRadius; sampleX++)
                {
                    if (IsSolidSample(tileSamples, width, height, sampleX, sampleY))
                    {
                        solidCount++;
                    }
                }
            }

            for (var x = 0; x < width; x++)
            {
                var centerSolid = IsSolidSample(tileSamples, width, height, x, y) ? 1 : 0;
                destination[y * width + x] = Math.Max(0, solidCount - centerSolid) /
                    (float)samplesPerPixel;
                sampleCounter += samplesPerPixel;

                var removeX = x - boundedRadius;
                var addX = x + boundedRadius + 1;
                for (var sampleY = y - boundedRadius; sampleY <= y + boundedRadius; sampleY++)
                {
                    if (IsSolidSample(tileSamples, width, height, removeX, sampleY))
                    {
                        solidCount--;
                    }

                    if (IsSolidSample(tileSamples, width, height, addX, sampleY))
                    {
                        solidCount++;
                    }
                }
            }
        }
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
        var pixelCount = Math.Max(0, width * height);
        if (maxSteps <= 0 || width <= 0 || height <= 0)
        {
            destination[..pixelCount].Clear();
            return;
        }

        var absoluteX = MathF.Abs(direction.X);
        var absoluteY = MathF.Abs(direction.Y);
        var dominant = Math.Max(absoluteX, absoluteY);
        if (!float.IsFinite(dominant) || dominant <= 0.0001f)
        {
            destination[..pixelCount].Clear();
            return;
        }

        // Transport shadow energy from the viewport edge facing the sun. Each
        // destination samples only the already-computed upstream scanline, so
        // arbitrary solar angles remain O(mask pixels) instead of O(pixels *
        // ray length). Linear sampling supplies a stable angular penumbra; the
        // bounded decay models a finite shadow distance before the final blur.
        var decay = 1f / Math.Max(1, maxSteps);
        if (absoluteY >= absoluteX)
        {
            var sourceYOffset = direction.Y < 0f ? -1 : 1;
            var startY = sourceYOffset < 0 ? 0 : height - 1;
            var endY = sourceYOffset < 0 ? height : -1;
            var rowStep = sourceYOffset < 0 ? 1 : -1;
            var sourceXOffset = direction.X / absoluteY;
            for (var y = startY; y != endY; y += rowStep)
            {
                var sourceY = y + sourceYOffset;
                for (var x = 0; x < width; x++)
                {
                    var sourceX = x + sourceXOffset;
                    var occlusion = SampleDirectionalOcclusion(
                        tileSamples,
                        destination,
                        width,
                        height,
                        sourceX,
                        sourceY,
                        ref sampleCounter);
                    destination[y * width + x] = Math.Max(0f, occlusion - decay);
                }
            }

            return;
        }

        var sourceXStep = direction.X < 0f ? -1 : 1;
        var startX = sourceXStep < 0 ? 0 : width - 1;
        var endX = sourceXStep < 0 ? width : -1;
        var columnStep = sourceXStep < 0 ? 1 : -1;
        var sourceYOffsetFraction = direction.Y / absoluteX;
        for (var x = startX; x != endX; x += columnStep)
        {
            var sourceX = x + sourceXStep;
            for (var y = 0; y < height; y++)
            {
                var sourceY = y + sourceYOffsetFraction;
                var occlusion = SampleDirectionalOcclusion(
                    tileSamples,
                    destination,
                    width,
                    height,
                    sourceX,
                    sourceY,
                    ref sampleCounter);
                destination[y * width + x] = Math.Max(0f, occlusion - decay);
            }
        }
    }

    private static float SampleDirectionalOcclusion(
        ReadOnlySpan<float> tileSamples,
        ReadOnlySpan<float> shadowSamples,
        int width,
        int height,
        float sourceX,
        float sourceY,
        ref long sampleCounter)
    {
        var x0 = SaturatingFloor(sourceX);
        var y0 = SaturatingFloor(sourceY);
        var blendX = Math.Clamp(sourceX - x0, 0f, 1f);
        var blendY = Math.Clamp(sourceY - y0, 0f, 1f);
        if (blendY <= 0.0001f)
        {
            return MathHelper.Lerp(
                ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0, y0, ref sampleCounter),
                ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0 + 1, y0, ref sampleCounter),
                blendX);
        }

        if (blendX <= 0.0001f)
        {
            return MathHelper.Lerp(
                ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0, y0, ref sampleCounter),
                ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0, y0 + 1, ref sampleCounter),
                blendY);
        }

        var top = MathHelper.Lerp(
            ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0, y0, ref sampleCounter),
            ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0 + 1, y0, ref sampleCounter),
            blendX);
        var bottom = MathHelper.Lerp(
            ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0, y0 + 1, ref sampleCounter),
            ReadDirectionalOcclusion(tileSamples, shadowSamples, width, height, x0 + 1, y0 + 1, ref sampleCounter),
            blendX);
        return MathHelper.Lerp(top, bottom, blendY);
    }

    private static float ReadDirectionalOcclusion(
        ReadOnlySpan<float> tileSamples,
        ReadOnlySpan<float> shadowSamples,
        int width,
        int height,
        int x,
        int y,
        ref long sampleCounter)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
        {
            return 0f;
        }

        sampleCounter++;
        var index = y * width + x;
        return tileSamples[index] >= SolidSampleMarker
            ? 1f
            : shadowSamples[index];
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
        if (maxSteps <= 0 || (originX == lightX && originY == lightY))
        {
            return false;
        }

        var x = originX;
        var y = originY;
        var deltaX = Math.Abs(lightX - originX);
        var deltaY = -Math.Abs(lightY - originY);
        var directionX = originX < lightX ? 1 : -1;
        var directionY = originY < lightY ? 1 : -1;
        var error = deltaX + deltaY;
        var traversed = 0;
        while ((x != lightX || y != lightY) && traversed < maxSteps)
        {
            var previousX = x;
            var previousY = y;
            var doubledError = error * 2;
            if (doubledError >= deltaY)
            {
                error += deltaY;
                x += directionX;
            }

            if (doubledError <= deltaX)
            {
                error += deltaX;
                y += directionY;
            }

            if (x == lightX && y == lightY)
            {
                break;
            }

            // Supercover corner checks prevent thin diagonal walls from leaking
            // light through a grid corner.
            if (x != previousX && y != previousY)
            {
                sampleCounter += 2;
                if (IsSolidSample(tileSamples, width, height, x, previousY) ||
                    IsSolidSample(tileSamples, width, height, previousX, y))
                {
                    return true;
                }
            }

            sampleCounter++;
            if (IsSolidSample(tileSamples, width, height, x, y))
            {
                return true;
            }

            traversed++;
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
