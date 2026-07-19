using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Game.Client.Rendering.Performance;

public interface IFramePacingClock
{
    long Frequency { get; }

    long GetTimestamp();

    void Sleep(int milliseconds);

    void SpinOnce();
}

public sealed class StopwatchFramePacingClock : IFramePacingClock
{
    public static StopwatchFramePacingClock Instance { get; } = new();
    private readonly SafeWaitHandle? _highResolutionTimer;

    private StopwatchFramePacingClock()
    {
        if (OperatingSystem.IsWindows())
        {
            _highResolutionTimer = NativeTimerMethods.CreateWaitableTimerEx(
                IntPtr.Zero,
                null,
                NativeTimerMethods.CreateWaitableTimerHighResolution,
                NativeTimerMethods.TimerAllAccess);
            if (_highResolutionTimer.IsInvalid)
            {
                _highResolutionTimer.Dispose();
                _highResolutionTimer = NativeTimerMethods.CreateWaitableTimerEx(
                    IntPtr.Zero,
                    null,
                    0,
                    NativeTimerMethods.TimerAllAccess);
            }
        }
    }

    public long Frequency => Stopwatch.Frequency;

    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public void Sleep(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            return;
        }

        if (_highResolutionTimer is { IsInvalid: false, IsClosed: false })
        {
            var dueTime100Nanoseconds = -milliseconds * 10_000L;
            if (NativeTimerMethods.SetWaitableTimerEx(
                    _highResolutionTimer,
                    ref dueTime100Nanoseconds,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0))
            {
                _ = NativeTimerMethods.WaitForSingleObject(_highResolutionTimer, uint.MaxValue);
                return;
            }
        }

        Thread.Sleep(milliseconds);
    }

    public void SpinOnce() => Thread.SpinWait(32);

    private static class NativeTimerMethods
    {
        internal const uint CreateWaitableTimerHighResolution = 0x00000002;
        internal const uint TimerAllAccess = 0x001F0003;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeWaitHandle CreateWaitableTimerEx(
            IntPtr timerAttributes,
            string? timerName,
            uint flags,
            uint desiredAccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWaitableTimerEx(
            SafeWaitHandle timer,
            ref long dueTime,
            int period,
            IntPtr completionRoutine,
            IntPtr completionArgument,
            IntPtr wakeContext,
            uint tolerableDelay);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);
    }
}

/// <summary>
/// Paces variable rendering without changing the authoritative fixed simulation tick.
/// The coarse sleep keeps CPU use low and the short spin phase avoids millisecond-scale overshoot.
/// </summary>
public sealed class HighResolutionFrameLimiter : IDisposable
{
    private const double SpinThresholdMilliseconds = 4d;
    private readonly IFramePacingClock _clock;
    private double _intervalTicks;
    private double _nextFrameTicks;
    private bool _timerResolutionAcquired;
    private bool _disposed;

    public HighResolutionFrameLimiter(IFramePacingClock? clock = null)
    {
        _clock = clock ?? StopwatchFramePacingClock.Instance;
        if (_clock.Frequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clock), "Frame pacing clock frequency must be positive.");
        }
    }

    public int TargetFramesPerSecond { get; private set; }

    public bool IsEnabled => TargetFramesPerSecond > 0;

    public void Configure(int targetFramesPerSecond)
    {
        if (targetFramesPerSecond != 0 && targetFramesPerSecond is < 30 or > 360)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFramesPerSecond),
                "Frame limit must be unlimited or between 30 and 360 FPS.");
        }

        if (TargetFramesPerSecond == targetFramesPerSecond)
        {
            return;
        }

        TargetFramesPerSecond = targetFramesPerSecond;
        if (targetFramesPerSecond > 0)
        {
            AcquireTimerResolution();
        }
        else
        {
            ReleaseTimerResolution();
        }

        _intervalTicks = targetFramesPerSecond == 0
            ? 0d
            : _clock.Frequency / (double)targetFramesPerSecond;
        _nextFrameTicks = 0d;
    }

    public void WaitForNextFrame()
    {
        if (!IsEnabled)
        {
            return;
        }

        var now = _clock.GetTimestamp();
        if (_nextFrameTicks <= 0d)
        {
            _nextFrameTicks = now + _intervalTicks;
            return;
        }

        while (now < _nextFrameTicks)
        {
            var remainingMilliseconds =
                (_nextFrameTicks - now) * 1000d / _clock.Frequency;
            if (remainingMilliseconds > SpinThresholdMilliseconds)
            {
                var sleepMilliseconds = Math.Max(
                    1,
                    (int)Math.Floor(remainingMilliseconds - SpinThresholdMilliseconds));
                _clock.Sleep(sleepMilliseconds);
            }
            else
            {
                _clock.SpinOnce();
            }

            now = _clock.GetTimestamp();
        }

        var nextDeadline = _nextFrameTicks + _intervalTicks;
        _nextFrameTicks = nextDeadline <= now || nextDeadline - now > _intervalTicks * 2d
            ? now + _intervalTicks
            : nextDeadline;
    }

    public void Reset()
    {
        _nextFrameTicks = 0d;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseTimerResolution();
        GC.SuppressFinalize(this);
    }

    private void AcquireTimerResolution()
    {
        if (_timerResolutionAcquired ||
            !OperatingSystem.IsWindows() ||
            !ReferenceEquals(_clock, StopwatchFramePacingClock.Instance))
        {
            return;
        }

        _timerResolutionAcquired = NativeMethods.TimeBeginPeriod(1) == 0;
    }

    private void ReleaseTimerResolution()
    {
        if (!_timerResolutionAcquired)
        {
            return;
        }

        _ = NativeMethods.TimeEndPeriod(1);
        _timerResolutionAcquired = false;
    }

    private static class NativeMethods
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        internal static extern uint TimeBeginPeriod(uint periodMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        internal static extern uint TimeEndPeriod(uint periodMilliseconds);
    }
}
