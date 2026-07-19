using Game.Client.UI;
using Game.Core.Effects;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class GameplayFeedbackPresentationTests
{
    [Theory]
    [InlineData(320, 180, 10)]
    [InlineData(640, 360, 6)]
    [InlineData(1280, 720, 10)]
    [InlineData(2560, 1440, 10)]
    public void Layout_ContainsFeedbackSurfacesAndStatusSlots(int width, int height, int statusCount)
    {
        var viewport = new Rectangle(0, 0, width, height);

        var layout = PixelGameplayFeedbackLayoutPlanner.Resolve(viewport, statusCount);

        AssertContained(viewport, layout.StatusDock);
        AssertContained(viewport, layout.MessagePanel);
        AssertContained(viewport, layout.CooldownTrack);
        Assert.Equal(Math.Min(statusCount, PixelGameplayFeedbackLayoutPlanner.MaximumVisibleStatusEffects), layout.StatusCount);
        for (var index = 0; index < layout.StatusCount; index++)
        {
            AssertContained(layout.StatusDock, layout.StatusSlot(index));
        }

        Assert.Equal(Rectangle.Empty, layout.StatusSlot(-1));
        Assert.Equal(Rectangle.Empty, layout.StatusSlot(layout.StatusCount));
    }

    [Fact]
    public void StatusPlanner_PrioritizesDebuffsAndUsesStableIdOrder()
    {
        var effects = CreateEffects();
        var destination = new ActiveStatusEffect[StatusEffectDockPlanner.MaximumCandidateCount];

        var count = StatusEffectDockPlanner.Build(effects, destination);

        Assert.Equal(4, count);
        Assert.Equal(StatusEffectKind.Debuff, destination[0].Definition.Kind);
        Assert.Equal("burning", destination[0].Definition.Id);
        Assert.Equal("poisoned", destination[1].Definition.Id);
        Assert.Equal(StatusEffectKind.Buff, destination[2].Definition.Kind);
        Assert.Equal("haste", destination[2].Definition.Id);
        Assert.Equal("regeneration", destination[3].Definition.Id);
    }

    [Fact]
    public void LayoutAndStatusPlanning_AreAllocationFreeInSteadyState()
    {
        var effects = CreateEffects();
        var destination = new ActiveStatusEffect[StatusEffectDockPlanner.MaximumCandidateCount];
        var viewport = new Rectangle(0, 0, 1920, 1080);
        _ = StatusEffectDockPlanner.Build(effects, destination);
        _ = PixelGameplayFeedbackLayoutPlanner.Resolve(viewport, destination.Length);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0;

        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var count = StatusEffectDockPlanner.Build(effects, destination);
            var layout = PixelGameplayFeedbackLayoutPlanner.Resolve(viewport, count);
            checksum += layout.StatusSlot(iteration % count).X;
        }

        Assert.True(checksum > 0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static StatusEffectCollection CreateEffects()
    {
        var effects = new StatusEffectCollection();
        effects.Apply(CreateDefinition("regeneration", StatusEffectKind.Buff));
        effects.Apply(CreateDefinition("poisoned", StatusEffectKind.Debuff));
        effects.Apply(CreateDefinition("haste", StatusEffectKind.Buff));
        effects.Apply(CreateDefinition("burning", StatusEffectKind.Debuff));
        return effects;
    }

    private static StatusEffectDefinition CreateDefinition(string id, StatusEffectKind kind)
    {
        return new StatusEffectDefinition
        {
            Id = id,
            DisplayName = id,
            Kind = kind,
            DurationSeconds = 10f
        };
    }

    private static void AssertContained(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to be contained by {outer}.");
    }
}
