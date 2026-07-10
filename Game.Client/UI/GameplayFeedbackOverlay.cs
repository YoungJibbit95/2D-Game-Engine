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
        DrawMiningProgress(context, camera, settings);
        DrawCooldown(context, settings);
        DrawMessage(context, settings);
        DrawStatusEffects(context, player, settings);
    }

    private void DrawMiningProgress(RenderContext context, Camera2D camera, GameSettings settings)
    {
        if (_mining is not { } mining || (!mining.InProgress && !mining.Completed))
        {
            return;
        }

        var palette = UiTheme.Resolve(settings);
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

    private void DrawCooldown(RenderContext context, GameSettings settings)
    {
        if (_cooldownRemaining <= 0 || _cooldownDuration <= 0)
        {
            return;
        }

        var palette = UiTheme.Resolve(settings);
        var width = Math.Min(220, context.ViewportBounds.Width - 32);
        var bounds = new Rectangle(
            context.ViewportBounds.Center.X - width / 2,
            context.ViewportBounds.Bottom - 76,
            width,
            8);
        var progress = 1f - _cooldownRemaining / _cooldownDuration;
        UiTheme.DrawProgressBar(context, bounds, progress, palette);
    }

    private void DrawMessage(RenderContext context, GameSettings settings)
    {
        if (string.IsNullOrWhiteSpace(_message) || _messageRemaining <= 0)
        {
            return;
        }

        var palette = UiTheme.Resolve(settings);
        var width = Math.Min(360, context.ViewportBounds.Width - 32);
        var bounds = new Rectangle(
            context.ViewportBounds.Center.X - width / 2,
            context.ViewportBounds.Bottom - 112,
            width,
            28);
        context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(palette.Backdrop, 0.78f));
        UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.Warning, 0.82f), 1);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 8), _message, palette.Warning, 1);
    }

    private static void DrawStatusEffects(RenderContext context, PlayerEntity? player, GameSettings settings)
    {
        if (player is null || player.StatusEffects.ActiveEffects.Count == 0)
        {
            return;
        }

        var palette = UiTheme.Resolve(settings);
        var effects = player.StatusEffects.ActiveEffects
            .OrderBy(effect => effect.Definition.Kind)
            .ThenBy(effect => effect.Definition.Id, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
        var x = context.ViewportBounds.Right - 40;
        var y = 136;
        foreach (var effect in effects)
        {
            var bounds = new Rectangle(x, y, 28, 28);
            var color = effect.Definition.Kind == StatusEffectKind.Debuff
                ? new Color(176, 68, 76)
                : new Color(72, 146, 103);
            context.SpriteBatch.Draw(context.Pixel, bounds, UiTheme.WithAlpha(color, 0.92f));
            UiTheme.DrawBorder(context, bounds, UiTheme.WithAlpha(palette.Text, 0.72f), 1);
            var initial = string.IsNullOrWhiteSpace(effect.Definition.DisplayName)
                ? "?"
                : effect.Definition.DisplayName[..1].ToUpperInvariant();
            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 6), initial, palette.Text, 2);
            context.DebugText.Draw(new Vector2(bounds.X - 2, bounds.Bottom + 2), $"{effect.RemainingSeconds:0.0}s", palette.TextMuted, 1);
            x -= 36;
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
