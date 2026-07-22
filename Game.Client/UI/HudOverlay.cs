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
    private readonly HudWorldPresentationCache _worldPresentation = new();

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
        _worldPresentation.Update(snapshot, items);

        PixelUiPrimitives.DrawGlassSurface(
            context,
            layout.ResourcePanel,
            palette,
            settings.Ui.HudOpacity * 0.94f,
            settings);
        context.DebugText.Draw(
            new Vector2(layout.ResourcePanel.X + 10, layout.ResourcePanel.Y + 9),
            "VITALS",
            UiTheme.ResolveMaterials(palette).BrassLight,
            1);
        DrawResourceCrest(context, layout.ResourceCrest, palette, settings.Ui.HudOpacity);
        DrawHealthPips(context, layout.ResourcePanel, snapshot.Hud.Health, snapshot.Hud.MaxHealth, palette, settings.Ui.HudOpacity);

        DrawWorldPanel(context, snapshot, textures, palette, settings, layout, _worldPresentation);
        DrawContextPanel(context, palette, settings, layout, _worldPresentation);
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


    private static void DrawWorldPanel(
        RenderContext context,
        GameFrameSnapshot snapshot,
        ClientTextureRegistry? textures,
        UiPalette palette,
        GameSettings settings,
        PixelHudLayout layout,
        HudWorldPresentationCache presentation)
    {
        if (layout.WorldPanel.Width <= 0 || layout.WorldPanel.Height <= 0)
        {
            return;
        }

        var opacity = settings.Ui.HudOpacity;
        var materials = UiTheme.ResolveMaterials(palette);
        PixelUiPrimitives.DrawGlassSurface(context, layout.WorldPanel, palette, opacity * 0.92f, settings, raised: false);
        context.DebugText.Draw(
            new Vector2(layout.WorldPanel.X + 10, layout.WorldPanel.Y + 8),
            presentation.DayLabel,
            materials.BrassLight,
            1);

        DrawCompassFrame(context, layout.WorldIcon, palette, opacity);
        var iconInner = layout.WorldIcon.Width >= 28
            ? new Rectangle(
                layout.WorldIcon.X + layout.WorldIcon.Width / 4,
                layout.WorldIcon.Y + layout.WorldIcon.Height / 4,
                Math.Max(1, layout.WorldIcon.Width / 2),
                Math.Max(1, layout.WorldIcon.Height / 2))
            : layout.WorldIcon;
        var iconId = snapshot.LivingWorld.Presentation.BiomeIconSpriteId;
        var drewIcon = iconId is not null &&
            ItemIconRenderer.TryDrawSprite(context, textures, iconId, iconInner, opacity);
        var textX = layout.WorldIcon.Width > 0
            ? layout.WorldIcon.Right + 8
            : layout.WorldPanel.X + 10;
        if (textX + 32 >= layout.WorldPanel.Right)
        {
            textX = layout.WorldPanel.X + 10;
        }

        context.DebugText.Draw(
            new Vector2(textX, layout.WorldPanel.Y + 29),
            layout.Density == PixelUiDensity.Compact ? presentation.CompactBiomeLabel : presentation.BiomeLabel,
            palette.Text,
            1);
        context.DebugText.Draw(
            new Vector2(textX, layout.WorldPanel.Y + 46),
            presentation.AtmosphereLabel,
            drewIcon ? palette.TextMuted : palette.Accent,
            1);

        if (snapshot.LivingWorld.IsWorldEventActive)
        {
            PixelUiPrimitives.DrawMeter(
                context,
                layout.WorldEventMeter,
                snapshot.LivingWorld.WorldEventProgress,
                palette,
                materials.BrassLight,
                opacity,
                settings,
                emphasized: true);
        }
    }

    private static void DrawContextPanel(
        RenderContext context,
        UiPalette palette,
        GameSettings settings,
        PixelHudLayout layout,
        HudWorldPresentationCache presentation)
    {
        if (layout.ContextPanel.Width <= 0 || layout.ContextPanel.Height <= 0)
        {
            return;
        }

        PixelUiPrimitives.DrawGlassSurface(
            context,
            layout.ContextPanel,
            palette,
            settings.Ui.HudOpacity * 0.78f,
            settings,
            raised: false);
        context.DebugText.Draw(
            new Vector2(layout.ContextPanel.X + 8, layout.ContextPanel.Y + Math.Max(3, (layout.ContextPanel.Height - 7) / 2)),
            presentation.ContextLabel,
            palette.TextMuted,
            1);
        var threatWidth = presentation.ThreatLabel.Length * 6;
        context.DebugText.Draw(
            new Vector2(layout.ContextPanel.Right - threatWidth - 8, layout.ContextPanel.Y + Math.Max(3, (layout.ContextPanel.Height - 7) / 2)),
            presentation.ThreatLabel,
            presentation.HasThreats ? palette.Danger : palette.Accent,
            1);
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
        var ribbonY = layout.HotbarDock.Y + 3;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(layout.HotbarDock.X + 10, ribbonY, Math.Max(1, layout.HotbarDock.Width / 5), 1), UiTheme.WithAlpha(palette.Warning, opacity * 0.48f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(layout.HotbarDock.Right - 10 - Math.Max(1, layout.HotbarDock.Width / 5), ribbonY, Math.Max(1, layout.HotbarDock.Width / 5), 1), UiTheme.WithAlpha(palette.Warning, opacity * 0.48f));
        var selectedSlot = player.SelectedHotbarSlot;

        for (var slot = 0; slot < HotbarSlots; slot++)
        {
            var selected = slot == selectedSlot;
            var baseBounds = layout.HotbarSlot(slot);
            var lift = selected ? settings.Ui.ReducedMotion ? 2 : 2 + (int)MathF.Round(MathF.Sin((float)context.Time.TotalSeconds * 3.2f) * 1f) : 0;
            var bounds = new Rectangle(baseBounds.X, baseBounds.Y - lift, baseBounds.Width, baseBounds.Height);
            UiTheme.DrawSlot(context, bounds, palette, selected, hovered: false, opacity);
            if (selected)
            {
                var cap = new Rectangle(bounds.Center.X - 5, bounds.Y - 3, 10, 2);
                context.SpriteBatch.Draw(context.Pixel, cap, UiTheme.WithAlpha(palette.Warning, opacity));
            }

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
        var heartColor = normalized <= 0.25f ? palette.Warning : palette.Text;
        DrawHeartGlyph(context, new Point(bounds.X + 6, bounds.Center.Y - 4), heartColor);
        DrawMeterText(context, bounds, string.Empty, valueLabel, palette);
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
            var pulseAmount = settings.Ui.ReducedMotion ? 0.96f : 0.68f + 0.28f * MathF.Sin((float)context.Time.TotalSeconds * 7f);
            var pulseOffset = settings.Ui.ReducedMotion ? 0 : (int)MathF.Round(MathF.Sin((float)context.Time.TotalSeconds * 7f));
            var pulse = new Rectangle(bounds.Right - 18, bounds.Y + 5 - pulseOffset, 10, 8);
            UiTheme.DrawRoundedRectangle(context, pulse, UiTheme.WithAlpha(palette.Warning, pulseAmount), 3);
        }
    }

    private static void DrawResourceCrest(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        float opacity)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var materials = UiTheme.ResolveMaterials(palette);
        var shadow = new Rectangle(bounds.X + 3, bounds.Y + 4, bounds.Width, bounds.Height);
        UiTheme.DrawRoundedRectangle(context, shadow, UiTheme.WithAlpha(materials.FrameShadow, opacity * 0.68f), 8);
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(materials.WoodDark, opacity), 8);
        UiTheme.DrawRoundedBorder(context, bounds, UiTheme.WithAlpha(materials.Brass, opacity), 8, 2);
        var inset = new Rectangle(bounds.X + 6, bounds.Y + 6, Math.Max(1, bounds.Width - 12), Math.Max(1, bounds.Height - 12));
        UiTheme.DrawRoundedRectangle(context, inset, UiTheme.WithAlpha(palette.Danger, opacity * 0.82f), 6);
        UiTheme.DrawRoundedBorder(context, inset, UiTheme.WithAlpha(materials.BrassLight, opacity * 0.72f), 6, 1);
        var notchWidth = Math.Max(4, bounds.Width / 3);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(bounds.Center.X - notchWidth / 2, bounds.Bottom - 5, notchWidth, 4),
            UiTheme.WithAlpha(materials.FrameShadow, opacity));
        DrawHeartGlyph(
            context,
            new Point(bounds.Center.X - 5, bounds.Center.Y - 4),
            UiTheme.WithAlpha(Color.White, opacity));
    }

    private static void DrawHealthPips(
        RenderContext context,
        Rectangle panel,
        int health,
        int maximumHealth,
        UiPalette palette,
        float opacity)
    {
        if (panel.Width < 126 || panel.Height < 24)
        {
            return;
        }

        const int pipCount = 8;
        const int pipWidth = 7;
        const int gap = 2;
        var startX = panel.Right - 10 - pipCount * pipWidth - (pipCount - 1) * gap;
        var filled = maximumHealth <= 0
            ? 0
            : Math.Clamp((int)MathF.Ceiling(health / (float)maximumHealth * pipCount), 0, pipCount);
        for (var index = 0; index < pipCount; index++)
        {
            var x = startX + index * (pipWidth + gap);
            var color = index < filled ? palette.Danger : UiTheme.ResolveMaterials(palette).WoodDark;
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 1, panel.Y + 8, 2, 2), UiTheme.WithAlpha(color, opacity));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 4, panel.Y + 8, 2, 2), UiTheme.WithAlpha(color, opacity));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x, panel.Y + 10, pipWidth, 3), UiTheme.WithAlpha(color, opacity));
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(x + 2, panel.Y + 13, 3, 2), UiTheme.WithAlpha(color, opacity));
        }
    }

    private static void DrawCompassFrame(
        RenderContext context,
        Rectangle bounds,
        UiPalette palette,
        float opacity)
    {
        if (bounds.Width < 20 || bounds.Height < 20)
        {
            return;
        }

        var materials = UiTheme.ResolveMaterials(palette);
        UiTheme.DrawRoundedRectangle(context, bounds, UiTheme.WithAlpha(materials.FrameShadow, opacity * 0.92f), Math.Min(12, bounds.Width / 4));
        UiTheme.DrawRoundedBorder(context, bounds, UiTheme.WithAlpha(materials.Brass, opacity * 0.90f), Math.Min(12, bounds.Width / 4), 2);
        var inset = new Rectangle(bounds.X + 4, bounds.Y + 4, Math.Max(1, bounds.Width - 8), Math.Max(1, bounds.Height - 8));
        UiTheme.DrawRoundedBorder(context, inset, UiTheme.WithAlpha(palette.AccentSoft, opacity * 0.72f), Math.Min(9, inset.Width / 4), 1);
        var center = bounds.Center;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X, bounds.Y + 6, 1, Math.Max(1, bounds.Height - 12)), UiTheme.WithAlpha(materials.BrassDark, opacity * 0.50f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(bounds.X + 6, center.Y, Math.Max(1, bounds.Width - 12), 1), UiTheme.WithAlpha(materials.BrassDark, opacity * 0.50f));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 1, center.Y - Math.Max(3, bounds.Height / 5), 3, Math.Max(4, bounds.Height / 5)), UiTheme.WithAlpha(materials.BrassLight, opacity));
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(center.X - 2, center.Y - 2, 5, 5), UiTheme.WithAlpha(palette.Accent, opacity));
        if (bounds.Width >= 40)
        {
            context.DebugText.Draw(new Vector2(center.X - 3, bounds.Y + 3), "N", materials.BrassLight, 1);
            context.DebugText.Draw(new Vector2(bounds.Right - 8, center.Y - 3), "E", palette.TextMuted, 1);
            context.DebugText.Draw(new Vector2(bounds.X + 3, center.Y - 3), "W", palette.TextMuted, 1);
        }
    }

    private static void DrawHeartGlyph(RenderContext context, Point origin, Color color)
    {
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 1, origin.Y, 3, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 6, origin.Y, 3, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X, origin.Y + 1, 10, 3), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 1, origin.Y + 4, 8, 2), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 3, origin.Y + 6, 4, 1), color);
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(origin.X + 4, origin.Y + 7, 2, 1), color);
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

