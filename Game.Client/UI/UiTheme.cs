using Game.Client.Rendering;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public readonly record struct UiPalette(
    Color Backdrop,
    Color Surface,
    Color SurfaceRaised,
    Color SurfaceHover,
    Color Accent,
    Color AccentSoft,
    Color Text,
    Color TextMuted,
    Color Warning,
    Color Danger);

public static class UiTheme
{
    public static UiPalette Resolve(GameSettings? settings = null)
    {
        var theme = settings?.Ui.Theme ?? "Midnight";
        return theme.ToUpperInvariant() switch
        {
            "EMBER" => new UiPalette(
                new Color(14, 11, 10),
                new Color(25, 22, 21),
                new Color(42, 35, 31),
                new Color(58, 45, 38),
                new Color(232, 157, 89),
                new Color(144, 91, 65),
                new Color(248, 241, 225),
                new Color(184, 169, 151),
                new Color(245, 209, 117),
                new Color(219, 86, 72)),
            "FOREST" => new UiPalette(
                new Color(9, 15, 13),
                new Color(16, 25, 22),
                new Color(29, 43, 36),
                new Color(39, 58, 49),
                new Color(114, 190, 126),
                new Color(67, 121, 88),
                new Color(229, 242, 229),
                new Color(156, 184, 164),
                new Color(225, 214, 111),
                new Color(210, 82, 78)),
            _ => new UiPalette(
                new Color(8, 12, 18),
                new Color(14, 19, 27),
                new Color(24, 31, 42),
                new Color(36, 47, 62),
                new Color(128, 184, 224),
                new Color(76, 111, 145),
                new Color(235, 242, 248),
                new Color(152, 174, 194),
                new Color(245, 214, 126),
                new Color(218, 84, 76))
        };
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255));
    }

    public static void DrawPanel(RenderContext context, Rectangle bounds, UiPalette palette, float opacity, bool raised = true)
    {
        var fill = raised ? palette.SurfaceRaised : palette.Surface;
        context.SpriteBatch.Draw(context.Pixel, bounds, WithAlpha(fill, opacity));
        DrawBorder(context, bounds, WithAlpha(palette.AccentSoft, 0.72f), 1);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), WithAlpha(palette.Accent, 0.86f));
    }

    public static void DrawButton(RenderContext context, Rectangle bounds, UiPalette palette, bool selected, bool hovered, bool enabled = true)
    {
        var fill = selected
            ? palette.SurfaceHover
            : hovered
                ? palette.SurfaceRaised
                : palette.Surface;
        var border = selected ? palette.Accent : hovered ? palette.AccentSoft : palette.SurfaceHover;
        context.SpriteBatch.Draw(context.Pixel, bounds, WithAlpha(fill, enabled ? 0.92f : 0.52f));
        DrawBorder(context, bounds, WithAlpha(border, enabled ? 0.95f : 0.45f), selected ? 2 : 1);
    }

    public static void DrawProgressBar(RenderContext context, Rectangle bounds, float progress, UiPalette palette)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        context.SpriteBatch.Draw(context.Pixel, bounds, WithAlpha(palette.SurfaceRaised, 0.86f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, (int)MathF.Round(bounds.Width * progress), bounds.Height), WithAlpha(palette.Accent, 0.94f));
        DrawBorder(context, bounds, WithAlpha(palette.AccentSoft, 0.8f), 1);
    }

    public static void DrawBorder(RenderContext context, Rectangle bounds, Color color, int thickness)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }
}
