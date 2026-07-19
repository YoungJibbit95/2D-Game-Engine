using System.Diagnostics;

namespace Game.Core.Runtime;

public sealed class SimulationPhaseTelemetry
{
    private static readonly int PhaseCount = Enum.GetValues<GameSimulationPhase>().Length;
    private readonly long[] _lastElapsedTicks = new long[PhaseCount];
    private readonly long[] _lastAllocatedBytes = new long[PhaseCount];
    private readonly long[] _totalElapsedTicks = new long[PhaseCount];
    private readonly long[] _totalAllocatedBytes = new long[PhaseCount];
    private readonly long[] _samples = new long[PhaseCount];

    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }

    public PhaseMeasurementScope Measure(GameSimulationPhase phase)
    {
        return IsEnabled
            ? new PhaseMeasurementScope(
                this,
                phase,
                Stopwatch.GetTimestamp(),
                GC.GetAllocatedBytesForCurrentThread())
            : default;
    }

    public SimulationPhaseTelemetrySnapshot CaptureSnapshot()
    {
        var measurements = new SimulationPhaseMeasurement[PhaseCount];
        for (var index = 0; index < measurements.Length; index++)
        {
            var samples = _samples[index];
            measurements[index] = new SimulationPhaseMeasurement(
                (GameSimulationPhase)index,
                _lastElapsedTicks[index],
                _lastAllocatedBytes[index],
                _totalElapsedTicks[index],
                _totalAllocatedBytes[index],
                samples,
                samples == 0 ? 0d : _totalElapsedTicks[index] * 1000d / Stopwatch.Frequency / samples,
                samples == 0 ? 0d : _totalAllocatedBytes[index] / (double)samples);
        }

        return new SimulationPhaseTelemetrySnapshot(
            IsEnabled,
            Stopwatch.Frequency,
            ImmutableSnapshotList<SimulationPhaseMeasurement>.FromOwned(measurements));
    }

    public void Reset()
    {
        Array.Clear(_lastElapsedTicks);
        Array.Clear(_lastAllocatedBytes);
        Array.Clear(_totalElapsedTicks);
        Array.Clear(_totalAllocatedBytes);
        Array.Clear(_samples);
    }

    private void Record(GameSimulationPhase phase, long startTimestamp, long startAllocatedBytes)
    {
        var index = (int)phase;
        var elapsed = Math.Max(0, Stopwatch.GetTimestamp() - startTimestamp);
        var allocated = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - startAllocatedBytes);
        _lastElapsedTicks[index] = elapsed;
        _lastAllocatedBytes[index] = allocated;
        _totalElapsedTicks[index] = SaturatingAdd(_totalElapsedTicks[index], elapsed);
        _totalAllocatedBytes[index] = SaturatingAdd(_totalAllocatedBytes[index], allocated);
        _samples[index] = SaturatingAdd(_samples[index], 1);
    }

    private static long SaturatingAdd(long value, long amount)
    {
        return value > long.MaxValue - amount ? long.MaxValue : value + amount;
    }

    public readonly struct PhaseMeasurementScope : IDisposable
    {
        private readonly SimulationPhaseTelemetry? _owner;
        private readonly GameSimulationPhase _phase;
        private readonly long _startTimestamp;
        private readonly long _startAllocatedBytes;

        internal PhaseMeasurementScope(
            SimulationPhaseTelemetry owner,
            GameSimulationPhase phase,
            long startTimestamp,
            long startAllocatedBytes)
        {
            _owner = owner;
            _phase = phase;
            _startTimestamp = startTimestamp;
            _startAllocatedBytes = startAllocatedBytes;
        }

        public void Dispose()
        {
            _owner?.Record(_phase, _startTimestamp, _startAllocatedBytes);
        }
    }
}

public readonly record struct SimulationPhaseMeasurement(
    GameSimulationPhase Phase,
    long LastElapsedTicks,
    long LastAllocatedBytes,
    long TotalElapsedTicks,
    long TotalAllocatedBytes,
    long Samples,
    double AverageMilliseconds,
    double AverageAllocatedBytes);

public readonly record struct SimulationPhaseTelemetrySnapshot(
    bool IsEnabled,
    long TimestampFrequency,
    ImmutableSnapshotList<SimulationPhaseMeasurement> Measurements)
{
    public bool TryGet(GameSimulationPhase phase, out SimulationPhaseMeasurement measurement)
    {
        var index = (int)phase;
        if ((uint)index < (uint)Measurements.Count)
        {
            measurement = Measurements[index];
            return measurement.Phase == phase;
        }

        measurement = default;
        return false;
    }
}