internal readonly record struct HudWorldPresentationInput(
    string BiomeId,
    string BiomeDisplayName,
    int Day,
    double NormalizedTimeOfDay,
    bool IsNight,
    Game.Core.Weather.WeatherKind Weather,
    bool IsWorldEventActive,
    string? WorldEventId,
    int SelectedSlot,
    string SelectedItemId,
    string SelectedItemDisplayName,
    int ActiveEnemies);

internal sealed class HudWorldPresentationCache
{
    private string? _biomeId;
    private int _day = int.MinValue;
    private int _phase = int.MinValue;
    private Game.Core.Weather.WeatherKind _weather = (Game.Core.Weather.WeatherKind)(-1);
    private bool _eventActive;
    private string? _eventId;
    private int _selectedSlot = int.MinValue;
    private string? _selectedItemId;
    private int _activeEnemies = int.MinValue;

    public string BiomeLabel { get; private set; } = "WILDS";

    public string CompactBiomeLabel { get; private set; } = "WILDS";

    public string DayLabel { get; private set; } = "DAY 1  DAWN";

    public string AtmosphereLabel { get; private set; } = "CLEAR";

    public string ContextLabel { get; private set; } = "[1] EMPTY HAND";

    public string ThreatLabel { get; private set; } = "CALM";

