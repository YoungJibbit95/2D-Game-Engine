using Game.Client.Diagnostics;
using Microsoft.Xna.Framework;

namespace Game.Client.GameStates;

public sealed partial class PlayingState
{
    private long _diagnosticPrepares;
    private long _diagnosticUploads;
    private long _diagnosticUploadBytes;
    private long _diagnosticRebuilds;

    internal void ResetLightingDiagnosticCounters(bool resetCorePhases = false)
    {
        _diagnosticPrepares = _lightingRenderer.DiagnosticPrepareExecutions;
        _diagnosticUploads = _lightingRenderer.DiagnosticUploadCalls;
        _diagnosticUploadBytes = _lightingRenderer.DiagnosticUploadBytes;
        _diagnosticRebuilds = _lightingRenderer.DiagnosticResourceRebuilds;
        if (resetCorePhases) { _simulation!.PhaseTelemetry.Reset(); }
    }

    internal Game.Core.Runtime.SimulationPhaseTelemetrySnapshot CaptureCorePhaseTelemetry() =>
        _simulation!.PhaseTelemetry.CaptureSnapshot();

    internal LightingDiagnosticFrame CaptureLightingDiagnosticFrame(int frame)
    {
        var sample = _lightingRenderer.CaptureDiagnosticFrame(frame, _frameSnapshot!.TickNumber,
            _camera.Position, _camera.Zoom, _goldenCamera + new Vector2(-96, -24),
            _diagnosticPrepares, _diagnosticUploads, _diagnosticUploadBytes, _diagnosticRebuilds);
        ResetLightingDiagnosticCounters();
        return sample;
    }

    private (Vector2 Camera, float Zoom) GoldenCameraAt(long tick) =>
        _goldenFixture?.Diagnostic?.Resolve(_goldenCamera, _goldenFixture.Zoom, tick) ?? (_goldenCamera, _goldenFixture!.Zoom);
}
