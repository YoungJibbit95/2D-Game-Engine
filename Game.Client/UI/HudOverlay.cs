using Game.Client.Rendering;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Settings;
using Game.Core.Runtime;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class HudOverlay
{
    private const int HotbarSlots = 10;
    private const int SlotSize = 42;
    private const int SlotGap = 4;

    public void Draw(
        RenderContext context,
        GameFrameSnapshot snapshot,
        IItemDefinitionProvider? items,
        ClientTextureRegistry? textures,
        GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var palette = UiTheme.Resolve(settings);
        DrawHotbar(context, snapshot.Player, items, textures, palette, settings.Ui.HudOpacity);
        DrawHealthBar(context, snapshot.Hud.Health, snapshot.Hud.MaxHealth, palette, settings.Ui.HudOpacity);
        DrawManaBar(context, textures, snapshot.Hud.Mana, snapshot.Hud.MaxMana, palette, settings.Ui.HudOpacity);
        DrawGuardStaminaBar(context, GuardBarPresentation.Create(snapshot.Player), palette, settings);
    }

    private static void DrawHotbar(
        RenderContext context,
        PlayerFrameSnapshot player,
        IItemDefinitionProvider? items,
        ClientTextureRegistry? textures,
        UiPalette palette,
        float opacity)
    {
        var totalWidth = HotbarSlots * SlotSize + (HotbarSlots - 1) * SlotGap;
        var startX = (context.ViewportBounds.Width - totalWidth) / 2;
        var y = context.ViewportBounds.Height - SlotSize - 18;
        var selectedSlot = player.SelectedHotbarSlot;

        for (var slot = 0; slot < HotbarSlots; slot++)
        {
            var x = startX + slot * (SlotSize + SlotGap);
            var bounds = new Rectangle(x, y, SlotSize, SlotSize);
            var selected = slot == selectedSlot;
            UiTheme.DrawSlot(context, bounds, palette, selected, hovered: false, opacity);

            var label = slot == 9 ? "0" : (slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            context.DebugText.Draw(new Vector2(x + 4, y + 4), label, selected ? palette.Text : palette.TextMuted, 1);

            if (items is not null && slot < player.Hotbar.Count)
            {
                ItemIconRenderer.DrawItemStack(
                    context,
                    textures,
                    items,
                    player.Hotbar[slot].Stack,
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
        context.DebugText.Draw(new Vector2(bounds.X + 6, bounds.Y + 4), $"{health}/{maxHealth}", palette.Text, 1);
    }

    private static void DrawManaBar(RenderContext context, ClientTextureRegistry? textures, int mana, int maxMana, UiPalette palette, float opacity)
    {
        var width = 190;
        var height = 14;
        var x = context.ViewportBounds.Width - width - 18;
        var y = 42;
        var bounds = new Rectangle(x, y, width, height);
        var fillWidth = maxMana <= 0 ? 0 : (int)MathF.Round(width * MathHelper.Clamp(mana / (float)maxMana, 0, 1));

        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Surface, opacity));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, y, fillWidth, height), UiTheme.WithAlpha(new Color(74, 132, 226), 0.94f));
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(new Color(128, 184, 255), 0.9f), 1);
        context.DebugText.Draw(new Vector2(bounds.X + 6, bounds.Y + 3), $"{mana}/{maxMana}", palette.Text, 1);

        if (textures is null)
        {
            return;
        }

        var starSize = 14;
        var starCount = Math.Min(10, Math.Max(0, (int)MathF.Ceiling(maxMana / 20f)));
        for (var i = 0; i < starCount; i++)
        {
            var starBounds = new Rectangle(x + i * (starSize + 3), y + height + 5, starSize, starSize);
            var alpha = mana >= (i + 1) * 20 ? 1f : 0.32f;
            if (!ItemIconRenderer.TryDrawSprite(context, textures, "ui/mana_star", starBounds, alpha))
            {
                context.SpriteBatch.Draw(context.Pixel, starBounds, UiTheme.WithAlpha(new Color(84, 139, 232), alpha * 0.85f));
                UiTheme.DrawBorder(context, starBounds, UiTheme.WithAlpha(palette.AccentSoft, alpha), 1);
            }
        }
    }

    private static void DrawGuardStaminaBar(
        RenderContext context,
        GuardBarPresentation guard,
        UiPalette palette,
        GameSettings settings)
    {
        if (!guard.IsVisible)
        {
            return;
        }

        const int width = 190;
        const int height = 12;
        var bounds = new Rectangle(context.ViewportBounds.Width - width - 18, 82, width, height);
        var labelWidth = 48;
        var track = new Rectangle(bounds.X + labelWidth, bounds.Y + 1, bounds.Width - labelWidth, bounds.Height - 2);
        var radius = Math.Min(4, UiTheme.ResolveContract(settings).Button.CornerRadius);
        var background = settings.Ui.HighContrastPanels ? palette.Backdrop : palette.Surface;
        var fillColor = guard.IsBroken
            ? palette.Danger
            : guard.IsGuarding
                ? palette.Warning
                : palette.AccentSoft;
        var borderColor = guard.IsBroken ? palette.Danger : guard.IsGuarding ? palette.Warning : palette.Accent;

        UiTheme.DrawRoundedRectangle(context, track, UiTheme.WithAlpha(background, settings.Ui.HudOpacity), radius);
        var fillWidth = (int)MathF.Round(track.Width * guard.NormalizedStamina);
        if (fillWidth > 0)
        {
            UiTheme.DrawRoundedRectangle(
                context,
                new Rectangle(track.X, track.Y, fillWidth, track.Height),
                UiTheme.WithAlpha(fillColor, guard.IsGuarding || guard.IsBroken ? 0.96f : 0.72f),
                Math.Min(radius, fillWidth / 2));
        }

        UiTheme.DrawRoundedBorder(
            context,
            track,
            UiTheme.WithAlpha(borderColor, 0.94f),
            radius,
            settings.Accessibility.HighContrastInteractionOutline ? 2 : 1);
        context.DebugText.Draw(
            new Vector2(bounds.X, bounds.Y + 3),
            guard.IsBroken ? "BREAK" : "GUARD",
            guard.IsBroken ? palette.Danger : palette.TextMuted,
            1);
    }
}

public readonly record struct GuardBarPresentation(
    bool IsVisible,
    bool IsGuarding,
    bool IsBroken,
    float NormalizedStamina)
{
    public static GuardBarPresentation Create(PlayerFrameSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Create(player.IsGuarding, player.IsGuardBroken, player.GuardStamina, player.MaxGuardStamina);
    }

    public static GuardBarPresentation Create(bool isGuarding, bool isBroken, float stamina, float maximumStamina)
    {
        if (!float.IsFinite(maximumStamina) || maximumStamina <= 0f)
        {
            return default;
        }

        var safeStamina = float.IsFinite(stamina) ? Math.Clamp(stamina, 0f, maximumStamina) : 0f;
        var normalized = safeStamina / maximumStamina;
        var visible = isGuarding || isBroken || safeStamina < maximumStamina - 0.001f;
        return new GuardBarPresentation(visible, isGuarding, isBroken, normalized);
    }
}
