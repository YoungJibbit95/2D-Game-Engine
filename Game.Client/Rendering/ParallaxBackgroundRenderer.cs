using Game.Core.World;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed class ParallaxBackgroundRenderer
{
    public string SurfaceSpriteId { get; set; } = "world/backgrounds/forest_parallax_layer";

    public string CaveSpriteId { get; set; } = "world/backgrounds/cave_parallax_layer";

    public float SurfaceParallax { get; set; } = 0.18f;

    public float CaveParallax { get; set; } = 0.08f;

    public float Opacity { get; set; } = 0.92f;

    public void Draw(RenderContext context, ClientTextureRegistry? textures, Camera2D camera, World world)
    {
        if (textures is null)
        {
            DrawFallback(context, world, camera);
            return;
        }

        var spawnY = world.Metadata.SpawnTile.Y * Game.Core.GameConstants.TileSize;
        var undergroundBlend = MathHelper.Clamp((camera.Position.Y - spawnY - 220f) / 340f, 0f, 1f);
        DrawGradientSky(context, undergroundBlend);

        if (undergroundBlend < 0.98f)
        {
            DrawLayer(context, textures, SurfaceSpriteId, camera, SurfaceParallax, 1f - undergroundBlend * 0.72f, verticalOffset: -24);
        }

        if (undergroundBlend > 0.02f)
        {
            DrawLayer(context, textures, CaveSpriteId, camera, CaveParallax, undergroundBlend, verticalOffset: 24);
        }
    }

    private void DrawLayer(
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
        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }

        alpha = Math.Clamp(alpha * Opacity, 0f, 1f);
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
    }

    private static void DrawGradientSky(RenderContext context, float undergroundBlend)
    {
        var top = Color.Lerp(new Color(91, 155, 213), new Color(18, 18, 27), undergroundBlend);
        var middle = Color.Lerp(new Color(73, 137, 186), new Color(28, 24, 31), undergroundBlend);
        var bottom = Color.Lerp(new Color(50, 95, 132), new Color(36, 29, 34), undergroundBlend);
        var third = Math.Max(1, context.ViewportBounds.Height / 3);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, 0, context.ViewportBounds.Width, third + 1), top);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, third, context.ViewportBounds.Width, third + 1), middle);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(0, third * 2, context.ViewportBounds.Width, context.ViewportBounds.Height - third * 2), bottom);
    }

    private static void DrawFallback(RenderContext context, World world, Camera2D camera)
    {
        var spawnY = world.Metadata.SpawnTile.Y * Game.Core.GameConstants.TileSize;
        var undergroundBlend = MathHelper.Clamp((camera.Position.Y - spawnY - 220f) / 340f, 0f, 1f);
        DrawGradientSky(context, undergroundBlend);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }
}
