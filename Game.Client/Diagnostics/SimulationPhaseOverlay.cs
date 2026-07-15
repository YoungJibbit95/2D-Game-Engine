using Game.Client.Rendering;
using Game.Core.Runtime;
using Game.Core.Settings;
using Microsoft.Xna.Framework;
using System.Globalization;

namespace Game.Client.Diagnostics;

public sealed class SimulationPhaseOverlay
{
    private const double RefreshIntervalSeconds = 0.5;
    private readonly string[] _lines = new string[32];
    private double _elapsed;
    private int _lineCount;

    public void Update(double deltaSeconds, GameSimulation simulation, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Debug.ShowPerformanceProfiler)
        {
            _elapsed = 0;
            _lineCount = 0;
            return;
        }

        _elapsed += Math.Max(0, deltaSeconds);
        if (_elapsed < RefreshIntervalSeconds)
        {
            return;
        }

        _elapsed %= RefreshIntervalSeconds;
        var snapshot = simulation.PhaseTelemetry.CaptureSnapshot();
        var measurements = new SimulationPhaseMeasurement[snapshot.Measurements.Count];
        for (var index = 0; index < measurements.Length; index++)
        {
            measurements[index] = snapshot.Measurements[index];
        }

        Array.Sort(
            measurements,
            (left, right) => settings.Debug.ShowAllocationMetrics
                ? right.LastAllocatedBytes.CompareTo(left.LastAllocatedBytes)
                : right.LastElapsedTicks.CompareTo(left.LastElapsedTicks));

        _lineCount = Math.Min(
            Math.Min(settings.Debug.ProfilerMetricLimit, measurements.Length),
            _lines.Length);
        for (var index = 0; index < _lineCount; index++)
        {
            var measurement = measurements[index];
            var milliseconds = measurement.LastElapsedTicks * 1000d / snapshot.TimestampFrequency;
            _lines[index] = settings.Debug.ShowAllocationMetrics
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{measurement.Phase,-18} {milliseconds,6:0.000} ms {measurement.LastAllocatedBytes,7} B")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{measurement.Phase,-18} {milliseconds,6:0.000} ms");
        }
    }

    public void Draw(RenderContext context)
    {
        if (_lineCount == 0)
        {
            return;
        }

        const int lineHeight = 14;
        const int panelWidth = 360;
        var panelHeight = 28 + _lineCount * lineHeight;
        var x = Math.Max(context.ViewportBounds.Left + 8, context.ViewportBounds.Right - panelWidth - 12);
        var y = Math.Max(context.ViewportBounds.Top + 8, context.ViewportBounds.Bottom - panelHeight - 12);
        context.SpriteBatch.Draw(
            context.Pixel,
            new Rectangle(x, y, panelWidth, panelHeight),
            new Color(10, 14, 21, 224));
        context.DebugText.Draw(new Vector2(x + 8, y + 7), "FIXED TICK PHASES", new Color(118, 211, 255), 1);
        for (var index = 0; index < _lineCount; index++)
        {
            context.DebugText.Draw(
                new Vector2(x + 8, y + 24 + index * lineHeight),
                _lines[index],
                Color.White,
                1);
        }
    }
}
