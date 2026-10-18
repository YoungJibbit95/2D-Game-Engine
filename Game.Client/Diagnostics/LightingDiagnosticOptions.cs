using Microsoft.Xna.Framework;
using System.Text.Json.Serialization;

namespace Game.Client.Diagnostics;

public sealed record LightingDiagnosticOptions
{
    public string CameraPath { get; init; } = "Static";
    public int DynamicLightCount { get; init; } = 2;
    public float PanAmplitude { get; init; } = 48;
    public float ZoomAmplitude { get; init; } = 0.25f;
    public int PeriodTicks { get; init; } = 240;
    public bool DisableBloom { get; init; }
    public int ForceCoreLightingInterval { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AdvanceWorldTime { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ForceDeviceResetInterval { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DynamicLightLifetimeTicks { get; init; }

    internal void Validate(GoldenSceneFixture fixture)
    {
        if (fixture.StartState != ClientSmokeStartState.Playing ||
            CameraPath is not ("Static" or "Pan" or "Zoom" or "PanZoom") ||
            DynamicLightCount is < 0 or > 12 || fixture.DynamicLights != (DynamicLightCount > 0) ||
            !float.IsFinite(PanAmplitude) || PanAmplitude is < 0 or > 96 ||
            !float.IsFinite(ZoomAmplitude) || ZoomAmplitude is < 0 or > 0.5f ||
            fixture.Zoom - ZoomAmplitude < 1 || fixture.Zoom + ZoomAmplitude > 4 ||
            PeriodTicks is < 60 or > 1200 || PeriodTicks % 4 != 0 ||
            (ForceCoreLightingInterval != 0 && ForceCoreLightingInterval is < 60 or > 1200) ||
            (ForceDeviceResetInterval != 0 && ForceDeviceResetInterval is < 60 or > 1200) ||
            (DynamicLightLifetimeTicks != 0 && (!fixture.DynamicLights ||
                DynamicLightLifetimeTicks < 60 || DynamicLightLifetimeTicks > fixture.Frames - 60)))
        {
            throw new InvalidDataException("Invalid bounded lighting diagnostic workload.");
        }
    }

    internal (Vector2 Camera, float Zoom) Resolve(Vector2 anchor, float zoom, long tick)
    {
        var phase = (tick % PeriodTicks) / (float)PeriodTicks;
        var triangle = phase < 0.25f ? phase * 4 : phase < 0.75f ? 2 - phase * 4 : phase * 4 - 4;
        return (anchor + new Vector2(CameraPath is "Pan" or "PanZoom" ? triangle * PanAmplitude : 0, 0),
            zoom + (CameraPath is "Zoom" or "PanZoom" ? triangle * ZoomAmplitude : 0));
    }
}
