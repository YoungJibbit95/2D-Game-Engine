using Game.Client.Rendering;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class HudOverlay
{
    private const int HotbarSlots = 10;
    private const int SlotSize = 42;
    private const int SlotGap = 4;

    public void Draw(
        RenderContext context,
        PlayerInventory? inventory,
        IItemDefinitionProvider? items,
        ClientTextureRegistry? textures,
        int health,
        int maxHealth,
        GameSettings settings)
    {
        var palette = UiTheme.Resolve(settings);
        DrawHotbar(context, inventory, items, textures, palette, settings.Ui.HudOpacity);
        DrawHealthBar(context, health, maxHealth, palette, settings.Ui.HudOpacity);
    }

    private static void DrawHotbar(
        RenderContext context,
        PlayerInventory? inventory,
        IItemDefinitionProvider? items,
        ClientTextureRegistry? textures,
        UiPalette palette,
        float opacity)
    {
        var totalWidth = HotbarSlots * SlotSize + (HotbarSlots - 1) * SlotGap;
        var startX = (context.ViewportBounds.Width - totalWidth) / 2;
        var y = context.ViewportBounds.Height - SlotSize - 18;
        var selectedSlot = inventory?.SelectedHotbarSlot ?? 0;

        for (var slot = 0; slot < HotbarSlots; slot++)
        {
            var x = startX + slot * (SlotSize + SlotGap);
            var bounds = new Rectangle(x, y, SlotSize, SlotSize);
            var selected = slot == selectedSlot;
            context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(selected ? palette.SurfaceHover : palette.Surface, opacity));
            UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(selected ? palette.Accent : palette.SurfaceHover, selected ? 1f : 0.72f), selected ? 2 : 1);

            var label = slot == 9 ? "0" : (slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.DebugText.Draw(new Vector2(x + 4, y + 4), label, selected ? palette.Text : palette.TextMuted, 1);

            if (inventory is not null && items is not null)
            {
                ItemIconRenderer.DrawItemStack(
                    context,
                    textures,
                    items,
                    inventory.Hotbar.Slots[slot].Stack,
                    bounds,
                    palette,
                    drawCount: true,
                    opacity);
            }
        }
    }

    private static void DrawHealthBar(RenderContext context, int health, int maxHealth, UiPalette palette, float opacity)
    {
        var width = 190;
        var height = 16;
        var x = context.ViewportBounds.Width - width - 18;
        var y = 18;
        var bounds = new Rectangle(x, y, width, height);
        var fillWidth = maxHealth <= 0 ? 0 : (int)MathF.Round(width * MathHelper.Clamp(health / (float)maxHealth, 0, 1));

        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Surface, opacity));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, fillWidth, height), UiTheme.WithAlpha(palette.Danger, 0.94f));
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.Warning, 0.9f), 1);
    }
}
