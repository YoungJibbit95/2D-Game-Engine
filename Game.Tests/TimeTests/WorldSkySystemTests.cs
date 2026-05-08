using Game.Core.Time;
using Xunit;

namespace Game.Tests.TimeTests;

public sealed class WorldSkySystemTests
{
    [Fact]
    public void Evaluate_ReturnsBrighterSkyDuringDayThanNight()
    {
        var system = new WorldSkySystem();
        var time = new WorldTime();

        time.SetNight();
        var night = system.Evaluate(time);

        time.SetDay();
        var day = system.Evaluate(time);

        Assert.True(day.Sunlight > night.Sunlight);
        Assert.True(day.B > night.B);
    }

    [Fact]
    public void Evaluate_WarmsSkyAtDawn()
    {
        var system = new WorldSkySystem();
        var time = new WorldTime();

        time.SetTimeNormalized(0.25);
        var dawn = system.Evaluate(time);

        Assert.True(dawn.R > 91);
    }
}
