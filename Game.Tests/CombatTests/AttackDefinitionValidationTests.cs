using Game.Core.Combat;
using System.Numerics;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class AttackDefinitionValidationTests
{
    [Fact]
    public void Timeline_RejectsOverlappingOrGappedPhases()
    {
        var overlapping = new AttackTimelineDefinition
        {
            Startup = new AttackPhaseWindow(0, 3),
            Active = new AttackPhaseWindow(2, 5),
            Recovery = new AttackPhaseWindow(5, 7)
        };
        var gapped = overlapping with
        {
            Startup = new AttackPhaseWindow(0, 2),
            Active = new AttackPhaseWindow(3, 5)
        };

        Assert.Throws<InvalidOperationException>(overlapping.Validate);
        Assert.Throws<InvalidOperationException>(gapped.Validate);
    }

    [Fact]
    public void Timeline_RejectsComboWindowOutsideAction()
    {
        var timeline = AttackTimelineDefinition.Create(
            startupTicks: 2,
            activeTicks: 3,
            recoveryTicks: 2,
            comboWindow: new AttackPhaseWindow(6, 8));

        Assert.Throws<InvalidOperationException>(timeline.Validate);
    }

    [Theory]
    [InlineData(float.NaN, 0, 0, null)]
    [InlineData(-1, 0, 0, null)]
    [InlineData(0, -1, 0, null)]
    [InlineData(0, 0, -1, null)]
    [InlineData(0, 0, 1, "")]
    public void ResourceCost_RejectsInvalidValues(float stamina, int mana, int ammo, string? ammoItemId)
    {
        var cost = new AttackResourceCost(stamina, mana, ammo, ammoItemId);

        Assert.ThrowsAny<Exception>(cost.Validate);
    }

    [Fact]
    public void Sequence_RejectsDuplicateUnknownAndCyclicStepIds()
    {
        var duplicate = CreateSequence(
            CreateStep("slash", "slash", comboWindow: new AttackPhaseWindow(1, 3)),
            CreateStep("slash"));
        var unknown = CreateSequence(
            CreateStep("slash", "missing", comboWindow: new AttackPhaseWindow(1, 3)));
        var cyclic = CreateSequence(
            CreateStep("one", "two", comboWindow: new AttackPhaseWindow(1, 3)),
            CreateStep("two", "one", comboWindow: new AttackPhaseWindow(1, 3)));

        Assert.Throws<InvalidOperationException>(duplicate.Validate);
        Assert.Throws<InvalidOperationException>(unknown.Validate);
        Assert.Throws<InvalidOperationException>(cyclic.Validate);
    }

    [Fact]
    public void Sequence_RejectsUnreachableStepsAndInvalidCapacities()
    {
        var unreachable = CreateSequence(CreateStep("one"), CreateStep("two"));
        var invalidCommands = CreateSequence(CreateStep("one")) with { CommandCapacity = 0 };
        var invalidEvents = CreateSequence(CreateStep("one")) with { EventCapacity = 2048 };
        var invalidHits = CreateSequence(CreateStep("one") with { MaxTargetsPerSwing = 0 });

        Assert.Throws<InvalidOperationException>(unreachable.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(invalidCommands.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(invalidEvents.Validate);
        Assert.Throws<ArgumentOutOfRangeException>(invalidHits.Validate);
    }

    [Fact]
    public void Sequence_RejectsOverlappingCancelWindows()
    {
        var step = CreateStep("slash") with
        {
            CancelRules = new[]
            {
                new AttackCancelRuleDefinition
                {
                    Phase = AttackRuntimePhase.Active,
                    Window = new AttackPhaseWindow(0, 2)
                },
                new AttackCancelRuleDefinition
                {
                    Phase = AttackRuntimePhase.Active,
                    Window = new AttackPhaseWindow(1, 3)
                }
            }
        };

        Assert.Throws<InvalidOperationException>(() => CreateSequence(step).Validate());
    }

    [Fact]
    public void Sequence_RejectsInvalidSweptShapeContent()
    {
        var invalidWindow = CreateStep("slash") with
        {
            MeleeShapes = new[]
            {
                new SweptMeleeShapeDefinition
                {
                    Id = "blade",
                    Sweep = new MeleeSweepDefinition(),
                    ActiveStartTickInclusive = 0,
                    ActiveEndTickExclusive = 4,
                    OriginOffset = new Vector2(1, 2)
                }
            }
        };

        Assert.Throws<InvalidOperationException>(() => CreateSequence(invalidWindow).Validate());
    }

    [Fact]
    public void Compiler_ConvertsLegacySecondsToStable60HzTicksAndMetadata()
    {
        var first = new AttackDefinition
        {
            Id = "slash-1",
            WindupSeconds = 0.1f,
            ActiveSeconds = 0.2f,
            RecoverySeconds = 0.3f,
            CooldownSeconds = 0.1f,
            ComboWindowStartSeconds = 0.2f,
            ComboWindowEndSeconds = 0.5f,
            ResourceCost = new AttackResourceCost(Stamina: 8),
            MaxTargetsPerSwing = 5,
            MeleeSweep = new MeleeSweepDefinition()
        };
        var second = new AttackDefinition
        {
            Id = "slash-2",
            ActiveSeconds = 0.05f
        };

        var compiled = AttackSequenceCompiler.Compile("sword", new[] { first, second });

        Assert.Equal(new AttackPhaseWindow(0, 6), compiled.Steps[0].Timeline.Startup);
        Assert.Equal(new AttackPhaseWindow(6, 18), compiled.Steps[0].Timeline.Active);
        Assert.Equal(new AttackPhaseWindow(18, 36), compiled.Steps[0].Timeline.Recovery);
        Assert.Equal(new AttackPhaseWindow(12, 30), compiled.Steps[0].Timeline.ComboWindow);
        Assert.Equal(6, compiled.Steps[0].Timeline.CooldownTicks);
        Assert.Equal(8, compiled.Steps[0].Cost.Stamina);
        Assert.Equal(5, compiled.Steps[0].MaxTargetsPerSwing);
        Assert.Single(compiled.Steps[0].MeleeShapes);
        Assert.Equal(3, compiled.Steps[1].Timeline.Active.DurationTicks);
    }

    private static AttackSequenceDefinition CreateSequence(params AttackComboStepDefinition[] steps)
    {
        return new AttackSequenceDefinition
        {
            Id = "test-sequence",
            Steps = steps
        };
    }

    private static AttackComboStepDefinition CreateStep(
        string id,
        string? nextStepId = null,
        AttackPhaseWindow? comboWindow = null)
    {
        return new AttackComboStepDefinition
        {
            Id = id,
            NextStepId = nextStepId,
            Timeline = AttackTimelineDefinition.Create(1, 3, 2, comboWindow: comboWindow)
        };
    }
}
