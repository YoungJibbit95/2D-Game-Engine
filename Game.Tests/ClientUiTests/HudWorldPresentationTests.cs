using Game.Client.UI;
using Game.Core.Weather;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class HudWorldPresentationTests
{
    [Theory]
    [InlineData(0.25, false, "DAWN")]
    [InlineData(0.50, false, "DAY")]
    [InlineData(0.75, true, "DUSK")]
    [InlineData(0.85, true, "NIGHT")]
    [InlineData(0.00, true, "LATE NIGHT")]
    public void Update_LabelsWorldTimeAgainstAuthoritativeDayNightClock(
        double normalizedTime,
        bool isNight,
        string expectedPhase)
    {
        var cache = new HudWorldPresentationCache();
        var input = new HudWorldPresentationInput(
            "forest",
            "Forest",
            3,
            normalizedTime,
            isNight,
            WeatherKind.Clear,
            false,
            null,
            0,
            string.Empty,
            "Empty Hand",
            0);

        cache.Update(input);

        Assert.Equal($"DAY 3  {expectedPhase}", cache.DayLabel);
    }
}
