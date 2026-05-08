using Game.Core.Time;
using Xunit;

namespace Game.Tests.TimeTests;

public sealed class WorldTimeTests
{
    [Fact]
    public void Update_AdvancesDayWhenDayLengthWraps()
    {
        var time = new WorldTime(dayLengthSeconds: 10);

        time.Update(12);

        Assert.Equal(2, time.Day);
        Assert.Equal(2, time.TimeOfDaySeconds);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.2, true)]
    [InlineData(0.5, false)]
    [InlineData(0.8, true)]
    public void IsNight_UsesQuarterDayThresholds(double normalized, bool expectedNight)
    {
        var time = new WorldTime();

        time.SetTimeNormalized(normalized);

        Assert.Equal(expectedNight, time.IsNight);
    }

    [Fact]
    public void SetDayAndSetNight_MoveToExpectedPeriods()
    {
        var time = new WorldTime();

        time.SetDay();
        Assert.False(time.IsNight);

        time.SetNight();
        Assert.True(time.IsNight);
    }
}
