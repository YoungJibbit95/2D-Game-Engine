using Game.Client.UI;
using Game.Core.Combat;
using Game.Core.Runtime;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class AttackHudPresentationTests
{
    [Fact]
    public void Create_HidesIdleAttacksAndMapsActiveComboState()
    {
        Assert.False(AttackHudPresentation.Create(AttackRuntimeFrameSnapshot.Empty).IsVisible);

        var snapshot = new AttackRuntimeFrameSnapshot(
            "wooden_sword",
            "wooden_sword.combo",
            "cross-cut",
            2,
            AttackRuntimePhase.Active,
            1,
            0,
            true,
            false,
            ImmutableSnapshotList<AttackRuntimeEvent>.Empty,
            0);

        var presentation = AttackHudPresentation.Create(snapshot);

        Assert.True(presentation.IsVisible);
        Assert.Equal("ACTIVE", presentation.PhaseLabel);
        Assert.Equal(2, presentation.ComboNumber);
        Assert.True(presentation.HasQueuedCombo);
    }
}
