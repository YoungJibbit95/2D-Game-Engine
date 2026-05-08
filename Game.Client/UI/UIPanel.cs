using Game.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class UIPanel : UIElement
{
    public Color BackgroundColor { get; set; } = new(18, 22, 28, 210);

    public Color BorderColor { get; set; } = new(96, 108, 122, 255);

    protected override void DrawSelf(UIContext context)
    {
        var render = context.RenderContext;
        render.SpriteBatch.Draw(render.Pixel, Bounds, BackgroundColor);
        DrawBorder(render, Bounds, BorderColor);
    }

    private static void DrawBorder(RenderContext render, Rectangle bounds, Color color)
    {
        render.SpriteBatch.Draw(render.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        render.SpriteBatch.Draw(render.Pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
        render.SpriteBatch.Draw(render.Pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        render.SpriteBatch.Draw(render.Pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
    }
}
