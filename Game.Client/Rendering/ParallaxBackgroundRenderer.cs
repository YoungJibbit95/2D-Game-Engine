using Game.Client.Rendering.Effects;
using Game.Core.Runtime;
using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class ParallaxBackgroundRenderer
{
    private readonly ParallaxLayerDescriptor[] _layers =
        new ParallaxLayerDescriptor[ParallaxLayerPlanner.MaximumLayerCount];

    public string SurfaceSpriteId { get; set; } = "world/backgrounds/forest_parallax_layer";

    public string CaveSpriteId { get; set; } = "world/backgrounds/cave_parallax_layer";

    public float SurfaceParallax { get; set; } = 0.18f;

    public float CaveParallax { get; set; } = 0.08f;

    public float Opacity { get; set; } = 0.92f;

    public void Draw(
        RenderContext context,
        ClientTextureRegistry? textures,
        Camera2D camera,
        World world,
        bool isNight,
        LivingWorldFrameSnapshot livingWorld)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(world);
        var spawnY = world.Metadata.SpawnTile.Y * Game.Core.GameConstants.TileSize;
        var cameraDepth = camera.Position.Y - spawnY;
        var scene = ParallaxLayerPlanner.Build(
            livingWorld,
            isNight,
            cameraDepth,
            SurfaceParallax,
            CaveParallax,
            SurfaceSpriteId,
            CaveSpriteId,
            _layers);
        DrawGradientSky(context, scene.UndergroundBlend, isNight, livingWorld);
        DrawWeatherDepth(context, camera, scene.UndergroundBlend, livingWorld);

        if (textures is null)
        {
            return;
        }

        for (var index = 0; index < scene.LayerCount; index++)
        {
            TryDrawLayer(context, textures, camera, spawnY, _layers[index]);
        }
    }

    private bool TryDrawLayer(
        RenderContext context,
        ClientTextureRegistry textures,
        Camera2D camera,
        float spawnY,
        in ParallaxLayerDescriptor layer)
    {
        var sprite = textures.Get(layer.SpriteId);
        var source = sprite.SourceRectangle;
        if (sprite.IsPlaceholder || source.Width <= 0 || source.Height <= 0)
        {
            return false;
        }

        var alpha = Math.Clamp(layer.Opacity * Opacity, 0f, 1f);
        if (alpha <= 0.001f)
        {
            return false;
        }

        var viewportScale = Math.Max(1f, MathF.Ceiling(context.ViewportBounds.Height / 320f));
        var scale = Math.Clamp(viewportScale * layer.ScaleMultiplier, 0.5f, 8f);
        var width = Math.Max(8, SaturatingRound(source.Width * scale));
        var height = Math.Max(8, SaturatingRound(source.Height * scale));
        var verticalScroll = (camera.Position.Y - spawnY) * layer.VerticalParallax;
        var y = SaturatingRound(
            context.ViewportBounds.Bottom - height - 72f + layer.VerticalOffset - verticalScroll);
        var scroll = camera.Position.X * layer.HorizontalParallax;
        var scrollPixels = SaturatingRound(scroll);
        var startX = (long)context.ViewportBounds.X - PositiveModulo(scrollPixels, width) - width;
        var endX = (long)context.ViewportBounds.X + context.ViewportBounds.Width + width;
        var tint = new Color(layer.Tint, alpha);
        for (long x = startX; x < endX; x += width)
        {
            context.SpriteBatch.Draw(
                sprite.Texture,
                new Rectangle(SaturatingToInt(x), y, width, height),
                source,
                tint);
        }

        return true;
    }

    private static void DrawGradientSky(
        RenderContext context,
        float undergroundBlend,
        bool isNight,
        in LivingWorldFrameSnapshot livingWorld)
    {
        var night = isNight ? 1f : 0f;
        var dayTop = Color.Lerp(new Color(91, 155, 213), new Color(24, 34, 66), night);
        var dayMiddle = Color.Lerp(new Color(73, 137, 186), new Color(33, 42, 74), night);
        var dayBottom = Color.Lerp(new Color(50, 95, 132), new Color(40, 48, 78), night);
        var storm = livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm
            ? livingWorld.WeatherIntensity
            : 0f;
        var fog = livingWorld.Weather == Game.Core.Weather.WeatherKind.Fog
            ? livingWorld.WeatherIntensity
            : 0f;
        var weatherTint = Color.Lerp(new Color(70, 91, 108), new Color(47, 54, 72), storm);
        var weatherBlend = MathHelper.Clamp(livingWorld.CloudCover * 0.38f + fog * 0.18f, 0f, 0.5f);
        var top = Color.Lerp(Color.Lerp(dayTop, weatherTint, weatherBlend), new Color(18, 18, 27), undergroundBlend);
        var middle = Color.Lerp(Color.Lerp(dayMiddle, weatherTint, weatherBlend), new Color(28, 24, 31), undergroundBlend);
        var bottom = Color.Lerp(Color.Lerp(dayBottom, weatherTint, weatherBlend), new Color(36, 29, 34), undergroundBlend);
        var third = Math.Max(1, context.ViewportBounds.Height / 3);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(context.ViewportBounds.X, context.ViewportBounds.Y, context.ViewportBounds.Width, third + 1),
            top);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(context.ViewportBounds.X, context.ViewportBounds.Y + third, context.ViewportBounds.Width, third + 1),
            middle);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(
                context.ViewportBounds.X,
                context.ViewportBounds.Y + third * 2,
                context.ViewportBounds.Width,
                Math.Max(0, context.ViewportBounds.Height - third * 2)),
            bottom);
    }

    private static void DrawWeatherDepth(
        RenderContext context,
        Camera2D camera,
        float undergroundBlend,
        in LivingWorldFrameSnapshot livingWorld)
    {
        var surfaceVisibility = 1f - undergroundBlend;
        var intensity = Math.Clamp(livingWorld.WeatherIntensity * surfaceVisibility, 0f, 1f);
        if (intensity <= 0.001f)
        {
            return;
        }

        if (livingWorld.Weather == Game.Core.Weather.WeatherKind.Fog)
        {
            var bandHeight = Math.Max(16, context.ViewportBounds.Height / 8);
            for (var band = 0; band < 4; band++)
            {
                var drift = MathF.Sin(
                    (float)context.Time.TotalSeconds * (0.09f + band * 0.02f) +
                    camera.Position.X * 0.0005f +
                    band) * 18f;
                var y = context.ViewportBounds.Y + context.ViewportBounds.Height / 4 + band * bandHeight;
                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(
                        context.ViewportBounds.X + SaturatingRound(drift) - 24,
                        y,
                        context.ViewportBounds.Width + 48,
                        bandHeight),
                    new Color(170, 190, 196, Math.Clamp((int)(intensity * 34f), 0, 255)));
            }

            return;
        }

        if (livingWorld.Weather is not (Game.Core.Weather.WeatherKind.Rain or Game.Core.Weather.WeatherKind.Storm))
        {
            return;
        }

        var streaks = livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm ? 30 : 18;
        var windOffset = SaturatingRound(livingWorld.Wind * 6f);
        var frame = (int)Math.Floor(context.Time.TotalSeconds * 24d);
        for (var index = 0; index < streaks; index++)
        {
            var x = PositiveModulo(index * 97 + frame * 5, Math.Max(1, context.ViewportBounds.Width + 48)) - 24;
            var y = PositiveModulo(index * 53 + frame * 11, Math.Max(1, context.ViewportBounds.Height + 64)) - 64;
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(
                    context.ViewportBounds.X + x + windOffset,
                    context.ViewportBounds.Y + y,
                    1,
                    livingWorld.Weather == Game.Core.Weather.WeatherKind.Storm ? 14 : 9),
                new Color(154, 192, 215, Math.Clamp((int)(intensity * 92f), 0, 255)));
        }
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var remainder = value % modulo;
        return remainder < 0 ? remainder + modulo : remainder;
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

    private static int SaturatingToInt(long value)
    {
        return value <= int.MinValue
            ? int.MinValue
            : value >= int.MaxValue
                ? int.MaxValue
                : (int)value;
    }
}
