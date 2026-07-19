using Game.Client.Rendering;
using Game.Core.Diagnostics;
using Game.Core.Settings;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class PerformanceOverlay
{
    private const double RefreshIntervalSeconds = 0.2;
    private readonly string[] _names = new string[32];
    private readonly string[] _compactNames = new string[32];
    private readonly string[] _timings = new string[32];
    private readonly string[] _allocations = new string[32];
    private readonly bool[] _overBudget = new bool[32];
    private readonly bool[] _allocationWarnings = new bool[32];
    private string _frameSummary = "WAITING";
    private string _rendererSummary = "RENDERER WAITING";
    private string _rendererCompactSummary = "RENDER WAIT";
    private double _nextRefreshAt;
    private int _lineCount;

    public void Draw(RenderContext context, GameSettings settings)
    {
        if (!settings.Debug.ShowPerformanceProfiler)
        {
            return;
        }

        if (context.Time.TotalSeconds >= _nextRefreshAt)
        {
            Refresh(context, settings);
            _nextRefreshAt = context.Time.TotalSeconds + RefreshIntervalSeconds;
        }

        var palette = UiTheme.Resolve(settings);
        var layout = PixelPerformanceLayoutPlanner.Resolve(
            context.ViewportBounds,
            _lineCount,
            settings.Debug.ShowAllocationMetrics);
        if (layout.Panel.Width <= 0 || layout.Panel.Height <= 0)
        {
            return;
        }

        PixelUiPrimitives.DrawGlassSurface(
            context,
            layout.Panel,
            palette,
            settings.Ui.PanelOpacity * 0.96f,
            settings);
        UiTheme.DrawHeader(context, layout.Header, palette, settings.Ui.PanelOpacity * 0.92f, settings);
        context.DebugText.Draw(
            new Vector2(layout.Header.X + 10, layout.Header.Y + 9),
            layout.Density == PixelUiDensity.Compact ? "PERF" : "PERFORMANCE",
            palette.Accent,
            layout.Density == PixelUiDensity.Compact ? 1 : 2);
        var frameWidth = _frameSummary.Length * 6;
        context.DebugText.Draw(
            new Vector2(Math.Max(layout.Header.X + 38, layout.Header.Right - frameWidth - 10), layout.Header.Y + 11),
            _frameSummary,
            palette.TextMuted,
            1);

        if (layout.RendererCard.Height >= 14)
        {
            PixelUiPrimitives.DrawStatusChip(
                context,
                layout.RendererCard,
                palette,
                palette.Accent,
                settings.Ui.PanelOpacity * 0.88f,
                settings);
            context.DebugText.Draw(
                new Vector2(layout.RendererCard.X + 19, layout.RendererCard.Y + 8),
                layout.Density == PixelUiDensity.Compact ? _rendererCompactSummary : _rendererSummary,
                palette.TextMuted,
                1);
        }

        if (_lineCount == 0)
        {
            context.DebugText.Draw(new Vector2(layout.Rows.X, layout.Rows.Y + 2), "WAITING FOR SAMPLES", palette.TextMuted, 1);
            return;
        }

        var y = layout.Rows.Y;
        for (var index = 0; index < _lineCount; index++)
        {
            if (y + layout.RowHeight > layout.Rows.Bottom)
            {
                break;
            }

            var color = _overBudget[index] ? palette.Warning : palette.Text;
            if (_overBudget[index] || _allocationWarnings[index])
            {
                var warningColor = _overBudget[index] ? palette.Danger : palette.Warning;
                context.SpriteBatch.Draw(
                    context.Pixel,
                    new Rectangle(layout.Rows.X - 4, y + 2, 2, Math.Max(1, layout.RowHeight - 5)),
                    UiTheme.WithAlpha(warningColor, 0.9f));
            }

            context.DebugText.Draw(
                new Vector2(layout.NameX, y + 2),
                layout.Density == PixelUiDensity.Compact ? _compactNames[index] : _names[index],
                color,
                1);
            context.DebugText.Draw(
                new Vector2(layout.TimingX, y + 2),
                _timings[index],
                color,
                1);

            if (layout.ShowAllocationColumn)
            {
                context.DebugText.Draw(
                    new Vector2(layout.AllocationX, y + 2),
                    _allocations[index],
                    _allocationWarnings[index] ? palette.Warning : palette.TextMuted,
                    1);
            }

            y += layout.RowHeight;
        }
    }

    private void Refresh(RenderContext context, GameSettings settings)
    {
        var frameTimes = context.FrameTimes.Capture();
        _frameSummary = frameTimes.SampleCount == 0
            ? $"FRAME {context.Performance.FrameIndex}"
            : $"{1000d / Math.Max(0.001, frameTimes.AverageMilliseconds):0} FPS P99 {frameTimes.P99Milliseconds:0.0}";

        var renderer = context.RendererMetrics.Capture();
        if (!renderer.Availability.CountersAvailable || renderer.SampleCount == 0)
        {
            _rendererSummary = "RENDERER COUNTERS UNAVAILABLE";
            _rendererCompactSummary = "COUNTERS N/A";
        }
        else
        {
            var average = renderer.RollingAveragePerFrame;
            _rendererSummary = $"DRAW {average.DrawCount:0}  SPR {average.SpriteCount:0}  TEX {average.TextureCount:0}  RT {average.TargetCount:0}";
            _rendererCompactSummary = $"D {average.DrawCount:0} S {average.SpriteCount:0} T {average.TextureCount:0} R {average.TargetCount:0}";
        }

        var metrics = context.Performance.SnapshotSlowest(settings.Debug.ProfilerMetricLimit);
        _lineCount = Math.Min(metrics.Count, _names.Length);
        for (var index = 0; index < _lineCount; index++)
        {
            var metric = metrics[index];
            _names[index] = Trim(metric.Name, 24);
            _compactNames[index] = Trim(metric.Name, 15);
            _timings[index] = $"{metric.LastMilliseconds,5:0.00}/{metric.AverageMilliseconds,5:0.00}ms";
            _allocations[index] = FormatBytes(metric.LastAllocatedBytes);
            _overBudget[index] = metric.IsOverBudget;
            _allocationWarnings[index] = metric.LastAllocatedBytes > 1024;
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
