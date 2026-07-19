using Game.Core.World;
using Game.Core.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Rendering.Effects;

public sealed class ScreenSpaceEffectsRenderer : IDisposable
{
    private readonly PresentationPassDescriptor[] _passes =
        new PresentationPassDescriptor[PresentationPassPlanner.MaximumPassCount];
    private RenderTarget2D? _sceneColor;
    private RenderTarget2D? _blurPing;
    private RenderTarget2D? _blurPong;
    private Texture2D? _preparedBlur;
    private WaterReflectionSurface[] _surfaces = Array.Empty<WaterReflectionSurface>();
    private PresentationQualityProfile _quality;
    private Rectangle _viewport;
    private long _frameIndex;
    private bool _captureActive;
    private bool _sceneCaptureRequired;
    private bool _hasCapturedScene;
    private bool _hasPreparedBlur;

    public bool ResourcesPrepared => _sceneColor is not null;

    public bool ShouldCaptureScene => ResourcesPrepared && (SurfaceCount > 0 || _sceneCaptureRequired);

    public bool HasCapturedScene => ResourcesPrepared && _hasCapturedScene;

    public int SurfaceCount { get; private set; }

    public PresentationPassPlan LastPassPlan { get; private set; }

    public WaterReflectionPlanTelemetry LastTelemetry { get; private set; }

    public BackdropBlurPlan LastBackdropBlurPlan { get; private set; }

    public ReadOnlySpan<WaterReflectionSurface> Surfaces => _surfaces.AsSpan(0, SurfaceCount);

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

    public void PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        in PresentationQualityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        var viewportArea = Math.Max(0L, (long)viewport.Width * viewport.Height);
        if (profile.Tier == PresentationQualityTier.Disabled ||
            viewportArea > profile.Budget.MaxSceneCapturePixels)
        {
            ReleaseResources();
            _quality = profile;
            _viewport = viewport;
            return;
        }

        if (_sceneColor is { IsDisposed: false } &&
            ReferenceEquals(_sceneColor.GraphicsDevice, graphicsDevice) &&
            _quality == profile &&
            _viewport == viewport)
        {
            return;
        }

        ReleaseResources();
        _quality = profile;
        _viewport = viewport;
        _sceneColor = new RenderTarget2D(
            graphicsDevice,
            viewport.Width,
            viewport.Height,
            mipMap: false,
            SurfaceFormat.Color,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.DiscardContents);
        var blurResources = BackdropBlurPlanner.Build(profile.Tier, viewport, radiusPixels: 8);
        if (blurResources.IsEnabled)
        {
            _blurPing = CreateTransientTarget(graphicsDevice, blurResources.TargetSize);
            _blurPong = CreateTransientTarget(graphicsDevice, blurResources.TargetSize);
        }

