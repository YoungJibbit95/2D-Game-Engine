using Game.Client.Rendering;
using Game.Core.Diagnostics;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class PerformanceOverlay
{
    public void Draw(RenderContext context, GameSettings settings)
    {
        if (!settings.Debug.ShowPerformanceProfiler)
        {
            return;
        }

        var metrics = context.Performance.SnapshotSlowest(settings.Debug.ProfilerMetricLimit);
        var palette = UiTheme.Resolve(settings);
        var showAllocations = settings.Debug.ShowAllocationMetrics;
        var rowHeight = 18;
        var width = showAllocations ? 410 : 322;
        var height = 48 + Math.Max(1, metrics.Count) * rowHeight;
        var bounds = new Rectangle(
            Math.Max(8, context.ViewportBounds.Right - width - 12),
            10,
            Math.Min(width, context.ViewportBounds.Width - 16),
            Math.Min(height, context.ViewportBounds.Height - 20));

        UiTheme.DrawPanel(context, bounds, palette, settings.Ui.PanelOpacity * 0.96f);
        context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 9), "PERFORMANCE", palette.Accent, 2);
        context.DebugText.Draw(
            new Vector2(bounds.Right - 118, bounds.Y + 11),
            $"FRAME {context.Performance.FrameIndex}",
            palette.TextMuted,
            1);

        if (metrics.Count == 0)
        {
            context.DebugText.Draw(new Vector2(bounds.X + 10, bounds.Y + 35), "WAITING FOR SAMPLES", palette.TextMuted, 1);
            return;
        }

        var y = bounds.Y + 35;
        foreach (var metric in metrics)
        {
            if (y + rowHeight > bounds.Bottom - 4)
            {
                break;
            }

            var color = metric.IsOverBudget ? palette.Warning : palette.Text;
            context.DebugText.Draw(new Vector2(bounds.X + 10, y), Trim(metric.Name, 24), color, 1);
            context.DebugText.Draw(
                new Vector2(bounds.X + 190, y),
                $"{metric.LastMilliseconds,5:0.00}/{metric.AverageMilliseconds,5:0.00}ms",
                color,
                1);

            if (showAllocations)
            {
                context.DebugText.Draw(
                    new Vector2(bounds.X + 316, y),
                    FormatBytes(metric.LastAllocatedBytes),
                    metric.LastAllocatedBytes > 1024 ? palette.Warning : palette.TextMuted,
                    1);
            }

            y += rowHeight;
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024d * 1024d):0.0}MB",
            >= 1024 => $"{bytes / 1024d:0.0}KB",
            _ => $"{bytes}B"
        };
    }

    private static string Trim(string text, int maximumLength)
    {
        return text.Length <= maximumLength ? text : text[..(maximumLength - 1)] + ">";
    }
}
