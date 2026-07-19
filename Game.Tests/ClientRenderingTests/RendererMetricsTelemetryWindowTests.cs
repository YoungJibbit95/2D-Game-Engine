using Game.Client.Diagnostics;
using Game.Client.Rendering.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class RendererMetricsTelemetryWindowTests
{
    [Fact]
    public void EndFrame_ReportsDeltaFromCapturedBaseline()
    {
        var telemetry = new RendererMetricsTelemetryWindow(4);
        var baseline = Counters(1, 10, 100, 50, 7, 3, 2, 1);
        var end = Counters(2, 14, 124, 62, 10, 5, 4, 2);

        Assert.True(telemetry.BeginFrame(baseline));
        Assert.True(telemetry.EndFrame(end));

        var snapshot = telemetry.Capture();
        Assert.Equal(baseline, snapshot.LatestBaseline);
        Assert.Equal(end, snapshot.LatestEnd);
        Assert.Equal(Counters(1, 4, 24, 12, 3, 2, 2, 1), snapshot.LatestDelta);
        Assert.Equal(1, snapshot.SampleCount);
    }

    [Fact]
    public void CounterReset_UsesPostResetValueAndResetClearsTelemetry()
    {
        var telemetry = new RendererMetricsTelemetryWindow(4);
        telemetry.BeginFrame(Counters(5, 50, 500, 250, 20, 10, 8, 6));
        telemetry.EndFrame(Counters(1, 4, 40, 20, 3, 2, 1, 1));

        Assert.Equal(Counters(1, 4, 40, 20, 3, 2, 1, 1), telemetry.Capture().LatestDelta);

        telemetry.Reset();

        var snapshot = telemetry.Capture();
        Assert.Equal(0, snapshot.SampleCount);
        Assert.Equal(0, snapshot.TotalSamples);
        Assert.Equal(default, snapshot.LatestDelta);
        Assert.False(telemetry.EndFrame(Counters(1, 1, 1, 1, 1, 1, 1, 1)));
    }

    [Fact]
    public void Capture_ReportsRollingAverageAndPeakAfterEviction()
    {
        var telemetry = new RendererMetricsTelemetryWindow(3);
        RecordDelta(telemetry, Counters(1, 2, 10, 20, 3, 1, 1, 1));
        RecordDelta(telemetry, Counters(2, 8, 40, 30, 5, 3, 2, 1));
        RecordDelta(telemetry, Counters(3, 4, 20, 10, 7, 2, 1, 3));
        RecordDelta(telemetry, Counters(4, 6, 30, 40, 1, 4, 3, 2));

        var snapshot = telemetry.Capture();
        Assert.Equal(3, snapshot.SampleCount);
        Assert.Equal(4, snapshot.TotalSamples);
        Assert.Equal(6, snapshot.RollingAveragePerFrame.DrawCount);
        Assert.Equal(30, snapshot.RollingAveragePerFrame.PrimitiveCount);
        Assert.Equal(8, snapshot.RollingPeakPerFrame.DrawCount);
        Assert.Equal(40, snapshot.RollingPeakPerFrame.PrimitiveCount);
        Assert.Equal(40, snapshot.RollingPeakPerFrame.SpriteCount);
        Assert.Equal(7, snapshot.RollingPeakPerFrame.TextureCount);
    }

    [Fact]
    public void EvictingUniquePeak_RecomputesPeakFromRetainedWindow()
    {
        var telemetry = new RendererMetricsTelemetryWindow(2);
        RecordDelta(telemetry, Counters(1, 10, 100, 40, 8, 4, 3, 2));
        RecordDelta(telemetry, Counters(1, 2, 20, 10, 2, 1, 1, 1));
        RecordDelta(telemetry, Counters(1, 3, 30, 12, 3, 2, 2, 1));

        var peak = telemetry.Capture().RollingPeakPerFrame;
        Assert.Equal(3, peak.DrawCount);
        Assert.Equal(30, peak.PrimitiveCount);
        Assert.Equal(12, peak.SpriteCount);
        Assert.Equal(3, peak.TextureCount);
    }

    [Fact]
    public void UnsupportedSource_ExposesAvailabilityWithoutRecordingSamples()
    {
        var availability = RendererMetricsAvailability.Unsupported("test backend");
        var telemetry = new RendererMetricsTelemetryWindow(4, availability);

        Assert.False(telemetry.BeginFrame(default));
        Assert.False(telemetry.EndFrame(Counters(1, 1, 1, 1, 1, 1, 1, 1)));

        var snapshot = telemetry.Capture();
        Assert.False(snapshot.Availability.CountersAvailable);
        Assert.Equal("test backend", snapshot.Availability.CounterSource);
        Assert.False(snapshot.Availability.GpuTimeAvailable);
        Assert.Null(snapshot.Availability.GpuTimeSource);
        var unavailableReason = snapshot.Availability.GpuTimeUnavailableReason;
        Assert.NotNull(unavailableReason);
        Assert.Contains("does not expose", unavailableReason);
        Assert.Equal(0, snapshot.SampleCount);
    }

    [Fact]
    public void MonoGameAvailability_LabelsSubmissionCountersAndRejectsFakeGpuTiming()
    {
        var availability = RendererMetricsAvailability.MonoGameGraphicsDeviceMetrics;

        Assert.True(availability.CountersAvailable);
        Assert.Contains("DrawCount", availability.CounterSemantics);
        Assert.Contains("SpriteBatch flushes", availability.CounterSemantics);
        Assert.Contains("TextureCount", availability.CounterSemantics);
        Assert.False(availability.GpuTimeAvailable);
        Assert.Null(availability.GpuTimeSource);
        var unavailableReason = availability.GpuTimeUnavailableReason;
        Assert.NotNull(unavailableReason);
        Assert.Contains("does not expose", unavailableReason);
        Assert.Contains("not reported as GPU time", unavailableReason);
    }

    [Fact]
    public void BeginAndEndFrame_DoNotAllocateAfterConstruction()
    {
        var telemetry = new RendererMetricsTelemetryWindow(8);
        var baseline = Counters(1, 2, 3, 4, 5, 6, 7, 8);
        var end = Counters(2, 4, 6, 8, 10, 12, 14, 16);
        for (var index = 0; index < 32; index++)
        {
            telemetry.BeginFrame(baseline);
            telemetry.EndFrame(end);
        }

        long checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
        {
            telemetry.BeginFrame(baseline);
            telemetry.EndFrame(end);
            checksum += telemetry.Capture().LatestDelta.DrawCount;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(20_000, checksum);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SmokeReportJson_ExportsAvailabilityAndRollingRendererMetrics()
    {
        var telemetry = new RendererMetricsTelemetryWindow(4);
        RecordDelta(telemetry, Counters(1, 3, 12, 6, 2, 1, 1, 1));
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"yjse-renderer-metrics-{Guid.NewGuid():N}");
        var screenshotPath = Path.Combine(directory, "smoke.png");

        try
        {
            var result = ClientSmokeResult.CaptureFailed(
                1,
                screenshotPath,
                new InvalidOperationException("expected test failure")) with
            {
                RendererMetrics = telemetry.Capture()
            };

            result.WriteJsonForScreenshot();

            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.ChangeExtension(screenshotPath, ".json")));
            var rendererMetrics = document.RootElement.GetProperty("RendererMetrics");
            var availability = rendererMetrics.GetProperty("Availability");
            Assert.True(availability.GetProperty("CountersAvailable").GetBoolean());
            Assert.False(availability.GetProperty("GpuTimeAvailable").GetBoolean());
            var unavailableReason = availability
                .GetProperty("GpuTimeUnavailableReason")
                .GetString();
            Assert.NotNull(unavailableReason);
            Assert.Contains("does not expose", unavailableReason);
            Assert.Equal(
                "MonoGame GraphicsDevice.Metrics",
                availability.GetProperty("CounterSource").GetString());
            Assert.Equal(3, rendererMetrics
                .GetProperty("RollingAveragePerFrame")
                .GetProperty("DrawCount")
                .GetDouble());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SmokeFailureBeforeRendererStartup_ReportsMetricsAsNotCaptured()
    {
        var result = ClientSmokeResult.CaptureFailed(
            0,
            null,
            new InvalidOperationException("startup failed"));

        Assert.False(result.RendererMetrics.Availability.CountersAvailable);
        Assert.Equal("not captured", result.RendererMetrics.Availability.CounterSource);
        Assert.False(result.RendererMetrics.Availability.GpuTimeAvailable);
    }

    private static void RecordDelta(
        RendererMetricsTelemetryWindow telemetry,
        RendererMetricCounters delta)
    {
        telemetry.BeginFrame(default);
        telemetry.EndFrame(delta);
    }

    private static RendererMetricCounters Counters(
        long clear,
        long draw,
        long primitive,
        long sprite,
        long texture,
        long target,
        long pixelShader,
        long vertexShader)
    {
        return new RendererMetricCounters(
            clear,
            draw,
            primitive,
            sprite,
            texture,
            target,
            pixelShader,
            vertexShader);
    }
}