    public bool HasThreats => _activeEnemies > 0;

    public void Update(GameFrameSnapshot snapshot, IItemDefinitionProvider? items)
    {
        var selectedSlot = Math.Clamp(snapshot.Player.SelectedHotbarSlot, 0, Math.Max(0, snapshot.Player.Hotbar.Count - 1));
        var selectedItemId = string.Empty;
        var selectedItemName = "EMPTY HAND";
        if (snapshot.Player.Hotbar.Count > 0)
        {
            var stack = snapshot.Player.Hotbar[selectedSlot].Stack;
            if (!stack.IsEmpty)
            {
                selectedItemId = stack.ItemId;
                selectedItemName = items?.GetById(stack.ItemId).DisplayName ?? stack.ItemId;
            }
        }

        var input = new HudWorldPresentationInput(
            snapshot.LivingWorld.BiomeId,
            snapshot.LivingWorld.BiomeDisplayName,
            snapshot.WorldTime.Day,
            snapshot.WorldTime.NormalizedTimeOfDay,
            snapshot.WorldTime.IsNight,
            snapshot.LivingWorld.Weather,
            snapshot.LivingWorld.IsWorldEventActive,
            snapshot.LivingWorld.WorldEventId,
            selectedSlot,
            selectedItemId,
            selectedItemName,
            snapshot.Hud.ActiveEnemies);
        Update(input);
    }

