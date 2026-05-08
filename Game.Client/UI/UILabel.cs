using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class UILabel : UIElement
{
    public string Text { get; set; } = string.Empty;

    public Color TextColor { get; set; } = Color.White;

    public int Scale { get; set; } = 2;

    protected override void DrawSelf(UIContext context)
    {
        context.RenderContext.DebugText.Draw(new Vector2(Bounds.X, Bounds.Y), Text, TextColor, Scale);
    }
}
