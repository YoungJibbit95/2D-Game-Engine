using Game.Client.Rendering;
using Game.Core;
using Game.Core.Actions;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Events;
using Game.Core.Interaction;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class GameplayFeedbackOverlay
{
    private readonly ActiveStatusEffect[] _visibleStatusEffects =
        new ActiveStatusEffect[StatusEffectDockPlanner.MaximumCandidateCount];
    private MiningResult? _mining;
    private float _cooldownRemaining;
    private float _cooldownDuration;
    private float _messageRemaining;
    private string? _message;

    public void Update(float deltaSeconds)
    {
        var delta = Math.Max(0, deltaSeconds);
        _cooldownRemaining = Math.Max(0, _cooldownRemaining - delta);
        _messageRemaining = Math.Max(0, _messageRemaining - delta);
        if (_messageRemaining <= 0)
        {
            _message = null;
        }
    }

    public void Observe(PlayerItemUseResult result)
    {
        if (result.Mining.InProgress || result.Mining.Completed)
        {
            _mining = result.Mining;
        }
        else if (result.AttemptedKind == PlayerItemUseKind.Mine && result.Blocked)
        {
            _mining = null;
        }

        if (result.CooldownDuration > 0)
        {
            _cooldownDuration = result.CooldownDuration;
            _cooldownRemaining = result.CooldownRemaining;
        }

        if (result.Blocked && result.FailureReason != GameplayActionFailureReason.Cooldown)
        {
            ShowMessage(FormatFailure(result.FailureReason), 1.15f);
        }
        else if (result.Success && (result.HealthRestored > 0 || result.ManaRestored > 0 || result.StatusEffectsApplied > 0))
        {
            var parts = new List<string>(3);
            if (result.HealthRestored > 0)
            {
                parts.Add($"+{result.HealthRestored} HEALTH");
            }

            if (result.ManaRestored > 0)
            {
                parts.Add($"+{result.ManaRestored} MANA");
            }

            if (result.StatusEffectsApplied > 0)
            {
                parts.Add($"{result.StatusEffectsApplied} EFFECT");
            }

            ShowMessage(string.Join("   ", parts), 1.4f);
        }
    }

    public void CancelMining()
    {
        _mining = null;
    }

    public void Draw(
        RenderContext context,
        Camera2D camera,
        PlayerEntity? player,
        GameSettings settings)
    {
        var palette = UiTheme.Resolve(settings);
        var plannedStatusCount = player is null
            ? 0
            : StatusEffectDockPlanner.Build(player.StatusEffects, _visibleStatusEffects);
        var statusCount = Math.Min(
            plannedStatusCount,
            PixelGameplayFeedbackLayoutPlanner.MaximumVisibleStatusEffects);
        var layout = PixelGameplayFeedbackLayoutPlanner.Resolve(context.ViewportBounds, statusCount);
        DrawMiningProgress(context, camera, palette, settings);
        DrawCooldown(context, palette, settings, layout.CooldownTrack);
        DrawMessage(context, palette, settings, layout.MessagePanel);
        DrawStatusEffects(context, palette, settings, layout, statusCount);
    }

    private void DrawMiningProgress(
        RenderContext context,
        Camera2D camera,
        UiPalette palette,
        GameSettings settings)
    {
        if (_mining is not { } mining || (!mining.InProgress && !mining.Completed))
        {
            return;
        }

        var tileWorld = new Vector2(
            mining.TilePosition.X * GameConstants.TileSize,
            mining.TilePosition.Y * GameConstants.TileSize);
        var tileScreen = camera.WorldToScreen(tileWorld, context.ViewportBounds);
        var tileSize = Math.Max(2, (int)MathF.Ceiling(GameConstants.TileSize * camera.Zoom));
        var barWidth = Math.Max(30, tileSize + 12);
        var bounds = new Rectangle(
            (int)MathF.Round(tileScreen.X + tileSize / 2f - barWidth / 2f),
            (int)MathF.Round(tileScreen.Y - 10),
            barWidth,
            6);

        UiTheme.DrawProgressBar(context, bounds, mining.Progress, palette);
        var crackColor = UiTheme.WithAlpha(palette.Warning, 0.35f + mining.Progress * 0.6f);
        var tileBounds = new Rectangle((int)tileScreen.X, (int)tileScreen.Y, tileSize, tileSize);
        var centerX = tileBounds.Center.X;
        var centerY = tileBounds.Center.Y;
        context.SpriteBatch.Draw(context.Pixel, new Rectangle(centerX, tileBounds.Y + 2, 1, Math.Max(1, centerY - tileBounds.Y)), crackColor);
        if (mining.Progress >= 0.35f)
        {
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(tileBounds.X + 2, centerY, Math.Max(1, centerX - tileBounds.X), 1), crackColor);
        }

        if (mining.Progress >= 0.7f)
        {
            context.SpriteBatch.Draw(context.Pixel, new Rectangle(centerX, centerY, Math.Max(1, tileBounds.Right - centerX - 2), 1), crackColor);
        }
    }

    private void DrawCooldown(
        RenderContext context,
        UiPalette palette,
        GameSettings settings,
        Rectangle bounds)
    {
        if (_cooldownRemaining <= 0 || _cooldownDuration <= 0 || bounds.IsEmpty)
        {
            return;
        }

        var progress = 1f - _cooldownRemaining / _cooldownDuration;
        PixelUiPrimitives.DrawMeter(
            context,
            bounds,
            progress,
            palette,
            palette.Accent,
            settings.Ui.HudOpacity,
            settings,
            segmented: true,
            emphasized: progress >= 0.92f);
    }

    private void DrawMessage(
        RenderContext context,
        UiPalette palette,
        GameSettings settings,
        Rectangle bounds)
    {
        if (string.IsNullOrWhiteSpace(_message) || _messageRemaining <= 0 || bounds.IsEmpty)
        {
            return;
        }

        PixelUiPrimitives.DrawGlassSurface(
            context,
            bounds,
            palette,
            settings.Ui.HudOpacity * 0.9f,
            settings);
        var accent = new Rectangle(bounds.X + 4, bounds.Y + 5, 3, Math.Max(0, bounds.Height - 10));
        UiTheme.DrawRoundedRectangle(context, accent, palette.Warning, 1);
        context.DebugText.Draw(
            new Vector2(bounds.X + 13, bounds.Y + Math.Max(4, (bounds.Height - 7) / 2)),
            _message,
            palette.Warning,
            1);
    }

    private void DrawStatusEffects(
        RenderContext context,
        UiPalette palette,
        GameSettings settings,
        in PixelGameplayFeedbackLayout layout,
        int count)
    {
        if (count <= 0 || layout.StatusDock.IsEmpty)
        {
            return;
        }

        PixelUiPrimitives.DrawGlassSurface(
            context,
            layout.StatusDock,
            palette,
            settings.Ui.HudOpacity * 0.8f,
            settings);
        for (var index = 0; index < count; index++)
        {
            var effect = _visibleStatusEffects[index];
            var bounds = layout.StatusSlot(index);
            var color = effect.Definition.Kind == StatusEffectKind.Debuff
                ? palette.Danger
                : new Color(72, 171, 118);
            var urgent = effect.Definition.Kind == StatusEffectKind.Debuff && effect.RemainingSeconds <= 3f;
            UiTheme.DrawRoundedRectangle(
                context,
                bounds,
                UiTheme.WithAlpha(palette.SurfaceRaised, settings.Ui.HudOpacity * 0.94f),
                5);
            UiTheme.DrawRoundedBorder(
                context,
                bounds,
                UiTheme.WithAlpha(urgent ? palette.Warning : color, urgent ? 1f : 0.86f),
                5,
                urgent ? 2 : 1);

            var icon = new Rectangle(bounds.X + 6, bounds.Y + 5, Math.Max(6, bounds.Width - 12), Math.Max(6, bounds.Height - 12));
            UiTheme.DrawRoundedRectangle(context, icon, UiTheme.WithAlpha(color, 0.24f), 3);
            DrawStatusRune(context, icon, effect.Definition.Id, color);

            var duration = Math.Max(0.001f, effect.Definition.DurationSeconds);
            var remaining = Math.Clamp(effect.RemainingSeconds / duration, 0f, 1f);
            var track = new Rectangle(bounds.X + 4, bounds.Bottom - 4, Math.Max(0, bounds.Width - 8), 2);
            context.SpriteBatch.Draw(context.Pixel, track, UiTheme.WithAlpha(palette.Backdrop, 0.86f));
            var fillWidth = (int)MathF.Round(track.Width * remaining);
            if (fillWidth > 0)
            {
                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(track.X, track.Y, fillWidth, track.Height),
                    urgent ? palette.Warning : color);
            }
        }
    }

    private static void DrawStatusRune(
        RenderContext context,
        Rectangle bounds,
        string id,
        Color color)
    {
        var hash = 2166136261u;
        for (var index = 0; index < id.Length; index++)
        {
            hash = (hash ^ char.ToUpperInvariant(id[index])) * 16777619u;
        }

        var cell = Math.Max(1, Math.Min(bounds.Width, bounds.Height) / 4);
        var originX = bounds.Center.X - cell * 3 / 2;
        var originY = bounds.Center.Y - cell * 3 / 2;
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var bit = row * 3 + column;
                if (((hash >> bit) & 1u) == 0u && bit != 4)
                {
                    continue;
                }

                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(originX + column * cell, originY + row * cell, cell, cell),
                    color);
            }
        }
    }

    private void ShowMessage(string message, float durationSeconds)
    {
        _message = message;
        _messageRemaining = Math.Max(_messageRemaining, durationSeconds);
    }

    private static string FormatFailure(GameplayActionFailureReason reason)
    {
        return reason switch
        {
            GameplayActionFailureReason.NoSelectedItem => "NO ITEM SELECTED",
            GameplayActionFailureReason.OutOfReach => "OUT OF REACH",
            GameplayActionFailureReason.Occupied => "TARGET OCCUPIED",
            GameplayActionFailureReason.InsufficientToolPower => "TOOL POWER TOO LOW",
            GameplayActionFailureReason.InsufficientMana => "NOT ENOUGH MANA",
            GameplayActionFailureReason.InsufficientAmmo => "NO AMMUNITION",
            GameplayActionFailureReason.InsufficientItem => "ITEM REQUIRED",
            GameplayActionFailureReason.UnsupportedPlacement => "BLOCK NEEDS SUPPORT",
            GameplayActionFailureReason.NoBenefit => "NO EFFECT",
            GameplayActionFailureReason.InvalidTarget => "INVALID TARGET",
            _ => reason.ToString().ToUpperInvariant()
        };
    }
}
