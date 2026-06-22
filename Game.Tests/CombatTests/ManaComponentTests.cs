using Game.Core.Combat;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class ManaComponentTests
{
    [Fact]
    public void TrySpend_ConsumesManaAndBlocksWhenInsufficient()
    {
        var mana = new ManaComponent(maxMana: 40, currentMana: 18);

        Assert.True(mana.TrySpend(12));
        Assert.Equal(6, mana.Current);
        Assert.False(mana.TrySpend(7));
        Assert.Equal(6, mana.Current);
    }

    [Fact]
    public void Update_RegeneratesManaAfterDelay()
    {
        var mana = new ManaComponent(maxMana: 40, currentMana: 20);

        Assert.True(mana.TrySpend(10));
        mana.Update(0.6f, regenPerSecond: 20);
        Assert.Equal(10, mana.Current);
        mana.Update(0.7f, regenPerSecond: 20);
        mana.Update(0.5f, regenPerSecond: 20);

        Assert.Equal(20, mana.Current);
    }

    [Fact]
    public void SetMax_PreservesCurrentRatio()
    {
        var mana = new ManaComponent(maxMana: 100, currentMana: 50);

        mana.SetMax(40);

        Assert.Equal(40, mana.Max);
        Assert.Equal(20, mana.Current);
    }
}
