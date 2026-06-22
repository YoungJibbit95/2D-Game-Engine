using Game.Core.World;
using Game.Core.Time;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class ParallaxBackgroundRenderer
{
    public string SurfaceSpriteId { get; set; } = "world/backgrounds/forest_parallax_layer";

    public string CaveSpriteId { get; set; } = "world/backgrounds/cave_parallax_layer";

    public float SurfaceParallax { get; set; } = 0.18f;

    public float CaveParallax { get; set; } = 0.08f;

    public float Opacity { get; set; } = 0.92f;

    public void Draw(RenderContext context, ClientTextureRegistry? textures, Camera2D camera, World world, WorldTime? time = null)
    {
        if (textures is null)
        {
            DrawFallback(context, world, camera, time);
            return;
        }

        var spawnY = world.Metadata.SpawnTile.Y * Game.Core.GameConstants.TileSize;
        var undergroundBlend = MathHelper.Clamp((camera.Position.Y - spawnY - 220f) / 340f, 0f, 1f);
        var deepBlend = MathHelper.Clamp((camera.Position.Y - spawnY - 980f) / 620f, 0f, 1f);
        DrawGradientSky(context, undergroundBlend, time);

        if (undergroundBlend < 0.98f)
        {
            var night = time?.IsNight == true;
            TryDrawLayer(context, textures, night ? "world/backgrounds/night_forest_parallax_layer" : SurfaceSpriteId, camera, SurfaceParallax * 0.55f, 0.44f * (1f - undergroundBlend), verticalOffset: -54);
            TryDrawLayer(context, textures, "world/backgrounds/magical_grove_parallax_layer", camera, SurfaceParallax * 0.9f, 0.30f * (1f - undergroundBlend), verticalOffset: -34);
            TryDrawLayer(context, textures, SurfaceSpriteId, camera, SurfaceParallax, 1f - undergroundBlend * 0.72f, verticalOffset: -24);
        }

        if (undergroundBlend > 0.02f)
        {
            TryDrawLayer(context, textures, CaveSpriteId, camera, CaveParallax, undergroundBlend * (1f - deepBlend * 0.35f), verticalOffset: 24);
            TryDrawLayer(context, textures, "world/backgrounds/deep_cave_parallax_layer", camera, CaveParallax * 0.6f, deepBlend, verticalOffset: 10);
        }
    }

    private bool TryDrawLayer(
        RenderContext context,
        ClientTextureRegistry textures,
        string spriteId,
        Camera2D camera,
        float parallax,
        float alpha,
        int verticalOffset)
    {
        var sprite = textures.Get(spriteId);
        var source = sprite.SourceRectangle;
        if (sprite.IsPlaceholder || source.Width <= 0 || source.Height <= 0)
        {
            return false;
        }

        alpha = Math.Clamp(alpha * Opacity, 0f, 1f);
        if (alpha <= 0.001f)
        {
            return false;
        }

        var scale = Math.Max(1f, MathF.Ceiling(context.ViewportBounds.Height / 320f));
        var width = Math.Max(1, (int)MathF.Round(source.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(source.Height * scale));
        var y = context.ViewportBounds.Height - height - 72 + verticalOffset;
        var scroll = camera.Position.X * parallax;
        var startX = -PositiveModulo((int)MathF.Round(scroll), width) - width;

        for (var x = startX; x < context.ViewportBounds.Width + width; x += width)
        {
            context.SpriteBatch.Draw(
                sprite.Texture,
                new Rectangle(x, y, width, height),
                source,
                new Color((byte)255, (byte)255, (byte)255, (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255)));
        }

        return true;
    }

    private static void DrawGradientSky(RenderContext context, float undergroundBlend, WorldTime? time)
    {
        var night = time?.IsNight == true ? 1f : 0f;
        var dayTop = Color.Lerp(new Color(91, 155, 213), new Color(24, 34, 66), night);
        var dayMiddle = Color.Lerp(new Color(73, 137, 186), new Color(33, 42, 74), night);
        var dayBottom = Color.Lerp(new Color(50, 95, 132), new Color(40, 48, 78), night);
        var top = Color.Lerp(dayTop, new Color(18, 18, 27), undergroundBlend);
        var middle = Color.Lerp(dayMiddle, new Color(28, 24, 31), undergroundBlend);
        var bottom = Color.Lerp(dayBottom, new Color(36, 29, 34), undergroundBlend);
        var third = Math.Max(1, context.ViewportBounds.Height / 3);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, 0, context.ViewportBounds.Width, third + 1), top);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, third, context.ViewportBounds.Width, third + 1), middle);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, third * 2, context.ViewportBounds.Width, context.ViewportBounds.Height - third * 2), bottom);
    }

    private static void DrawFallback(RenderContext context, World world, Camera2D camera, WorldTime? time)
    {
        var spawnY = world.Metadata.SpawnTile.Y * Game.Core.GameConstants.TileSize;
        var undergroundBlend = MathHelper.Clamp((camera.Position.Y - spawnY - 220f) / 340f, 0f, 1f);
        DrawGradientSky(context, undergroundBlend, time);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }
}
