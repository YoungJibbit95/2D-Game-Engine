using Game.Core.Effects;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public readonly record struct PixelGameplayFeedbackLayout(
    PixelUiDensity Density,
    Rectangle StatusDock,
    Rectangle MessagePanel,
    Rectangle CooldownTrack,
    int StatusSlotSize,
    int StatusSlotGap,
    int StatusColumns,
    int StatusCount)
{
    public Rectangle StatusSlot(int index)
    {
        if (index < 0 || index >= StatusCount || StatusColumns <= 0)
        {
            return Rectangle.Empty;
        }

        var column = index % StatusColumns;
        var row = index / StatusColumns;
        return new Rectangle(
            StatusDock.X + 6 + column * (StatusSlotSize + StatusSlotGap),
            StatusDock.Y + 6 + row * (StatusSlotSize + StatusSlotGap),
            StatusSlotSize,
            StatusSlotSize);
    }
}

public static class PixelGameplayFeedbackLayoutPlanner
{
    public const int MaximumVisibleStatusEffects = 10;

    public static PixelGameplayFeedbackLayout Resolve(Rectangle viewport, int statusEffectCount)
    {
        var density = viewport.Width switch
        {
            < 720 => PixelUiDensity.Compact,
            >= 1440 => PixelUiDensity.Expanded,
            _ => PixelUiDensity.Regular
        };
        var edge = density switch
        {
            PixelUiDensity.Compact => 8,
            PixelUiDensity.Expanded => 24,
            _ => 16
        };
        var slotSize = density switch
        {
            PixelUiDensity.Compact => 24,
            PixelUiDensity.Expanded => 34,
            _ => 30
        };
        var gap = density == PixelUiDensity.Compact ? 3 : 4;
        var count = Math.Clamp(statusEffectCount, 0, MaximumVisibleStatusEffects);
        var maximumColumns = density == PixelUiDensity.Compact ? 5 : MaximumVisibleStatusEffects;
        var availableWidth = Math.Max(0, viewport.Width - edge * 2 - 12);
        var columnsByWidth = Math.Max(1, (availableWidth + gap) / Math.Max(1, slotSize + gap));
        var columns = count == 0 ? 0 : Math.Min(count, Math.Min(maximumColumns, columnsByWidth));
        var rows = columns == 0 ? 0 : (count + columns - 1) / columns;
        var dockWidth = columns == 0 ? 0 : columns * slotSize + Math.Max(0, columns - 1) * gap + 12;
        var dockHeight = rows == 0 ? 0 : rows * slotSize + Math.Max(0, rows - 1) * gap + 12;
        dockWidth = Math.Min(dockWidth, Math.Max(0, viewport.Width - edge * 2));
        dockHeight = Math.Min(dockHeight, Math.Max(0, viewport.Height - edge * 2));
        var dockX = Math.Max(viewport.X, viewport.Right - edge - dockWidth);
        var desiredDockY = viewport.Y + edge + 120;
        var dockY = Math.Clamp(
            desiredDockY,
            viewport.Y,
            Math.Max(viewport.Y, viewport.Bottom - edge - dockHeight));
        var statusDock = new Rectangle(dockX, dockY, dockWidth, dockHeight);

        var availableMessageWidth = Math.Max(0, viewport.Width - edge * 2);
        var messageWidth = Math.Min(density == PixelUiDensity.Compact ? 300 : 420, availableMessageWidth);
        var messageHeight = Math.Min(density == PixelUiDensity.Compact ? 26 : 30, Math.Max(0, viewport.Height));
        var hotbarClearance = density == PixelUiDensity.Compact ? 70 : 88;
        var messageX = viewport.X + Math.Max(0, (viewport.Width - messageWidth) / 2);
        var messageY = Math.Clamp(
            viewport.Bottom - edge - hotbarClearance - messageHeight - 10,
            viewport.Y,
            Math.Max(viewport.Y, viewport.Bottom - messageHeight));
        var message = new Rectangle(messageX, messageY, messageWidth, messageHeight);
        var cooldownWidth = Math.Min(Math.Max(0, messageWidth - 56), 248);
        var cooldownX = viewport.X + Math.Max(0, (viewport.Width - cooldownWidth) / 2);
        var cooldownY = Math.Min(viewport.Bottom - 4, message.Bottom + 5);
        var cooldown = new Rectangle(
            cooldownX,
            cooldownY,
            cooldownWidth,
            Math.Max(0, Math.Min(7, viewport.Bottom - cooldownY)));

        return new PixelGameplayFeedbackLayout(
            density,
            statusDock,
            message,
            cooldown,
            slotSize,
            gap,
            columns,
            count);
    }
}

public static class StatusEffectDockPlanner
{
    public const int MaximumCandidateCount = 64;

    public static int Build(
        StatusEffectCollection effects,
        Span<ActiveStatusEffect> destination)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var count = effects.CopyActiveEffectsTo(destination);
        for (var index = 1; index < count; index++)
        {
            var current = destination[index];
            var insertAt = index;
            while (insertAt > 0 && Compare(current, destination[insertAt - 1]) < 0)
            {
                destination[insertAt] = destination[insertAt - 1];
                insertAt--;
            }

            destination[insertAt] = current;
        }

        return count;
    }

    private static int Compare(ActiveStatusEffect left, ActiveStatusEffect right)
    {
        var kind = (int)right.Definition.Kind - (int)left.Definition.Kind;
        return kind != 0
            ? kind
            : string.Compare(
                left.Definition.Id,
                right.Definition.Id,
                StringComparison.OrdinalIgnoreCase);
    }
}
