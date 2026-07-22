using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core;
using Game.Core.Diagnostics;
using Game.Core.Runtime;
using Game.Core.Settings;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering;

public sealed class LightingRenderer : IDisposable
{
    private readonly PresentationPassDescriptor[] _passes =
        new PresentationPassDescriptor[PresentationPassPlanner.MaximumPassCount];
    private Texture2D? _shadowMap;
    private Texture2D? _coloredLightMap;
    private Texture2D? _bloomMap;
    private Texture2D? _reflectionMap;
    private Texture2D? _shadowUploadMap;
    private Texture2D? _coloredLightUploadMap;
    private Texture2D? _bloomUploadMap;
    private Texture2D? _reflectionUploadMap;
    private Texture2D? _shadowSpareMap;
    private Texture2D? _coloredLightSpareMap;
    private Texture2D? _bloomSpareMap;
    private Texture2D? _reflectionSpareMap;
    private Color[] _shadowPixels = Array.Empty<Color>();
    private Color[] _coloredLightPixels = Array.Empty<Color>();
    private Color[] _bloomPixels = Array.Empty<Color>();
    private Color[] _reflectionPixels = Array.Empty<Color>();
    private WaterReflectionSurface[] _reflectionSurfaces = Array.Empty<WaterReflectionSurface>();
    private float[] _shadowValues = Array.Empty<float>();
    private float[] _lightRed = Array.Empty<float>();
    private float[] _lightGreen = Array.Empty<float>();
    private float[] _lightBlue = Array.Empty<float>();
    private float[] _bloomValues = Array.Empty<float>();
    private float[] _scratch = Array.Empty<float>();
    private float[] _previousShadowValues = Array.Empty<float>();
    private float[] _previousLightRed = Array.Empty<float>();
    private float[] _previousLightGreen = Array.Empty<float>();
    private float[] _previousLightBlue = Array.Empty<float>();
    private PresentationQualityProfile _quality;
    private Rectangle _resourceViewport;
    private Rectangle _preparedViewport;
    private bool _hasPreparedFrame;
    private bool _hasColoredLight;
    private bool _hasBloom;
    private bool _hasReflection;
    private bool _wasConfigured;
    private float _reflectionStrength;
    private Rectangle _previousVisibleWorld;
    private long _previousLightingFrameIndex = -1;
    private bool _hasLightingHistory;
    private TextureUploadContentTracker _shadowContent;
    private TextureUploadContentTracker _coloredLightContent;
    private TextureUploadContentTracker _bloomContent;
    private TextureUploadContentTracker _reflectionContent;

    public bool ResourcesPrepared => _shadowMap is not null;

    public PresentationQualityProfile Quality => _quality;

    public PresentationPassPlan LastPassPlan { get; private set; }

    public LightingBuildTelemetry LastTelemetry { get; private set; }

    public LightingReflectionTelemetry LastReflectionTelemetry { get; private set; }

    public WaterReflectionPlanTelemetry LastReflectionSurfaceTelemetry { get; private set; }
    public LightingTemporalStabilizationTelemetry LastTemporalTelemetry { get; private set; }


    public int LastTextureUploadCount { get; private set; }

    public int LastTextureUploadSkippedCount { get; private set; }

    public long TotalTextureUploadCount { get; private set; }

    public long TotalTextureUploadSkippedCount { get; private set; }

    public ReadOnlySpan<PresentationPassDescriptor> LastPasses =>
        _passes.AsSpan(0, LastPassPlan.PassCount);

