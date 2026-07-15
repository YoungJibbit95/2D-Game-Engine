using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core;
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
    private Color[] _shadowPixels = Array.Empty<Color>();
    private Color[] _coloredLightPixels = Array.Empty<Color>();
    private Color[] _bloomPixels = Array.Empty<Color>();
    private float[] _shadowValues = Array.Empty<float>();
    private float[] _lightRed = Array.Empty<float>();
    private float[] _lightGreen = Array.Empty<float>();
    private float[] _lightBlue = Array.Empty<float>();
    private float[] _bloomValues = Array.Empty<float>();
    private float[] _scratch = Array.Empty<float>();
    private PresentationQualityProfile _quality;
    private Rectangle _resourceViewport;
    private Rectangle _preparedViewport;
    private bool _hasPreparedFrame;
    private bool _wasConfigured;

    public bool ResourcesPrepared => _shadowMap is not null;

    public PresentationQualityProfile Quality => _quality;

    public PresentationPassPlan LastPassPlan { get; private set; }

    public LightingBuildTelemetry LastTelemetry { get; private set; }

    public ReadOnlySpan<PresentationPassDescriptor> LastPasses =>
        _passes.AsSpan(0, LastPassPlan.PassCount);

    public void PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        PresentationQualityTier quality,
        PresentationBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        var profile = PresentationQualityProfile.Create(quality, viewport, budget);
        PrepareResources(graphicsDevice, viewport, profile);
    }

    public PresentationQualityProfile PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        RenderingSettings settings)
    {
        var profile = PresentationSettingsAdapter.Create(settings, viewport).Lighting;
        PrepareResources(graphicsDevice, viewport, profile);
        return profile;
    }

    public void PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        in PresentationQualityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
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

        var count = profile.MaskPixelCount;
        _shadowPixels = new Color[count];
        _coloredLightPixels = new Color[count];
        _bloomPixels = new Color[count];
        _shadowValues = new float[count];
        _lightRed = new float[count];
        _lightGreen = new float[count];
        _lightBlue = new float[count];
        _bloomValues = new float[count];
        _scratch = new float[count];
    }

    public LightingBuildTelemetry PrepareFrame(
        World world,
        Camera2D camera,
        Rectangle viewport,
        in LightingFrameParameters frame,
        ReadOnlySpan<ScreenSpaceLight> pointLights)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        if (!ResourcesPrepared)
        {
            _hasPreparedFrame = false;
            LastPassPlan = default;
            LastTelemetry = default;
            return LastTelemetry;
        }

        if (viewport != _resourceViewport)
        {
            throw new InvalidOperationException(
                "Lighting resources do not match the requested viewport. Call PrepareResources before PrepareFrame.");
        }

        LastTelemetry = TileRayCastShadowMaskBuilder.Build(
            world,
            camera.VisibleWorldRect,
            _quality,
            frame,
            pointLights,
            _shadowValues,
            _lightRed,
            _lightGreen,
            _lightBlue,
            _bloomValues,
            _scratch);

        var caveTint = Color.Lerp(
            new Color(3, 7, 12),
            new Color(10, 4, 18),
            float.IsFinite(frame.CaveBlend) ? Math.Clamp(frame.CaveBlend, 0f, 1f) : 0f);
        for (var index = 0; index < _quality.MaskPixelCount; index++)
        {
            _shadowPixels[index] = Premultiply(caveTint, _shadowValues[index]);
            _coloredLightPixels[index] = new Color(
                _lightRed[index],
                _lightGreen[index],
                _lightBlue[index],
                1f);

            var bloom = _bloomValues[index];
            _bloomPixels[index] = new Color(
                Math.Clamp(bloom, 0f, 1f),
                Math.Clamp(bloom * 0.72f, 0f, 1f),
                Math.Clamp(bloom * 0.42f, 0f, 1f),
                1f);
        }

        _shadowMap!.SetData(_shadowPixels);
        _coloredLightMap!.SetData(_coloredLightPixels);
        _bloomMap!.SetData(_bloomPixels);
        _preparedViewport = viewport;
        _hasPreparedFrame = true;

        var features = PresentationFeature.Lighting | PresentationFeature.AmbientOcclusion;
        if (frame.BloomStrength > 0.001f)
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
        ReadOnlySpan<ScreenSpaceLight> pointLights)
    {
        var frame = PresentationSettingsAdapter.CreateLightingFrame(
            settings,
            world,
            camera,
            time,
            livingWorld,
            frameIndex);
        return PrepareFrame(world, camera, viewport, frame, pointLights);
    }

    public void Draw(RenderContext context, World world, Camera2D camera, float blendStrength = 1f)
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

        DrawLowQualityFallback(context, world, camera, blendStrength);
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

        context.SpriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.LinearClamp,
            rasterizerState: RasterizerState.CullNone);
        context.SpriteBatch.Draw(_coloredLightMap!, _preparedViewport, Color.White * blendStrength);
        if (_quality.EnableBloom)
        {
            context.SpriteBatch.Draw(_bloomMap!, _preparedViewport, Color.White * blendStrength);
        }

        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
    }

    private static void DrawLowQualityFallback(
        RenderContext context,
        World world,
        Camera2D camera,
        float blendStrength)
    {
        var centerTile = CoordinateUtils.WorldToTile(camera.Position.X, camera.Position.Y);
        var light = world.TryGetTile(centerTile.X, centerTile.Y, out var tile)
            ? tile.Light / 255f
            : 0f;
        var depth = Math.Clamp(
            (centerTile.Y - world.Metadata.SpawnTile.Y) / 90f,
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
            ReferenceEquals(_shadowMap.GraphicsDevice, graphicsDevice) &&
            _resourceViewport == viewport &&
            _quality == profile;
    }

    private void ReleaseResources()
    {
        _shadowMap?.Dispose();
        _coloredLightMap?.Dispose();
        _bloomMap?.Dispose();
        _shadowMap = null;
        _coloredLightMap = null;
        _bloomMap = null;
        _shadowPixels = Array.Empty<Color>();
        _coloredLightPixels = Array.Empty<Color>();
        _bloomPixels = Array.Empty<Color>();
        _shadowValues = Array.Empty<float>();
        _lightRed = Array.Empty<float>();
        _lightGreen = Array.Empty<float>();
        _lightBlue = Array.Empty<float>();
        _bloomValues = Array.Empty<float>();
        _scratch = Array.Empty<float>();
        _hasPreparedFrame = false;
        LastPassPlan = default;
        LastTelemetry = default;
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
