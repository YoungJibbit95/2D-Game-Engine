using System.Text.Json;
using Game.Client.Diagnostics;
using Game.Core.Diagnostics;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class LightingDiagnosticTests
{
    [Fact]
    public void OriginalFixtureSerializationAndLimitsRemainCompatible()
    {
        var fixture = new GoldenSceneFixture { SceneId = "forest_day" };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(fixture));
        Assert.False(json.RootElement.TryGetProperty("Diagnostic", out _));
        Assert.Throws<InvalidDataException>(() => (fixture with { Frames = 1800 }).Validate());
        (fixture with { Frames = 1800, Width = 2560, Height = 1440,
            Diagnostic = new LightingDiagnosticOptions { DynamicLightCount = 0 } }).Validate();
        Assert.Throws<InvalidDataException>(() => (fixture with
        {
            Diagnostic = new LightingDiagnosticOptions { DynamicLightCount = 0, ForceCoreLightingInterval = 59 }
        }).Validate());
    }

    [Theory]
    [InlineData("unknown", 240, 2)]
    [InlineData("Static", 241, 2)]
    [InlineData("Static", 240, 13)]
    public void InvalidDiagnosticWorkloadsFailClosed(string path, int period, int lights)
    {
        var fixture = new GoldenSceneFixture
        {
            SceneId = "diagnostic", DynamicLights = true,
            Diagnostic = new LightingDiagnosticOptions { CameraPath = path, PeriodTicks = period, DynamicLightCount = lights }
        };
        Assert.Throws<InvalidDataException>(fixture.Validate);
    }

    [Fact]
    public void CameraSweepHasFixedReversalsAndReturnsToAnchor()
    {
        var options = new LightingDiagnosticOptions { CameraPath = "PanZoom" };
        var anchor = new Vector2(-1000, 500);
        Assert.Equal((anchor, 2f), options.Resolve(anchor, 2, 0));
        Assert.Equal((anchor + new Vector2(48, 0), 2.25f), options.Resolve(anchor, 2, 60));
        Assert.Equal((anchor - new Vector2(48, 0), 1.75f), options.Resolve(anchor, 2, 180));
        Assert.Equal((anchor, 2f), options.Resolve(anchor, 2, 240));
        Assert.Equal(anchor, (options with { CameraPath = "Zoom" }).Resolve(anchor, 2, 60).Camera);
    }

    [Fact]
    public void RecorderRetainsRawInvocationTimesAndUnscheduledFrames()
    {
        var recorder = new LightingDiagnosticRecorder(60);
        var profiler = new PerformanceProfiler();
        profiler.Record("Presentation.LightingPrepare", 1, 8);
        recorder.Record(Frame(61), profiler);
        recorder.Record(Frame(62), profiler);
        profiler.Record("Presentation.LightingPrepare", 10, 16);
        recorder.Record(Frame(63), profiler);
        var capture = recorder.Capture();
        Assert.Equal(3, capture.Frames.Length);
        var scope = Assert.Single(capture.Scopes, scope => scope.Name == "Presentation.LightingPrepare");
        Assert.Equal(new[] { 1d, 10d }, scope.Samples.Select(sample => sample.Milliseconds));
        Assert.Equal(new[] { 61, 63 }, scope.Samples.Select(sample => sample.Frame));
        Assert.Equal("unknown", capture.GpuTiming);
        Assert.All(capture.Scopes, recorded => Assert.NotEmpty(recorded.Samples));
    }

    [Fact]
    public void RecorderRejectsLostInvocationsInsteadOfReportingLastAsMean()
    {
        var profiler = new PerformanceProfiler();
        profiler.Record("Frame.Draw", 1);
        profiler.Record("Frame.Draw", 2);
        Assert.Throws<InvalidOperationException>(() => new LightingDiagnosticRecorder(60).Record(Frame(61), profiler));
    }

    [Fact]
    [Trait("Category", "StrictAllocation")]
    public void RecorderAddsNoSteadyStateAllocation()
    {
        var profiler = new PerformanceProfiler();
        var recorder = new LightingDiagnosticRecorder(2048);
        for (var frame = 1; frame <= 64; frame++)
        {
            profiler.Record("Frame.Draw", 1);
            recorder.Record(Frame(frame), profiler);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 65; frame <= 1064; frame++)
        {
            profiler.Record("Frame.Draw", 1);
            recorder.Record(Frame(frame), profiler);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private static LightingDiagnosticFrame Frame(int frame) =>
        new(frame, frame, 0, 0, 2, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 36);
}