    public void PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        PresentationQualityTier quality,
        PresentationBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        var profile = PresentationQualityProfile.Create(quality, viewport, budget) with
        {
            EnableReflections = false
        };
        PrepareResources(graphicsDevice, viewport, profile);
    }

    public PresentationQualityProfile PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        RenderingSettings settings)
    {
        var configuration = PresentationSettingsAdapter.Create(settings, viewport);
        var profile = configuration.Lighting with
        {
            EnableReflections = configuration.Reflections.EnableReflections,
            Budget = configuration.Lighting.Budget with
            {
                MaxReflectionSurfaces = Math.Min(
                    configuration.Lighting.Budget.MaxReflectionSurfaces,
                    configuration.Reflections.Budget.MaxReflectionSurfaces),
                MaxReflectionStripsPerSurface = Math.Min(
                    configuration.Lighting.Budget.MaxReflectionStripsPerSurface,
                    configuration.Reflections.Budget.MaxReflectionStripsPerSurface)
            }
        };
        PrepareResources(graphicsDevice, viewport, profile);
        _reflectionStrength = configuration.Reflections.EnableReflections
            ? configuration.ReflectionStrength
            : 0f;
        return profile;
    }

    public void PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        in PresentationQualityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _reflectionStrength = 0f;
        _wasConfigured = true;
        if (profile.Tier == PresentationQualityTier.Disabled)
        {
            ReleaseResources();
            _quality = profile;
            _resourceViewport = viewport;
            return;
        }

        if (ResourcesMatch(graphicsDevice, viewport, profile))
        {
            return;
        }

        ReleaseResources();
        _quality = profile;
        _resourceViewport = viewport;
        var width = profile.MaskSize.X;
        var height = profile.MaskSize.Y;
        _shadowMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _coloredLightMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _bloomMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _shadowUploadMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _coloredLightUploadMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _bloomUploadMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _shadowSpareMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _coloredLightSpareMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        _bloomSpareMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
        if (profile.EnableReflections && profile.Budget.MaxReflectionSurfaces > 0)
        {
            _reflectionMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
            _reflectionUploadMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
            _reflectionSpareMap = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
            _reflectionSurfaces = new WaterReflectionSurface[profile.Budget.MaxReflectionSurfaces];
        }

        var count = profile.MaskPixelCount;
        _shadowPixels = new Color[count];
        _coloredLightPixels = new Color[count];
        _bloomPixels = new Color[count];
        _reflectionPixels = profile.EnableReflections ? new Color[count] : Array.Empty<Color>();
        _shadowValues = new float[count];
        _lightRed = new float[count];
        _lightGreen = new float[count];
        _lightBlue = new float[count];
        _bloomValues = new float[count];
        _scratch = new float[count];
        _previousShadowValues = new float[count];
        _previousLightRed = new float[count];
        _previousLightGreen = new float[count];
        _previousLightBlue = new float[count];
    }

    public LightingBuildTelemetry PrepareFrame(
        World world,
        Camera2D camera,
        Rectangle viewport,
        in LightingFrameParameters frame,
        ReadOnlySpan<ScreenSpaceLight> pointLights,
        PerformanceProfiler? performance = null,
        int? surfaceTileY = null,
        Func<int, int>? surfaceHeightResolver = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        if (!ResourcesPrepared)
        {
            _hasPreparedFrame = false;
            LastPassPlan = default;
            LastTelemetry = default;
            LastReflectionTelemetry = default;
            LastReflectionSurfaceTelemetry = default;
            LastTemporalTelemetry = default;
            return LastTelemetry;
        }

        if (viewport != _resourceViewport)
        {
            throw new InvalidOperationException(
                "Lighting resources do not match the requested viewport. Call PrepareResources before PrepareFrame.");
        }

        var visibleWorld = camera.VisibleWorldRect;
        using (performance?.Measure("Presentation.LightingMaskCpu", 1.25) ?? default)
        {
            LastTelemetry = TileRayCastShadowMaskBuilder.Build(
                world,
                visibleWorld,
                _quality,
                frame,
                pointLights,
                _shadowValues,
                _lightRed,
                _lightGreen,
                _lightBlue,
                _bloomValues,
                _scratch,
                surfaceTileY,
                surfaceHeightResolver);
        }

        var canUseHistory = _hasLightingHistory &&
            frame.FrameIndex > _previousLightingFrameIndex &&
            frame.FrameIndex - _previousLightingFrameIndex <= 8;
        if (canUseHistory)
        {
            using (performance?.Measure("Presentation.LightingTemporal", 0.2) ?? default)
            {
                LastTemporalTelemetry = LightingTemporalStabilizer.Apply(
                    _previousVisibleWorld,
                    visibleWorld,
                    _quality.MaskSize,
                    _previousShadowValues,
                    _previousLightRed,
                    _previousLightGreen,
                    _previousLightBlue,
                    _shadowValues,
                    _lightRed,
                    _lightGreen,
                    _lightBlue);
            }
        }
        else
        {
            LastTemporalTelemetry = new LightingTemporalStabilizationTelemetry(0, 0, true);
        }

        _shadowValues.AsSpan(0, _quality.MaskPixelCount).CopyTo(_previousShadowValues);
        _lightRed.AsSpan(0, _quality.MaskPixelCount).CopyTo(_previousLightRed);
        _lightGreen.AsSpan(0, _quality.MaskPixelCount).CopyTo(_previousLightGreen);
        _lightBlue.AsSpan(0, _quality.MaskPixelCount).CopyTo(_previousLightBlue);
        _previousVisibleWorld = visibleWorld;
        _previousLightingFrameIndex = frame.FrameIndex;
        _hasLightingHistory = true;

        var hasReflection = false;
        if (_quality.EnableReflections &&
            _reflectionStrength > 0.001f &&
            _reflectionPixels.Length >= _quality.MaskPixelCount)
        {
            using (performance?.Measure("Presentation.LightingReflectionRadianceCpu", 0.35) ?? default)
            {
                LastReflectionSurfaceTelemetry = WaterReflectionSurfacePlanner.Build(
                    world,
                    camera,
                    viewport,
                    _quality,
                    _reflectionSurfaces);
                LastReflectionTelemetry = ReflectionRadianceMapBuilder.Build(
                    viewport,
                    _quality,
                    frame,
                    _reflectionSurfaces.AsSpan(0, LastReflectionSurfaceTelemetry.SurfaceCount),
                    _lightRed,
                    _lightGreen,
                    _lightBlue,
                    _shadowValues,
                    _reflectionStrength,
                    _reflectionPixels);
            }

            hasReflection = LastReflectionTelemetry.PixelsShaded > 0;
        }
        else
        {
            LastReflectionTelemetry = default;
            LastReflectionSurfaceTelemetry = default;
        }

        var caveBlend = float.IsFinite(frame.CaveBlend) ? Math.Clamp(frame.CaveBlend, 0f, 1f) : 0f;
        var caveTint = Color.Lerp(
            new Color(3, 7, 12),
            new Color(10, 4, 18),
            caveBlend);
        var skyIllumination = TileRayCastShadowMaskBuilder.ResolveSkyIllumination(frame.NormalizedTimeOfDay);
        var surfaceTint = Color.Lerp(new Color(18, 28, 58), new Color(18, 20, 24), skyIllumination);
        var shadowTint = Color.Lerp(surfaceTint, caveTint, caveBlend);
        var hasColoredLight = false;
        var hasBloom = false;
        var encodeEmissive = LastTelemetry.PointLightsUsed > 0;
        using (performance?.Measure("Presentation.LightingColorEncode", 0.5) ?? default)
        {
            for (var index = 0; index < _quality.MaskPixelCount; index++)
            {
                _shadowPixels[index] = Premultiply(shadowTint, _shadowValues[index]);
                if (!encodeEmissive)
                {
                    continue;
                }

                _coloredLightPixels[index] = new Color(
                    _lightRed[index],
                    _lightGreen[index],
                    _lightBlue[index],
                    1f);
                hasColoredLight |= _lightRed[index] > 0.001f ||
                    _lightGreen[index] > 0.001f ||
                    _lightBlue[index] > 0.001f;

                var bloom = _bloomValues[index];
                _bloomPixels[index] = new Color(
                    Math.Clamp(bloom, 0f, 1f),
                    Math.Clamp(bloom * 0.72f, 0f, 1f),
                    Math.Clamp(bloom * 0.42f, 0f, 1f),
                    1f);
                hasBloom |= bloom > 0.001f;
            }
        }

        hasBloom &= _quality.EnableBloom;
        ulong shadowHash;
        ulong coloredLightHash = 0;
        ulong bloomHash = 0;
        ulong reflectionHash = 0;
        using (performance?.Measure("Presentation.LightingUploadHash", 0.1) ?? default)
        {
            shadowHash = LightingTextureContentHash.Compute(
                _shadowPixels.AsSpan(0, _quality.MaskPixelCount));
            if (hasColoredLight)
            {
                coloredLightHash = LightingTextureContentHash.Compute(
                    _coloredLightPixels.AsSpan(0, _quality.MaskPixelCount));
            }

            if (hasBloom)
            {
                bloomHash = LightingTextureContentHash.Compute(
                    _bloomPixels.AsSpan(0, _quality.MaskPixelCount));
            }

            if (hasReflection)
            {
                reflectionHash = LightingTextureContentHash.Compute(
                    _reflectionPixels.AsSpan(0, _quality.MaskPixelCount));
            }
        }

        var uploadShadow = _shadowContent.IsChanged(shadowHash);
        var uploadColoredLight = hasColoredLight &&
            (!_hasColoredLight || _coloredLightContent.IsChanged(coloredLightHash));
        var uploadBloom = hasBloom &&
            (!_hasBloom || _bloomContent.IsChanged(bloomHash));
        var uploadReflection = hasReflection &&
            (!_hasReflection || _reflectionContent.IsChanged(reflectionHash));
        LastTextureUploadCount = 0;
        var uploadCandidateCount = 1 +
            (hasColoredLight ? 1 : 0) +
            (hasBloom ? 1 : 0) +
            (hasReflection ? 1 : 0);
        using (performance?.Measure("Presentation.LightingGpuUpload", 0.5) ?? default)
        {
            if (uploadShadow)
            {
                _shadowUploadMap!.SetData(_shadowPixels);
                _shadowContent.Commit(shadowHash);
                LastTextureUploadCount++;
            }

            if (uploadColoredLight)
            {
                _coloredLightUploadMap!.SetData(_coloredLightPixels);
                _coloredLightContent.Commit(coloredLightHash);
                LastTextureUploadCount++;
            }

            if (uploadBloom)
            {
                _bloomUploadMap!.SetData(_bloomPixels);
                _bloomContent.Commit(bloomHash);
                LastTextureUploadCount++;
            }

            if (uploadReflection)
            {
                _reflectionUploadMap!.SetData(_reflectionPixels);
                _reflectionContent.Commit(reflectionHash);
                LastTextureUploadCount++;
            }
        }

        if (uploadShadow)
        {
            Rotate(ref _shadowMap, ref _shadowUploadMap, ref _shadowSpareMap);
        }

        if (uploadColoredLight)
        {
            Rotate(ref _coloredLightMap, ref _coloredLightUploadMap, ref _coloredLightSpareMap);
        }

        if (uploadBloom)
        {
            Rotate(ref _bloomMap, ref _bloomUploadMap, ref _bloomSpareMap);
        }

        if (uploadReflection)
        {
            Rotate(ref _reflectionMap, ref _reflectionUploadMap, ref _reflectionSpareMap);
        }

        LastTextureUploadSkippedCount = uploadCandidateCount - LastTextureUploadCount;
        TotalTextureUploadCount += LastTextureUploadCount;
        TotalTextureUploadSkippedCount += LastTextureUploadSkippedCount;

        _hasColoredLight = hasColoredLight;
        _hasBloom = hasBloom;
        _hasReflection = hasReflection;
        _preparedViewport = viewport;
        _hasPreparedFrame = true;

        var features = PresentationFeature.Lighting;
        if (_quality.AmbientOcclusionRadius > 0)
        {
            features |= PresentationFeature.AmbientOcclusion;
        }

        if (hasBloom)
        {
            features |= PresentationFeature.Bloom;
        }

        LastPassPlan = PresentationPassPlanner.Build(
            _quality,
            new PresentationPassRequest(
                features,
                pointLights.Length,
                RequestedReflectionSurfaces: 0,
                frame.BloomStrength,
                ReflectionStrength: 0f),
            _passes);
        return LastTelemetry;
    }

    public LightingBuildTelemetry PrepareFrame(
        World world,
        Camera2D camera,
        Rectangle viewport,
        in WorldTimeFrameSnapshot time,
        in LivingWorldFrameSnapshot livingWorld,
        RenderingSettings settings,
        long frameIndex,
        ReadOnlySpan<ScreenSpaceLight> pointLights,
        PerformanceProfiler? performance = null,
        int? surfaceTileY = null,
        Func<int, int>? surfaceHeightResolver = null)
    {
        var frame = PresentationSettingsAdapter.CreateLightingFrame(
            settings,
            world,
            camera,
            time,
            livingWorld,
            frameIndex);
        return PrepareFrame(
            world,
            camera,
            viewport,
            frame,
            pointLights,
            performance,
            surfaceTileY,
            surfaceHeightResolver);
    }

    public void Draw(
        RenderContext context,
        World world,
        Camera2D camera,
        float blendStrength = 1f,
        int? surfaceTileY = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        blendStrength = Math.Clamp(blendStrength, 0f, 1f);
        if (blendStrength <= 0.001f || context.ViewportBounds.IsEmpty)
        {
            return;
        }

        if (_wasConfigured && _quality.Tier == PresentationQualityTier.Disabled)
        {
            return;
        }

        if (_hasPreparedFrame &&
            _preparedViewport == context.ViewportBounds &&
            _shadowMap is not null &&
            _coloredLightMap is not null &&
            _bloomMap is not null)
        {
            DrawPrepared(context, blendStrength);
            return;
        }

        DrawLowQualityFallback(context, world, camera, blendStrength, surfaceTileY);
    }

    public void Dispose()
    {
        ReleaseResources();
        GC.SuppressFinalize(this);
    }

    internal static void ComposeLightMap(
        World world,
        int minTileX,
        int minTileY,
        int width,
        int height,
        float blendStrength,
        Span<Color> destination)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (width < 0 || height < 0 || (long)width * height > destination.Length)
        {
            throw new ArgumentException("Light-map destination is smaller than the requested area.", nameof(destination));
        }

        var surfaceY = world.Metadata.SpawnTile.Y;
        for (var localY = 0; localY < height; localY++)
        {
            var tileY = SaturatingAdd(minTileY, localY);
            var depth = Math.Clamp((tileY - (double)surfaceY) / 90d, 0d, 1d);
            var tint = Color.Lerp(new Color(3, 7, 12), new Color(9, 4, 16), (float)depth);
            for (var localX = 0; localX < width; localX++)
            {
                var tileX = SaturatingAdd(minTileX, localX);
                var light = world.IsInBounds(tileX, tileY) ? world.GetTile(tileX, tileY).Light : (byte)0;
                var darkness = Math.Clamp((255 - light) / 255f * blendStrength, 0f, 0.94f);
                destination[localY * width + localX] = Premultiply(tint, darkness);
            }
        }
    }

    private void DrawPrepared(RenderContext context, float blendStrength)
    {
        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            rasterizerState: RasterizerState.CullNone);
        context.SpriteBatch.Draw(_shadowMap!, _preparedViewport, Color.White * blendStrength);
        context.SpriteBatch.End();

        if (_hasColoredLight || _hasBloom || _hasReflection)
        {
            context.SpriteBatch.Begin(
                blendState: BlendState.Additive,
                samplerState: SamplerState.LinearClamp,
                rasterizerState: RasterizerState.CullNone);
            if (_hasColoredLight)
            {
                context.SpriteBatch.Draw(_coloredLightMap!, _preparedViewport, Color.White * blendStrength);
            }

            if (_hasBloom)
            {
                context.SpriteBatch.Draw(_bloomMap!, _preparedViewport, Color.White * blendStrength);
            }

            if (_hasReflection && _reflectionMap is not null)
            {
                context.SpriteBatch.Draw(_reflectionMap, _preparedViewport, Color.White * blendStrength);
            }

            context.SpriteBatch.End();
        }

        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
    }

    private static void DrawLowQualityFallback(
        RenderContext context,
        World world,
        Camera2D camera,
        float blendStrength,
        int? surfaceTileY)
    {
        var centerTile = CoordinateUtils.WorldToTile(camera.Position.X, camera.Position.Y);
        var light = world.TryGetTile(centerTile.X, centerTile.Y, out var tile)
            ? tile.Light / 255f
            : 0f;
        var surfaceY = surfaceTileY ?? world.Metadata.SpawnTile.Y;
        if (centerTile.Y <= surfaceY && (tile.Flags & TileFlags.Solid) == 0)
        {
            light = Math.Max(light, 0.82f);
        }
        var depth = Math.Clamp(
            (centerTile.Y - surfaceY) / 90f,
            0f,
            1f);
        var alpha = Math.Clamp((1f - light) * blendStrength, 0f, 0.82f);
        var tint = Color.Lerp(new Color(3, 7, 12), new Color(9, 4, 16), depth);
        context.SpriteBatch.Draw(context.Pixel, context.ViewportBounds, Premultiply(tint, alpha));
    }

    private bool ResourcesMatch(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        in PresentationQualityProfile profile)
    {
        return _shadowMap is { IsDisposed: false } &&
            _shadowUploadMap is { IsDisposed: false } &&
            _shadowSpareMap is { IsDisposed: false } &&
            (!_quality.EnableReflections ||
             _reflectionMap is { IsDisposed: false } &&
             _reflectionUploadMap is { IsDisposed: false } &&
             _reflectionSpareMap is { IsDisposed: false }) &&
            ReferenceEquals(_shadowMap.GraphicsDevice, graphicsDevice) &&
            _resourceViewport == viewport &&
            _quality == profile;
    }

    private void ReleaseResources()
    {
        _shadowMap?.Dispose();
        _coloredLightMap?.Dispose();
        _bloomMap?.Dispose();
        _reflectionMap?.Dispose();
        _shadowUploadMap?.Dispose();
        _coloredLightUploadMap?.Dispose();
        _bloomUploadMap?.Dispose();
        _reflectionUploadMap?.Dispose();
        _shadowSpareMap?.Dispose();
        _coloredLightSpareMap?.Dispose();
        _bloomSpareMap?.Dispose();
        _reflectionSpareMap?.Dispose();
        _shadowMap = null;
        _coloredLightMap = null;
        _bloomMap = null;
        _reflectionMap = null;
        _shadowUploadMap = null;
        _coloredLightUploadMap = null;
        _bloomUploadMap = null;
        _reflectionUploadMap = null;
        _shadowSpareMap = null;
        _coloredLightSpareMap = null;
        _bloomSpareMap = null;
        _reflectionSpareMap = null;
        _shadowPixels = Array.Empty<Color>();
        _coloredLightPixels = Array.Empty<Color>();
        _bloomPixels = Array.Empty<Color>();
        _reflectionPixels = Array.Empty<Color>();
        _reflectionSurfaces = Array.Empty<WaterReflectionSurface>();
        _shadowValues = Array.Empty<float>();
        _lightRed = Array.Empty<float>();
        _lightGreen = Array.Empty<float>();
        _lightBlue = Array.Empty<float>();
        _bloomValues = Array.Empty<float>();
        _scratch = Array.Empty<float>();
        _previousShadowValues = Array.Empty<float>();
        _previousLightRed = Array.Empty<float>();
        _previousLightGreen = Array.Empty<float>();
        _previousLightBlue = Array.Empty<float>();
        _previousVisibleWorld = default;
        _previousLightingFrameIndex = -1;
        _hasLightingHistory = false;
        LastTemporalTelemetry = default;
        _hasPreparedFrame = false;
        _hasColoredLight = false;
        _hasBloom = false;
        _hasReflection = false;
        LastTextureUploadCount = 0;
        LastTextureUploadSkippedCount = 0;
        TotalTextureUploadCount = 0;
        TotalTextureUploadSkippedCount = 0;
        _shadowContent.Reset();
        _coloredLightContent.Reset();
        _bloomContent.Reset();
        _reflectionContent.Reset();
        LastPassPlan = default;
        LastTelemetry = default;
        LastReflectionTelemetry = default;
        LastReflectionSurfaceTelemetry = default;
    }

    private static void Rotate(ref Texture2D? front, ref Texture2D? upload, ref Texture2D? spare)
    {
        var previousFront = front;
        front = upload;
        upload = spare;
        spare = previousFront;
    }

    private static Color Premultiply(Color tint, float alpha)
    {
        var alphaByte = (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255);
        return new Color(
            (byte)(tint.R * alphaByte / 255),
            (byte)(tint.G * alphaByte / 255),
            (byte)(tint.B * alphaByte / 255),
            alphaByte);
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
}
