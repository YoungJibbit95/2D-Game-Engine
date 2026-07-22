using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core;
using Game.Core.Runtime;
using Game.Core.Settings;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class PixelAtmosphereRenderer
{
    private readonly PresentationPassDescriptor[] _passes =
        new PresentationPassDescriptor[PresentationPassPlanner.MaximumPassCount];
    private AtmosphereFrame _preparedFrame;
    private bool _hasPreparedFrame;

    public PresentationPassPlan LastPassPlan { get; private set; }

    public AtmosphereFrame PrepareFrame(
        World world,
        Camera2D camera,
        in WorldTimeFrameSnapshot time,
        in LivingWorldFrameSnapshot livingWorld,
        in RenderingSettings settings,
        PresentationQualityTier quality,
        long frameIndex)
    {
        return PrepareFrame(
            world,
            camera,
            time,
            livingWorld,
            settings,
            new Rectangle(0, 0, 1280, 720),
            quality,
            frameIndex);
    }

    public AtmosphereFrame PrepareFrame(
        World world,
        Camera2D camera,
        in WorldTimeFrameSnapshot time,
        in LivingWorldFrameSnapshot livingWorld,
        in RenderingSettings settings,
        Rectangle viewport,
        PresentationQualityTier quality,
        long frameIndex)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        var profile = ResolveProfile(world, camera, time, livingWorld);
        _preparedFrame = new AtmosphereFrame(
            profile,
            quality,
            Math.Max(0, frameIndex),
            Math.Clamp(settings.ColorGradeIntensity, 0f, 1f),
            Math.Clamp(settings.VignetteStrength, 0f, 1f),
            settings.PostProcessingEnabled);
        _hasPreparedFrame = true;

        var qualityProfile = PresentationQualityProfile.Create(
            quality,
            viewport);
        LastPassPlan = settings.PostProcessingEnabled
            ? PresentationPassPlanner.Build(
                qualityProfile,
                new PresentationPassRequest(
                    PresentationFeature.Atmosphere,
                    RequestedPointLights: 0,
                    RequestedReflectionSurfaces: 0,
                    BloomStrength: 0f,
                    ReflectionStrength: 0f),
                _passes)
            : default;
        return _preparedFrame;
    }

    public void Draw(
        RenderContext context,
        World world,
        Camera2D camera,
        WorldTimeFrameSnapshot time,
        LivingWorldFrameSnapshot livingWorld,
        RenderingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(settings);
        var quality = PresentationSettingsAdapter.ResolveTier(settings.LightingQuality);
        var fallbackFrameIndex = Math.Max(0L, (long)Math.Floor(context.Time.TotalSeconds * 60d));
        var frame = new AtmosphereFrame(
            ResolveProfile(world, camera, time, livingWorld),
            quality,
            fallbackFrameIndex,
            Math.Clamp(settings.ColorGradeIntensity, 0f, 1f),
            Math.Clamp(settings.VignetteStrength, 0f, 1f),
            settings.PostProcessingEnabled);
        DrawFrame(context, camera, frame);
    }

    public void DrawPrepared(RenderContext context, Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (_hasPreparedFrame)
        {
            DrawFrame(context, camera, _preparedFrame);
        }
    }

    internal static AtmosphereProfile ResolveProfile(
        World world,
        Camera2D camera,
        in WorldTimeFrameSnapshot time,
        in LivingWorldFrameSnapshot livingWorld)
    {
        var surfaceTileY = livingWorld.SurfaceTileY > 0
            ? livingWorld.SurfaceTileY
            : world.Metadata.SpawnTile.Y;
        var cameraTileY = camera.Position.Y / GameConstants.TileSize;
        var depthProgress = Math.Clamp((cameraTileY - surfaceTileY - 4f) / 52f, 0f, 1f);
        var depth = depthProgress * depthProgress * (3f - 2f * depthProgress);
        if (livingWorld.IsUnderground &&
            (!string.IsNullOrWhiteSpace(livingWorld.SubBiomeId) ||
             !string.IsNullOrWhiteSpace(livingWorld.CaveProfileId)))
        {
            depth = Math.Max(depth, 0.28f);
        }

        var night = SolarIlluminationCurve.ResolveNightBlend((float)time.NormalizedTimeOfDay);
        var daylightTint = TileRayCastShadowMaskBuilder.ResolveDaylightColor((float)time.NormalizedTimeOfDay);
        var surfaceGrade = Color.Lerp(new Color(30, 52, 78), new Color(24, 38, 78), night);
        surfaceGrade = Color.Lerp(surfaceGrade, daylightTint, MathHelper.Lerp(0.12f, 0.055f, night));
        var biomeGrade = ResolveBiomeGrade(livingWorld.BiomeId, livingWorld.SubBiomeId);
        surfaceGrade = Color.Lerp(surfaceGrade, biomeGrade, 0.2f);
        var deepGrade = Color.Lerp(new Color(25, 31, 48), new Color(35, 20, 48), depth);
        var weatherFog = livingWorld.Weather switch
        {
            Game.Core.Weather.WeatherKind.Fog => livingWorld.WeatherIntensity * 0.18f,
            Game.Core.Weather.WeatherKind.Rain => livingWorld.WeatherIntensity * 0.08f,
            Game.Core.Weather.WeatherKind.Storm => livingWorld.WeatherIntensity * 0.12f,
            Game.Core.Weather.WeatherKind.Snow => livingWorld.WeatherIntensity * 0.025f,
            Game.Core.Weather.WeatherKind.Blizzard => livingWorld.WeatherIntensity * 0.065f,
            _ => 0f
        };
        var eventGrade = livingWorld.IsWorldEventActive
            ? Math.Clamp(livingWorld.WorldEventIntensity, 0f, 1f) * 0.06f
            : 0f;
        return new AtmosphereProfile(
            Color.Lerp(
                Color.Lerp(surfaceGrade, deepGrade, depth),
                new Color(104, 53, 116),
                eventGrade),
            GradeStrength: Math.Clamp(
                0.07f + night * 0.055f + depth * 0.13f +
                (1f - livingWorld.AmbientLight) * 0.04f,
                0f,
                0.34f),
            FogStrength: Math.Clamp(
                (depth - 0.18f) * 0.32f + weatherFog + livingWorld.FogDensity * 0.12f,
                0f,
                0.38f),
            FogColor: Color.Lerp(new Color(40, 55, 68), new Color(55, 39, 68), depth));
    }

    private static void DrawFrame(RenderContext context, Camera2D camera, in AtmosphereFrame frame)
    {
        if (!frame.Enabled || frame.Quality == PresentationQualityTier.Disabled)
        {
            return;
        }

        DrawColorGrade(
            context,
            frame.Profile.GradeColor,
            frame.ColorGradeIntensity * frame.Profile.GradeStrength);
        DrawFog(context, camera, frame);
        DrawVignette(context, frame.VignetteStrength, frame.Quality);
    }

    private static Color ResolveBiomeGrade(string? biomeId, string? subBiomeId)
    {
        var identity = subBiomeId ?? biomeId;
        if (identity?.Contains("mushroom", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new Color(82, 50, 104);
        }

        if (identity?.Contains("crystal", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new Color(54, 88, 116);
        }

        return identity?.Contains("meadow", StringComparison.OrdinalIgnoreCase) == true
            ? new Color(68, 105, 79)
            : new Color(45, 84, 70);
    }

    private static void DrawColorGrade(RenderContext context, Color color, float strength)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        context.SpriteBatch.Draw(
            context.Pixel,
            context.ViewportBounds,
            new Color(color, Math.Clamp(strength, 0f, 0.3f)));
    }

    private static void DrawFog(
        RenderContext context,
        Camera2D camera,
        in AtmosphereFrame frame)
    {
        var strength = frame.Profile.FogStrength * frame.ColorGradeIntensity;
        if (strength <= 0.002f)
        {
            return;
        }

        var bandCount = frame.Quality switch
        {
            PresentationQualityTier.High => 6,
            PresentationQualityTier.Medium => 4,
            _ => 2
        };
        var time = frame.FrameIndex / 60f;
        var bandHeight = Math.Max(16, context.ViewportBounds.Height / Math.Max(4, bandCount + 3));
        for (var band = 0; band < bandCount; band++)
        {
            var drift = MathF.Sin(time * (0.12f + band * 0.025f) + camera.Position.X * 0.0007f + band) * 14f;
            var y = context.ViewportBounds.Y +
                context.ViewportBounds.Height / 3 +
                band * bandHeight / 2 +
                (int)MathF.Round(drift);
            var bounds = new Rectangle(
                context.ViewportBounds.X - 8,
                y,
                context.ViewportBounds.Width + 16,
                bandHeight);
            context.SpriteBatch.Draw(
                context.Pixel,
                bounds,
                new Color(
                    frame.Profile.FogColor,
                    strength * Math.Max(0.12f, 0.52f - band * 0.055f)));
        }
    }

    private static void DrawVignette(
        RenderContext context,
        float strength,
        PresentationQualityTier quality)
    {
        strength = Math.Clamp(strength, 0f, 1f);
        if (strength <= 0.001f || context.ViewportBounds.Width <= 0 || context.ViewportBounds.Height <= 0)
        {
            return;
        }

        var steps = quality switch
        {
            PresentationQualityTier.High => 7,
            PresentationQualityTier.Medium => 5,
            _ => 3
        };
        var maxThickness = Math.Max(1, Math.Min(context.ViewportBounds.Width, context.ViewportBounds.Height) / 10);
        var thickness = Math.Max(1, maxThickness / steps);
        for (var step = 0; step < steps; step++)
        {
            var inset = step * thickness;
            var width = context.ViewportBounds.Width - inset * 2;
            var height = context.ViewportBounds.Height - inset * 2;
            if (width <= 0 || height <= 0)
            {
                break;
            }

            var progress = (step + 1f) / steps;
            var alpha = strength * (1f - progress * 0.72f) * 0.12f;
            var color = new Color(2, 4, 8, alpha);
            var x = context.ViewportBounds.X + inset;
            var y = context.ViewportBounds.Y + inset;
            var edge = Math.Min(thickness, Math.Min(width, height));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, width, edge), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y + height - edge, width, edge), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, edge, height), color);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + width - edge, y, edge, height), color);
        }
    }

    public readonly record struct AtmosphereFrame(
        AtmosphereProfile Profile,
        PresentationQualityTier Quality,
        long FrameIndex,
        float ColorGradeIntensity,
        float VignetteStrength,
        bool Enabled);

    public readonly record struct AtmosphereProfile(
        Color GradeColor,
        float GradeStrength,
        float FogStrength,
        Color FogColor);
}
