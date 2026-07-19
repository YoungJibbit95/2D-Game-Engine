using Game.Client.Rendering;
using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.Settings;
using Game.Core.Runtime;
using Game.Core.Combat;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class HudOverlay
{
    private const int HotbarSlots = 10;
    private static readonly string[] HotbarLabels = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];
    private int _cachedHealth = int.MinValue;
    private int _cachedMaxHealth = int.MinValue;
    private int _cachedMana = int.MinValue;
    private int _cachedMaxMana = int.MinValue;
    private int _cachedCombo = int.MinValue;
    private string _healthLabel = "0/0";
    private string _manaLabel = "0/0";
    private string _comboLabel = "COMBO 1";

    public void Draw(
        RenderContext context,
        GameFrameSnapshot snapshot,
        IItemDefinitionProvider? items,
        ClientTextureRegistry? textures,
        GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var palette = UiTheme.Resolve(settings);
        var layout = PixelHudLayoutPlanner.Resolve(context.ViewportBounds, HotbarSlots);
        UpdateCachedLabels(snapshot);

        PixelUiPrimitives.DrawGlassSurface(
            context,
            layout.ResourcePanel,
            palette,
            settings.Ui.HudOpacity * 0.94f,
            settings);
        context.DebugText.Draw(
            new Vector2(layout.ResourcePanel.X + 10, layout.ResourcePanel.Y + 9),
            "VITALS",
            palette.Accent,
            1);

        DrawHotbar(context, snapshot.Player, items, textures, palette, settings, layout);
        DrawHealthBar(context, snapshot.Hud.Health, snapshot.Hud.MaxHealth, palette, settings, layout.HealthMeter, _healthLabel);
        DrawManaBar(context, textures, snapshot.Hud.Mana, snapshot.Hud.MaxMana, palette, settings, layout.ManaMeter, _manaLabel);
        DrawGuardStaminaBar(context, GuardBarPresentation.Create(snapshot.Player), palette, settings, layout.GuardMeter);
        DrawAttackRhythm(context, AttackHudPresentation.Create(snapshot.Attack), palette, settings, layout.AttackMeter, _comboLabel);
    }

    private void UpdateCachedLabels(GameFrameSnapshot snapshot)
    {
        if (_cachedHealth != snapshot.Hud.Health || _cachedMaxHealth != snapshot.Hud.MaxHealth)
        {
            _cachedHealth = snapshot.Hud.Health;
            _cachedMaxHealth = snapshot.Hud.MaxHealth;
            _healthLabel = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{_cachedHealth}/{_cachedMaxHealth}");
        }

        if (_cachedMana != snapshot.Hud.Mana || _cachedMaxMana != snapshot.Hud.MaxMana)
        {
            _cachedMana = snapshot.Hud.Mana;
            _cachedMaxMana = snapshot.Hud.MaxMana;
            _manaLabel = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{_cachedMana}/{_cachedMaxMana}");
        }

        var combo = Math.Max(1, snapshot.Attack.ComboIndex + 1);
        if (_cachedCombo != combo)
        {
            _cachedCombo = combo;
            _comboLabel = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"COMBO {combo}");
        }
    }

    private static void DrawHotbar(
        RenderContext context,
        PlayerFrameSnapshot player,
        IItemDefinitionProvider? items,
        ClientTextureRegistry? textures,
        UiPalette palette,
        GameSettings settings,
        PixelHudLayout layout)
    {
        var opacity = settings.Ui.HudOpacity;
        PixelUiPrimitives.DrawGlassSurface(context, layout.HotbarDock, palette, opacity * 0.88f, settings);
        var selectedSlot = player.SelectedHotbarSlot;

        for (var slot = 0; slot < HotbarSlots; slot++)
        {
            var bounds = layout.HotbarSlot(slot);
            var selected = slot == selectedSlot;
            UiTheme.DrawSlot(context, bounds, palette, selected, hovered: false, opacity);

            context.DebugText.Draw(
                new Vector2(bounds.X + 4, bounds.Y + 4),
                HotbarLabels[slot],
                selected ? palette.Text : palette.TextMuted,
                1);

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

    private static void DrawHealthBar(
        RenderContext context,
        int health,
        int maxHealth,
        UiPalette palette,
        GameSettings settings,
        Rectangle bounds,
        string valueLabel)
    {
        var normalized = maxHealth <= 0 ? 0f : MathHelper.Clamp(health / (float)maxHealth, 0f, 1f);
        PixelUiPrimitives.DrawMeter(
            context,
            bounds,
            normalized,
            palette,
            palette.Danger,
            settings.Ui.HudOpacity,
            settings,
            segmented: true,
            emphasized: normalized <= 0.25f);
        DrawMeterText(context, bounds, "HP", valueLabel, palette);
    }

    private static void DrawManaBar(
        RenderContext context,
        ClientTextureRegistry? textures,
        int mana,
        int maxMana,
        UiPalette palette,
        GameSettings settings,
        Rectangle bounds,
        string valueLabel)
    {
        var normalized = maxMana <= 0 ? 0f : MathHelper.Clamp(mana / (float)maxMana, 0f, 1f);
        PixelUiPrimitives.DrawMeter(
            context,
            bounds,
            normalized,
            palette,
            new Color(76, 131, 238),
            settings.Ui.HudOpacity,
            settings,
            segmented: true);
        var manaIcon = new Rectangle(bounds.X + 4, bounds.Center.Y - 5, 10, 10);
        var drewManaIcon = textures is not null &&
            ItemIconRenderer.TryDrawSprite(context, textures, "ui/mana_star", manaIcon, normalized <= 0f ? 0.42f : 0.96f);
        DrawMeterText(context, bounds, drewManaIcon ? string.Empty : "MP", valueLabel, palette);
    }

    private static void DrawGuardStaminaBar(
        RenderContext context,
        GuardBarPresentation guard,
        UiPalette palette,
        GameSettings settings,
        Rectangle bounds)
    {
        if (!guard.IsVisible)
        {
            return;
        }

        var fillColor = guard.IsBroken
            ? palette.Danger
            : guard.IsGuarding
                ? palette.Warning
                : palette.AccentSoft;
        PixelUiPrimitives.DrawMeter(
            context,
            bounds,
            guard.NormalizedStamina,
            palette,
            fillColor,
            settings.Ui.HudOpacity,
            settings,
            emphasized: guard.IsBroken || guard.IsGuarding);
        context.DebugText.Draw(
            new Vector2(bounds.X + 6, bounds.Y + 3),
            guard.IsBroken ? "BREAK" : "GUARD",
            guard.IsBroken ? palette.Text : palette.TextMuted,
            1);
    }

    private static void DrawAttackRhythm(
        RenderContext context,
        AttackHudPresentation attack,
        UiPalette palette,
        GameSettings settings,
        Rectangle bounds,
        string comboLabel)
    {
        if (!attack.IsVisible)
        {
            return;
        }

        var phaseColor = attack.Phase switch
        {
            AttackRuntimePhase.Startup => palette.Warning,
            AttackRuntimePhase.Active => palette.Accent,
            AttackRuntimePhase.Recovery => palette.AccentSoft,
            _ => palette.TextMuted
        };
        PixelUiPrimitives.DrawStatusChip(context, bounds, palette, phaseColor, settings.Ui.HudOpacity, settings);
        context.DebugText.Draw(
            new Vector2(bounds.X + 7, bounds.Y + 5),
            attack.PhaseLabel,
            phaseColor,
            1);
        var comboWidth = comboLabel.Length * 6;
        context.DebugText.Draw(
            new Vector2(Math.Max(bounds.X + 55, bounds.Right - comboWidth - 7), bounds.Y + 5),
            comboLabel,
            palette.TextMuted,
            1);

        if (attack.HasQueuedCombo || attack.HasBufferedInput)
        {
            var pulse = new Rectangle(bounds.Right - 18, bounds.Y + 5, 10, 8);
            UiTheme.DrawRoundedRectangle(context, pulse, UiTheme.WithAlpha(palette.Warning, 0.96f), 3);
        }
    }

    private static void DrawMeterText(
        RenderContext context,
        Rectangle bounds,
        string label,
        string value,
        UiPalette palette)
    {
        context.DebugText.Draw(new Vector2(bounds.X + 6, bounds.Y + Math.Max(2, (bounds.Height - 7) / 2)), label, palette.Text, 1);
        var valueWidth = value.Length * 6;
        context.DebugText.Draw(
            new Vector2(Math.Max(bounds.X + 28, bounds.Right - valueWidth - 6), bounds.Y + Math.Max(2, (bounds.Height - 7) / 2)),
            value,
            palette.Text,
            1);
    }
}

public readonly record struct AttackHudPresentation(
    bool IsVisible,
    AttackRuntimePhase Phase,
    string PhaseLabel,
    int ComboNumber,
    bool HasQueuedCombo,
    bool HasBufferedInput)
{
    public static AttackHudPresentation Create(in AttackRuntimeFrameSnapshot attack)
    {
        if (!attack.HasSequence || attack.Phase == AttackRuntimePhase.Idle)
        {
            return default;
        }

        return new AttackHudPresentation(
            true,
            attack.Phase,
            attack.Phase switch
            {
                AttackRuntimePhase.Startup => "STARTUP",
                AttackRuntimePhase.Active => "ACTIVE",
                AttackRuntimePhase.Recovery => "RECOVERY",
                AttackRuntimePhase.Cooldown => "COOLDOWN",
                _ => "READY"
            },
            Math.Max(1, attack.ComboIndex + 1),
            attack.HasQueuedCombo,
            attack.HasBufferedInput);
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
