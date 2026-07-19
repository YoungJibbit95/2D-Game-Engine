using Game.Client.Rendering.Performance;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class HighResolutionFrameLimiterTests
{
    [Fact]
    public void Limiter_PacesAtConfiguredHighRefreshWithoutChangingTheClockOnFirstFrame()
    {
        var clock = new FakeClock();
        using var limiter = new HighResolutionFrameLimiter(clock);
        limiter.Configure(165);

        limiter.WaitForNextFrame();
        Assert.Equal(0, clock.Timestamp);

        limiter.WaitForNextFrame();
        Assert.InRange(clock.Timestamp, 6_060, 6_100);
        limiter.WaitForNextFrame();
        Assert.InRange(clock.Timestamp, 12_121, 12_160);
        Assert.True(clock.SleepCalls > 0);
        Assert.True(clock.SpinCalls > 0);
    }

    [Fact]
    public void Limiter_RebasesAfterAFrameMissAndCanBeDisabled()
    {
        var clock = new FakeClock();
        using var limiter = new HighResolutionFrameLimiter(clock);
        limiter.Configure(120);
        limiter.WaitForNextFrame();

        clock.AdvanceMilliseconds(30);
        limiter.WaitForNextFrame();
        var afterMiss = clock.Timestamp;
        limiter.WaitForNextFrame();
        Assert.InRange(clock.Timestamp - afterMiss, 8_333, 8_370);

        limiter.Configure(0);
        var disabledAt = clock.Timestamp;
        limiter.WaitForNextFrame();
        Assert.Equal(disabledAt, clock.Timestamp);
        Assert.False(limiter.IsEnabled);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(361)]
    public void Configure_RejectsUnsupportedTargets(int target)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HighResolutionFrameLimiter(new FakeClock()).Configure(target));
    }

    private sealed class FakeClock : IFramePacingClock
    {
        public long Frequency => 1_000_000;

        public long Timestamp { get; private set; }

        public int SleepCalls { get; private set; }

        public int SpinCalls { get; private set; }

        public long GetTimestamp() => Timestamp;

        public void Sleep(int milliseconds)
        {
            SleepCalls++;
            Timestamp += milliseconds * 1_000L;
        }

        public void SpinOnce()
        {
            SpinCalls++;
            Timestamp += 25;
        }

        public void AdvanceMilliseconds(int milliseconds)
        {
            Timestamp += milliseconds * 1_000L;
        }
    }
}
