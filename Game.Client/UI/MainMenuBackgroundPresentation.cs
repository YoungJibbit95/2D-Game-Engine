using Game.Client.Rendering;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

internal readonly record struct MainMenuBackgroundPlacement(Rectangle Destination, Rectangle Source);

internal static class MainMenuBackgroundCropPlanner
{
    public static MainMenuBackgroundPlacement Resolve(Rectangle viewport, Point textureSize)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewport), viewport, "Viewport dimensions must be positive.");
        }

        if (textureSize.X <= 0 || textureSize.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textureSize), textureSize, "Texture dimensions must be positive.");
        }

        var viewportAspect = viewport.Width / (double)viewport.Height;
        var textureAspect = textureSize.X / (double)textureSize.Y;
        Rectangle source;
        if (textureAspect > viewportAspect)
        {
            var width = Math.Clamp((int)Math.Round(textureSize.Y * viewportAspect), 1, textureSize.X);
            source = new Rectangle((textureSize.X - width) / 2, 0, width, textureSize.Y);
        }
        else
        {
            var height = Math.Clamp((int)Math.Round(textureSize.X / viewportAspect), 1, textureSize.Y);
            source = new Rectangle(0, (textureSize.Y - height) / 2, textureSize.X, height);
        }

        return new MainMenuBackgroundPlacement(viewport, source);
    }
}

public static class MainMenuAmbientOverlay
{
    public static void Draw(RenderContext context, UiPalette palette, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var viewport = context.ViewportBounds;
        var time = settings.Ui.ReducedMotion
            ? 0d
            : context.Time.TotalSeconds * Math.Clamp(settings.Ui.AnimationSpeed, 0.1f, 4f);

        DrawLanternPulse(context, viewport, 9, 28, palette.Warning, time, 0.0f);
        DrawLanternPulse(context, viewport, 15, 47, palette.Warning, time, 1.2f);
        DrawLanternPulse(context, viewport, 82, 22, palette.Warning, time, 2.1f);
        DrawLanternPulse(context, viewport, 88, 48, palette.Warning, time, 0.8f);
        DrawLanternPulse(context, viewport, 78, 61, palette.Warning, time, 2.8f);

        for (var index = 0; index < 21; index++)
        {
            var baseX = viewport.X + viewport.Width * 29 / 100 + PositiveModulo(index * 137, Math.Max(1, viewport.Width * 68 / 100));
            var x = baseX + (int)MathF.Round(MathF.Sin((float)time * 0.68f + index * 1.73f) * 12f);
            var y = viewport.Y + viewport.Height * 54 / 100 + PositiveModulo(index * 71, Math.Max(1, viewport.Height * 34 / 100)) +
                (int)MathF.Round(MathF.Cos((float)time * 0.92f + index) * 7f);
            var pulse = 0.34f + 0.56f * (0.5f + 0.5f * MathF.Sin((float)time * 2.4f + index * 1.47f));
            var firefly = index % 5 == 0 ? new Color(112, 235, 221) : new Color(255, 213, 104);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 3, y - 3, 7, 7), UiTheme.WithAlpha(firefly, pulse * 0.10f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 2, 2), UiTheme.WithAlpha(firefly, pulse));
        }

        for (var index = 0; index < 9; index++)
        {
            var x = viewport.X + viewport.Width * (24 + PositiveModulo(index * 19, 61)) / 100;
            var y = viewport.Y + viewport.Height * (5 + PositiveModulo(index * 13, 24)) / 100;
            var pulse = 0.28f + 0.58f * (0.5f + 0.5f * MathF.Sin((float)time * 1.35f + index * 2.03f));
            var color = UiTheme.WithAlpha(palette.Text, pulse);
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 3, y, 7, 1), UiTheme.WithAlpha(color, 0.48f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y - 3, 1, 7), UiTheme.WithAlpha(color, 0.48f));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, 2, 2), color);
        }

        DrawEdgeShade(context, viewport, palette);
    }

    private static void DrawLanternPulse(
        RenderContext context,
        Rectangle viewport,
        int xPercent,
        int yPercent,
        Color color,
        double time,
        float phase)
    {
        var x = viewport.X + viewport.Width * xPercent / 100;
        var y = viewport.Y + viewport.Height * yPercent / 100;
        var pulse = 0.70f + 0.20f * MathF.Sin((float)time * 3.7f + phase);
        DrawPixelDisc(context, new Point(x, y), Math.Clamp(viewport.Height / 48, 10, 24), UiTheme.WithAlpha(color, pulse * 0.035f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(x - 2, y - 3, 5, 7), UiTheme.WithAlpha(color, pulse * 0.24f));
    }

    private static void DrawPixelDisc(RenderContext context, Point center, int radius, Color color)
    {
        for (var y = -radius; y <= radius; y += 3)
        {
            var normalized = y / (float)radius;
            var halfWidth = (int)MathF.Round(radius * MathF.Sqrt(Math.Max(0f, 1f - normalized * normalized)));
            context.SpriteBatch.Draw(
                context.Pixel,
                new Rectangle(center.X - halfWidth, center.Y + y, halfWidth * 2 + 1, 4),
                color);
        }
    }

    private static void DrawEdgeShade(RenderContext context, Rectangle viewport, UiPalette palette)
    {
        var horizontal = Math.Clamp(viewport.Width / 38, 18, 58);
        var vertical = Math.Clamp(viewport.Height / 34, 12, 38);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Y, horizontal, viewport.Height), UiTheme.WithAlpha(palette.Backdrop, 0.22f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.Right - horizontal, viewport.Y, horizontal, viewport.Height), UiTheme.WithAlpha(palette.Backdrop, 0.22f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Y, viewport.Width, vertical), UiTheme.WithAlpha(palette.Backdrop, 0.14f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(viewport.X, viewport.Bottom - vertical, viewport.Width, vertical), UiTheme.WithAlpha(palette.Backdrop, 0.20f));
    }

    private static int PositiveModulo(int value, int modulus)
    {
        if (modulus <= 0)
        {
            return 0;
        }

        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