    internal void Update(in HudWorldPresentationInput input)
    {
        if (!string.Equals(_biomeId, input.BiomeId, StringComparison.Ordinal))
        {
            _biomeId = input.BiomeId;
            BiomeLabel = string.IsNullOrWhiteSpace(input.BiomeDisplayName) ? "WILDS" : input.BiomeDisplayName;
            CompactBiomeLabel = Abbreviate(BiomeLabel, 12);
        }

        var phase = ResolveDayPhase(input.NormalizedTimeOfDay, input.IsNight);
        if (_day != input.Day || _phase != phase)
        {
            _day = input.Day;
            _phase = phase;
            DayLabel = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"DAY {Math.Max(1, input.Day)}  {PhaseLabel(phase)}");
        }

        if (_weather != input.Weather ||
            _phase != phase ||
            _eventActive != input.IsWorldEventActive ||
            !string.Equals(_eventId, input.WorldEventId, StringComparison.Ordinal))
        {
            _weather = input.Weather;
            _eventActive = input.IsWorldEventActive;
            _eventId = input.WorldEventId;
            AtmosphereLabel = input.IsWorldEventActive && !string.IsNullOrWhiteSpace(input.WorldEventId)
                ? Abbreviate(input.WorldEventId, 18)
                : WeatherLabel(input.Weather);
        }

        if (_selectedSlot != input.SelectedSlot ||
            !string.Equals(_selectedItemId, input.SelectedItemId, StringComparison.Ordinal))
        {
            _selectedSlot = input.SelectedSlot;
            _selectedItemId = input.SelectedItemId;
            ContextLabel = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"[{Math.Clamp(input.SelectedSlot + 1, 1, 10) % 10}] {Abbreviate(input.SelectedItemDisplayName, 28)}");
        }

        if (_activeEnemies != input.ActiveEnemies)
        {
            _activeEnemies = Math.Max(0, input.ActiveEnemies);
            ThreatLabel = _activeEnemies == 0
                ? "CALM"
                : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"THREATS {_activeEnemies}");
        }
    }

    private static int ResolveDayPhase(double normalizedTime, bool isNight)
    {
        if (!double.IsFinite(normalizedTime))
        {
            return isNight ? 3 : 1;
        }

        var wrapped = normalizedTime - Math.Floor(normalizedTime);
        return wrapped switch
        {
            < 0.18 => 4,
            < 0.30 => 0,
            < 0.68 => 1,
            < 0.78 => 2,
            < 0.92 => 3,
            _ => 4
        };
    }

    private static string PhaseLabel(int phase) => phase switch
    {
        0 => "DAWN",
        1 => "DAY",
        2 => "DUSK",
        3 => "NIGHT",
        _ => "LATE NIGHT"
    };

    private static string WeatherLabel(Game.Core.Weather.WeatherKind weather) => weather switch
    {
        Game.Core.Weather.WeatherKind.Rain => "RAIN",
        Game.Core.Weather.WeatherKind.Storm => "STORM",
        Game.Core.Weather.WeatherKind.Fog => "FOG",
        Game.Core.Weather.WeatherKind.Snow => "SNOW",
        Game.Core.Weather.WeatherKind.Blizzard => "BLIZZARD",
        _ => "CLEAR"
    };

    private static string Abbreviate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Length <= maxLength
            ? value
            : value[..Math.Max(1, maxLength - 1)] + ".";
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
