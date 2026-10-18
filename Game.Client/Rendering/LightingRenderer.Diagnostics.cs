using Game.Client.Diagnostics;
using Microsoft.Xna.Framework;

namespace Game.Client.Rendering;

public sealed partial class LightingRenderer
{
    // Monotonic instance counters deliberately survive resource recreation.
    internal long DiagnosticPrepareExecutions { get; private set; }
    internal long DiagnosticUploadCalls { get; private set; }
    internal long DiagnosticUploadBytes { get; private set; }
    internal long DiagnosticResourceRebuilds { get; private set; }

    internal LightingDiagnosticFrame CaptureDiagnosticFrame(int frame, long tick, Vector2 camera,
        float zoom, Vector2 probe, long previousPrepares, long previousUploads,
        long previousBytes, long previousRebuilds)
    {
        var index = -1;
        if (_hasPreparedFrame && _previousVisibleWorld.Contains(probe) && _quality.MaskPixelCount > 0)
        {
            var x = Math.Clamp((int)((probe.X - _previousVisibleWorld.Left) * _quality.MaskSize.X / _previousVisibleWorld.Width), 0, _quality.MaskSize.X - 1);
            var y = Math.Clamp((int)((probe.Y - _previousVisibleWorld.Top) * _quality.MaskSize.Y / _previousVisibleWorld.Height), 0, _quality.MaskSize.Y - 1);
            index = y * _quality.MaskSize.X + x;
        }
        var prepared = DiagnosticPrepareExecutions > previousPrepares;
        return new LightingDiagnosticFrame(frame, tick, camera.X, camera.Y, zoom,
            DiagnosticPrepareExecutions - previousPrepares, DiagnosticUploadCalls - previousUploads,
            DiagnosticUploadBytes - previousBytes, DiagnosticResourceRebuilds - previousRebuilds,
            _quality.MaskSize.X, _quality.MaskSize.Y, prepared ? LastTelemetry.RaysCast : 0, prepared ? LastTelemetry.OccluderSamples : 0,
            LastTelemetry.PointLightsUsed, prepared ? LastTemporalTelemetry.ReprojectedPixels : 0,
            prepared ? LastTemporalTelemetry.DisocclusionRejectedPixels : 0, prepared && LastTemporalTelemetry.HistoryRejected,
            index < 0 ? 0 : _shadowValues[index], index < 0 ? 0 : _lightRed[index],
            index < 0 ? 0 : _lightGreen[index], index < 0 ? 0 : _lightBlue[index],
            index < 0 ? 0 : _bloomValues[index],
            ResourcesPrepared ? (long)_quality.MaskPixelCount * 4 * (_reflectionMap is null ? 9 : 12) : 0,
            index >= 0);
    }
}
