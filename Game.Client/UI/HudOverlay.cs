using Game.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class HudOverlay
{
    private const int HotbarSlots = 10;
    private const int SlotSize = 42;
    private const int SlotGap = 4;

    public void Draw(RenderContext context, int selectedHotbarSlot, int health, int maxHealth)
    {
        DrawHotbar(context, selectedHotbarSlot);
        DrawHealthBar(context, health, maxHealth);
    }

    private static void DrawHotbar(RenderContext context, int selectedSlot)
    {
        var totalWidth = HotbarSlots * SlotSize + (HotbarSlots - 1) * SlotGap;
        var startX = (context.ViewportBounds.Width - totalWidth) / 2;
        var y = context.ViewportBounds.Height - SlotSize - 18;

        for (var slot = 0; slot < HotbarSlots; slot++)
        {
            var x = startX + slot * (SlotSize + SlotGap);
            var bounds = new Rectangle(x, y, SlotSize, SlotSize);
            var selected = slot == selectedSlot;
            var fill = selected ? new Color(62, 72, 86, 235) : new Color(24, 29, 36, 220);
            var border = selected ? new Color(245, 214, 126) : new Color(92, 104, 118);

            DrawRectangle(context, bounds, fill);
            DrawBorder(context, bounds, border, selected ? 3 : 1);

            var label = slot == 9 ? "0" : (slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.DebugText.Draw(new Vector2(x + 5, y + 5), label, selected ? Color.White : Color.LightGray, 2);
        }
    }

    private static void DrawHealthBar(RenderContext context, int health, int maxHealth)
    {
        var width = 190;
        var height = 16;
        var x = context.ViewportBounds.Width - width - 18;
        var y = 18;
        var bounds = new Rectangle(x, y, width, height);
        var fillWidth = maxHealth <= 0 ? 0 : (int)MathF.Round(width * MathHelper.Clamp(health / (float)maxHealth, 0, 1));

        DrawRectangle(context, bounds, new Color(30, 26, 32, 230));
        DrawRectangle(context, new Rectangle(x, y, fillWidth, height), new Color(196, 56, 68));
        DrawBorder(context, bounds, new Color(246, 183, 94), 1);
    }

    private static void DrawRectangle(RenderContext context, Rectangle bounds, Color color)
    {
        context.SpriteBatch.Draw(context.Pixel, bounds, color);
    }

    private static void DrawBorder(RenderContext context, Rectangle bounds, Color color, int thickness)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }
}