        _surfaces = new WaterReflectionSurface[profile.Budget.MaxReflectionSurfaces];
    }

    public PresentationQualityProfile PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        RenderingSettings settings)
    {
        var profile = PresentationSettingsAdapter.Create(settings, viewport).Reflections;
        PrepareResources(graphicsDevice, viewport, profile);
        return profile;
    }

    public PresentationQualityProfile PrepareResources(
        GraphicsDevice graphicsDevice,
        Rectangle viewport,
        RenderingSettings rendering,
        UiSettings ui)
    {
        ArgumentNullException.ThrowIfNull(rendering);
        ArgumentNullException.ThrowIfNull(ui);
        var configuration = PresentationSettingsAdapter.Create(rendering, viewport);
        var uiTier = ui.BackdropBlurStrength > 0.001f
            ? configuration.UiEffects
            : PresentationQualityTier.Disabled;
        var tier = (PresentationQualityTier)Math.Max(
            (int)configuration.Reflections.Tier,
            (int)uiTier);
        var profile = PresentationQualityProfile.Create(tier, viewport) with
        {
            EnableReflections = configuration.Reflections.EnableReflections
        };
        PrepareResources(graphicsDevice, viewport, profile);
        return profile;
    }

    public void SetSceneCaptureRequired(bool required)
    {
        _sceneCaptureRequired = required;
    }

    public WaterReflectionPlanTelemetry PrepareFrame(
        World world,
        Camera2D camera,
        Rectangle viewport,
        long frameIndex,
        float reflectionStrength)
    {
        var palette = WaterPresentationPaletteCatalog.ClearWater;
        return PrepareFrame(world, camera, viewport, frameIndex, reflectionStrength, palette);
    }

    public WaterReflectionPlanTelemetry PrepareFrame(
        World world,
        Camera2D camera,
        Rectangle viewport,
        long frameIndex,
        float reflectionStrength,
        in WaterPresentationPalette palette)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        reflectionStrength = Math.Clamp(reflectionStrength, 0f, 1f);
        if (!ResourcesPrepared)
        {
            SurfaceCount = 0;
            LastPassPlan = default;
            LastTelemetry = default;
            return LastTelemetry;
        }

        if (reflectionStrength <= 0.001f)
        {
            SurfaceCount = 0;
            LastPassPlan = default;
            LastTelemetry = default;
            return LastTelemetry;
        }

        if (viewport != _viewport)
        {
            throw new InvalidOperationException(
                "Screen-space effect resources do not match the viewport. Call PrepareResources first.");
        }

        _frameIndex = Math.Max(0, frameIndex);
        LastTelemetry = _quality.EnableReflections
            ? WaterReflectionSurfacePlanner.Build(
                world,
                camera,
                viewport,
                _quality,
                palette,
                _surfaces)
            : default;
        SurfaceCount = LastTelemetry.SurfaceCount;
        LastPassPlan = PresentationPassPlanner.Build(
            _quality,
            new PresentationPassRequest(
                PresentationFeature.Reflections,
                RequestedPointLights: 0,
                RequestedReflectionSurfaces: SurfaceCount,
                BloomStrength: 0f,
                reflectionStrength),
            _passes);
        return LastTelemetry;
    }

    public WaterReflectionPlanTelemetry PrepareFrame(
        World world,
        Camera2D camera,
        Rectangle viewport,
        long frameIndex,
        RenderingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return PrepareFrame(
            world,
            camera,
            viewport,
            frameIndex,
            settings.ScreenSpaceReflections ? settings.ReflectionStrength : 0f);
    }

    public WaterReflectionPlanTelemetry PrepareFrame(
        World world,
        Camera2D camera,
        Rectangle viewport,
        long frameIndex,
        RenderingSettings settings,
        in WaterPresentationPalette palette)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return PrepareFrame(
            world,
            camera,
            viewport,
            frameIndex,
            settings.ScreenSpaceReflections ? settings.ReflectionStrength : 0f,
            palette);
    }

    public bool BeginSceneCapture(RenderContext context, Color clearColor, bool captureThisFrame = true)
    {
        if (!captureThisFrame || !ShouldCaptureScene || context.ViewportBounds != _viewport)
        {
            return false;
        }

        if (_captureActive)
        {
            throw new InvalidOperationException("Scene capture is already active.");
        }

        context.SpriteBatch.End();
        context.GraphicsDevice.SetRenderTarget(_sceneColor);
        context.GraphicsDevice.Clear(clearColor);
        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
        _captureActive = true;
        return true;
    }

    public void EndSceneCaptureAndComposite(
        RenderContext context,
        float reflectionStrength,
        int backdropBlurRadiusPixels = 0)
    {
        if (!_captureActive || _sceneColor is null)
        {
            throw new InvalidOperationException("Scene capture is not active.");
        }

        reflectionStrength = Math.Clamp(reflectionStrength, 0f, 1f);
        context.SpriteBatch.End();
        if (_sceneCaptureRequired && backdropBlurRadiusPixels > 0)
        {
            PrepareBackdropBlur(context, backdropBlurRadiusPixels);
        }
        else
        {
            context.GraphicsDevice.SetRenderTarget(null);
            _hasPreparedBlur = false;
            _preparedBlur = null;
            LastBackdropBlurPlan = default;
        }

        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
        context.SpriteBatch.Draw(_sceneColor, _viewport, Color.White);
        context.SpriteBatch.End();

        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            rasterizerState: RasterizerState.CullNone);
        DrawReflections(context, _sceneColor, reflectionStrength);
        context.SpriteBatch.End();

        context.SpriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
        DrawWetHighlights(context, reflectionStrength);
        context.SpriteBatch.End();

        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
        _hasCapturedScene = true;
        _captureActive = false;
    }

    public void DrawReusedSceneEffects(RenderContext context, float reflectionStrength)
    {
        if (!_hasCapturedScene || _sceneColor is null || SurfaceCount == 0)
        {
            return;
        }

        reflectionStrength = Math.Clamp(reflectionStrength, 0f, 1f);
        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            rasterizerState: RasterizerState.CullNone);
        DrawReflections(context, _sceneColor, reflectionStrength);
        context.SpriteBatch.End();

        context.SpriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
        DrawWetHighlights(context, reflectionStrength);
        context.SpriteBatch.End();

        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
    }

    public void DrawPreparedSurfaceHighlights(RenderContext context, float reflectionStrength)
    {
        DrawWetHighlights(context, Math.Clamp(reflectionStrength, 0f, 1f));
    }

    public void DrawPreparedBackdropBlur(RenderContext context, float strength, int radiusPixels)
    {
        if (_sceneColor is null || strength <= 0.001f || radiusPixels <= 0)
        {
            return;
        }

        strength = Math.Clamp(strength, 0f, 1f);
        if (_hasPreparedBlur && _preparedBlur is not null)
        {
            var opacity = Math.Clamp(strength * 0.72f, 0.08f, 0.72f);
            context.SpriteBatch.End();
            context.SpriteBatch.Begin(
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                rasterizerState: RasterizerState.CullNone);
            context.SpriteBatch.Draw(_preparedBlur, _viewport, Color.White * opacity);
            context.SpriteBatch.End();
            context.SpriteBatch.Begin(
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.PointClamp,
                rasterizerState: RasterizerState.CullNone);
            return;
        }

        var radius = Math.Clamp(radiusPixels, 1, 12);
        var tap = Math.Clamp(strength * 0.12f, 0.02f, 0.12f);
        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            rasterizerState: RasterizerState.CullNone);
        DrawBlurTap(context, -radius, 0, tap);
        DrawBlurTap(context, radius, 0, tap);
        DrawBlurTap(context, 0, -radius, tap);
        DrawBlurTap(context, 0, radius, tap);
        DrawBlurTap(context, -radius / 2, -radius / 2, tap);
        DrawBlurTap(context, radius / 2, -radius / 2, tap);
        DrawBlurTap(context, -radius / 2, radius / 2, tap);
        DrawBlurTap(context, radius / 2, radius / 2, tap);
        context.SpriteBatch.End();
        context.SpriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            rasterizerState: RasterizerState.CullNone);
    }

    public void Dispose()
    {
        ReleaseResources();
        GC.SuppressFinalize(this);
    }

    private void DrawReflections(RenderContext context, Texture2D sceneColor, float strength)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        var stripBudget = Math.Max(1, _quality.Budget.MaxReflectionStripsPerSurface);
        var amplitude = _quality.Tier switch
        {
            PresentationQualityTier.High => 4f,
            PresentationQualityTier.Medium => 2f,
            _ => 1f
        };

        for (var surfaceIndex = 0; surfaceIndex < SurfaceCount; surfaceIndex++)
        {
            ref readonly var surface = ref _surfaces[surfaceIndex];
            var bounds = surface.ScreenBounds;
            var strips = Math.Min(stripBudget, Math.Max(1, bounds.Height));
            var stripHeight = Math.Max(1, DivideRoundUp(bounds.Height, strips));
            for (var strip = 0; strip < strips; strip++)
            {
                var destinationY = bounds.Y + strip * stripHeight;
                var height = Math.Min(stripHeight, bounds.Bottom - destinationY);
                if (height <= 0)
                {
                    break;
                }

                var phase = (_frameIndex * 0.075f) + surface.Phase * 0.000013f + strip * 1.7f;
                var offset = (int)MathF.Round(MathF.Sin(phase) * amplitude);
                var sourceY = Math.Clamp(
                    bounds.Y - (strip + 1) * stripHeight,
                    _viewport.Top,
                    Math.Max(_viewport.Top, _viewport.Bottom - height));
                var source = new Rectangle(
                    bounds.X - _viewport.X,
                    sourceY - _viewport.Y,
                    bounds.Width,
                    height);
                var destination = new Rectangle(bounds.X + offset, destinationY, bounds.Width, height);
                var alpha = strength * surface.Reflectivity;
                context.SpriteBatch.Draw(
                    sceneColor,
                    destination,
                    source,
                    surface.Tint * alpha,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.FlipVertically,
                    0f);
            }
        }
    }

    private void DrawWetHighlights(RenderContext context, float strength)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        for (var surfaceIndex = 0; surfaceIndex < SurfaceCount; surfaceIndex++)
        {
            ref readonly var surface = ref _surfaces[surfaceIndex];
            var alpha = strength * (surface.Kind == ReflectionSurfaceKind.Water ? 0.18f : 0.1f);
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(
                    surface.ScreenBounds.X,
                    surface.ScreenBounds.Y,
                    surface.ScreenBounds.Width,
                    Math.Min(2, surface.ScreenBounds.Height)),
                surface.Tint * alpha);
        }
    }

    private void DrawBlurTap(RenderContext context, int offsetX, int offsetY, float opacity)
    {
        context.SpriteBatch.Draw(
            _sceneColor!,
            new Rectangle(_viewport.X + offsetX, _viewport.Y + offsetY, _viewport.Width, _viewport.Height),
            Color.White * opacity);
    }

    private void PrepareBackdropBlur(RenderContext context, int radiusPixels)
    {
        var plan = BackdropBlurPlanner.Build(_quality.Tier, _viewport, radiusPixels);
        if (!plan.IsEnabled || _sceneColor is null || _blurPing is null || _blurPong is null)
        {
            context.GraphicsDevice.SetRenderTarget(null);
            _hasPreparedBlur = false;
            _preparedBlur = null;
            LastBackdropBlurPlan = default;
            return;
        }

        var targetBounds = new Rectangle(0, 0, plan.TargetSize.X, plan.TargetSize.Y);
        context.GraphicsDevice.SetRenderTarget(_blurPing);
        context.GraphicsDevice.Clear(Color.Transparent);
        context.SpriteBatch.Begin(
            blendState: BlendState.Opaque,
            samplerState: SamplerState.LinearClamp,
            rasterizerState: RasterizerState.CullNone);
        context.SpriteBatch.Draw(_sceneColor, targetBounds, Color.White);
        context.SpriteBatch.End();

        Texture2D source = _blurPing;
        RenderTarget2D destination = _blurPong;
        for (var iteration = 0; iteration < plan.Iterations; iteration++)
        {
            context.GraphicsDevice.SetRenderTarget(destination);
            context.GraphicsDevice.Clear(Color.Transparent);
            context.SpriteBatch.Begin(
                blendState: BlendState.Additive,
                samplerState: SamplerState.LinearClamp,
                rasterizerState: RasterizerState.CullNone);
            DrawKawaseTap(context, source, targetBounds, -plan.RadiusPerIteration, -plan.RadiusPerIteration);
            DrawKawaseTap(context, source, targetBounds, plan.RadiusPerIteration, -plan.RadiusPerIteration);
            DrawKawaseTap(context, source, targetBounds, -plan.RadiusPerIteration, plan.RadiusPerIteration);
            DrawKawaseTap(context, source, targetBounds, plan.RadiusPerIteration, plan.RadiusPerIteration);
            context.SpriteBatch.End();

            source = destination;
            destination = ReferenceEquals(destination, _blurPong) ? _blurPing : _blurPong;
        }

        context.GraphicsDevice.SetRenderTarget(null);
        _preparedBlur = source;
        _hasPreparedBlur = true;
        LastBackdropBlurPlan = plan;
    }

    private static void DrawKawaseTap(
        RenderContext context,
        Texture2D source,
        Rectangle targetBounds,
        int offsetX,
        int offsetY)
    {
        context.SpriteBatch.Draw(
            source,
            new Rectangle(
                targetBounds.X + offsetX,
                targetBounds.Y + offsetY,
                targetBounds.Width,
                targetBounds.Height),
            Color.White * 0.25f);
    }

    private static RenderTarget2D CreateTransientTarget(GraphicsDevice graphicsDevice, Point size)
    {
        return new RenderTarget2D(
            graphicsDevice,
            size.X,
            size.Y,
            mipMap: false,
            SurfaceFormat.Color,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.DiscardContents);
    }

    private void ReleaseResources()
    {
        if (_captureActive && _sceneColor is not null)
        {
            _sceneColor.GraphicsDevice.SetRenderTarget(null);
        }

        _sceneColor?.Dispose();
        _blurPing?.Dispose();
        _blurPong?.Dispose();
        _sceneColor = null;
        _blurPing = null;
        _blurPong = null;
        _preparedBlur = null;
        _surfaces = Array.Empty<WaterReflectionSurface>();
        SurfaceCount = 0;
        LastPassPlan = default;
        LastTelemetry = default;
        _captureActive = false;
        _sceneCaptureRequired = false;
        _hasCapturedScene = false;
        _hasPreparedBlur = false;
        LastBackdropBlurPlan = default;
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return value / divisor + (value % divisor == 0 ? 0 : 1);
    }
}
