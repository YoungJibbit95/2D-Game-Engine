using Game.Client.UI;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class HudOverlayTests
{
    [Fact]
    public void GuardBar_HidesAtRestWithFullStamina()
    {
        var guard = GuardBarPresentation.Create(
            isGuarding: false,
            isBroken: false,
            stamina: 100f,
            maximumStamina: 100f);

        Assert.False(guard.IsVisible);
        Assert.Equal(1f, guard.NormalizedStamina);
    }

    [Theory]
    [InlineData(true, false, 100f, 1f)]
    [InlineData(false, false, 40f, 0.4f)]
    [InlineData(false, true, 0f, 0f)]
    public void GuardBar_ShowsForGuardRecoveryAndBreak(
        bool isGuarding,
        bool isBroken,
        float stamina,
        float expectedNormalized)
    {
        var guard = GuardBarPresentation.Create(isGuarding, isBroken, stamina, maximumStamina: 100f);

        Assert.True(guard.IsVisible);
        Assert.Equal(isGuarding, guard.IsGuarding);
        Assert.Equal(isBroken, guard.IsBroken);
        Assert.Equal(expectedNormalized, guard.NormalizedStamina, precision: 3);
    }

    [Fact]
    public void GuardBar_ClampsInvalidStaminaAndRejectsInvalidMaximum()
    {
        var overfilled = GuardBarPresentation.Create(false, false, 180f, 100f);
        var invalid = GuardBarPresentation.Create(true, false, 10f, 0f);

        Assert.False(overfilled.IsVisible);
        Assert.Equal(1f, overfilled.NormalizedStamina);
        Assert.False(invalid.IsVisible);
    }
}
