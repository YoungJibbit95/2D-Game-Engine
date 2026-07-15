using Game.Core.Animation;
using Xunit;

namespace Game.Tests.AnimationTests;

public sealed class LayeredAnimationStateMachineTests
{
    [Fact]
    public void Transition_CrossFadesOutgoingAndCurrentClipsOverFixedTicks()
    {
        var idle = new AnimationStateDefinition("idle", AnimationTestFactory.CreateClip("idle"));
        var run = new AnimationStateDefinition("run", AnimationTestFactory.CreateClip("run"));
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "base",
            "idle",
            new[] { idle, run },
            new[] { new AnimationTransitionDefinition("idle", "run", blendTicks: 4) }));

        Assert.Equal(AnimationStateRequestResult.Applied, machine.RequestState("base", "run"));
        var start = Assert.Single(machine.Sample().Layers);
        Assert.NotNull(start.Outgoing);
        Assert.Equal(0f, start.BlendWeight);

        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right, 1000));
        var quarter = Assert.Single(machine.Sample().Layers);
        Assert.Equal(0.25f, quarter.BlendWeight);
        Assert.Equal("run", quarter.StateId);
        Assert.Equal("idle", quarter.Outgoing!.ClipId);

        for (var index = 0; index < 3; index++)
        {
            machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right, 1000));
        }

        var complete = Assert.Single(machine.Sample().Layers);
        Assert.Null(complete.Outgoing);
        Assert.Equal(1f, complete.BlendWeight);
    }

    [Fact]
    public void UntilCompleteActionLock_BlocksStateChangesAtEveryFrameEdge()
    {
        var idle = new AnimationStateDefinition("idle", AnimationTestFactory.CreateClip("idle"));
        var mine = new AnimationStateDefinition(
            "mine",
            AnimationTestFactory.CreateClip("mine", AnimationLoopMode.Once),
            actionLockMode: AnimationActionLockMode.UntilClipComplete);
        var hurt = new AnimationStateDefinition("hurt", AnimationTestFactory.CreateClip("hurt"));
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "action",
            "idle",
            new[] { idle, mine, hurt }));

        Assert.Equal(AnimationStateRequestResult.Applied, machine.RequestState("action", "mine"));
        Assert.Equal(AnimationStateRequestResult.BlockedByActionLock, machine.RequestState("action", "hurt"));
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));
        Assert.Equal(AnimationStateRequestResult.BlockedByActionLock, machine.RequestState("action", "hurt"));

        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));

        Assert.Equal(AnimationStateRequestResult.Applied, machine.RequestState("action", "hurt"));
    }

    [Fact]
    public void FixedTickActionLock_ReleasesIndependentlyOfClipPlaybackRate()
    {
        var hurt = new AnimationStateDefinition(
            "hurt",
            AnimationTestFactory.CreateClip("hurt", AnimationLoopMode.Once, new[] { 10 }),
            playbackRate: new AnimationPlaybackRate(1, 4),
            actionLockMode: AnimationActionLockMode.FixedTicks,
            actionLockTicks: 2);
        var idle = new AnimationStateDefinition("idle", AnimationTestFactory.CreateClip("idle"));
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "action",
            "hurt",
            new[] { hurt, idle }));

        Assert.Equal(AnimationStateRequestResult.BlockedByActionLock, machine.RequestState("action", "idle"));
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));
        Assert.Equal(AnimationStateRequestResult.BlockedByActionLock, machine.RequestState("action", "idle"));
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));

        Assert.Equal(AnimationStateRequestResult.Applied, machine.RequestState("action", "idle"));
    }

    [Fact]
    public void LocomotionScaling_QuantizesSpeedIntoDeterministicRationalRates()
    {
        var run = new AnimationStateDefinition(
            "run",
            AnimationTestFactory.CreateClip("run", frameDurations: new[] { 1, 1, 1, 1 }),
            scaleWithLocomotion: true,
            locomotionReferenceSpeedMilliUnitsPerSecond: 1000,
            minimumLocomotionRatePercentage: 0,
            maximumLocomotionRatePercentage: 300);
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "base",
            "run",
            new[] { run }));

        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right, 2000));
        Assert.Equal(2, Assert.Single(machine.Sample().Layers).Current.TimelineTick);

        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right, 500));
        Assert.Equal(2, Assert.Single(machine.Sample().Layers).Current.TimelineTick);
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right, 500));
        Assert.Equal(3, Assert.Single(machine.Sample().Layers).Current.TimelineTick);
    }

    [Fact]
    public void OnceState_AutomaticallyReturnsToConfiguredCompletionState()
    {
        var idle = new AnimationStateDefinition("idle", AnimationTestFactory.CreateClip("idle"));
        var swing = new AnimationStateDefinition(
            "swing",
            AnimationTestFactory.CreateClip("swing", AnimationLoopMode.Once, new[] { 1, 1 }),
            actionLockMode: AnimationActionLockMode.UntilClipComplete,
            completionStateId: "idle");
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "action",
            "idle",
            new[] { idle, swing }));
        machine.RequestState("action", "swing");

        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));

        Assert.True(machine.TryGetCurrentState("action", out var current));
        Assert.Equal("idle", current);
    }

    [Fact]
    public void Events_AreForwardedFromCurrentStateButNotOutgoingBlendSource()
    {
        var idle = new AnimationStateDefinition("idle", AnimationTestFactory.CreateClip(
            "idle",
            events: new[] { new AnimationEventMarker("idle-start", 0), new AnimationEventMarker("idle-step", 1) }));
        var swing = new AnimationStateDefinition("swing", AnimationTestFactory.CreateClip(
            "swing",
            events: new[] { new AnimationEventMarker("windup", 0), new AnimationEventMarker("impact", 1) }));
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "action",
            "idle",
            new[] { idle, swing },
            new[] { new AnimationTransitionDefinition("idle", "swing", 3) }));
        machine.Events.ConsumeAll();

        machine.RequestState("action", "swing");
        Assert.Equal("windup", Assert.Single(machine.Events.ConsumeAll()).ClipEvent.EventId);
        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Right));

        var forwarded = Assert.Single(machine.Events.ConsumeAll());
        Assert.Equal("impact", forwarded.ClipEvent.EventId);
        Assert.Equal("swing", forwarded.StateId);
    }

    [Fact]
    public void Sample_OrdersLayersByPriorityAndCarriesDirectionTintAndVisibility()
    {
        var clip = AnimationTestFactory.CreateClip("idle");
        var high = new AnimationStateLayerDefinition(
            "face",
            20,
            "idle",
            new[] { new AnimationStateDefinition("idle", clip) });
        var low = new AnimationStateLayerDefinition(
            "base",
            0,
            "idle",
            new[] { new AnimationStateDefinition("idle", clip) });
        var machine = new LayeredAnimationStateMachine(new LayeredAnimationStateMachineDefinition(new[] { high, low }));
        var tint = new AnimationColor(100, 120, 140, 200);
        Assert.True(machine.SetLayerPresentation("face", tint, visible: false, opacity: 0.4f));

        machine.AdvanceFixedTick(new AnimationUpdateContext(CharacterFacingDirection.Left));
        var pose = machine.Sample();

        Assert.Equal(CharacterFacingDirection.Left, pose.FacingDirection);
        Assert.Equal(new[] { "base", "face" }, pose.Layers.Select(layer => layer.LayerId));
        Assert.Equal(tint, pose.Layers[1].Tint);
        Assert.False(pose.Layers[1].Visible);
        Assert.Equal(0.4f, pose.Layers[1].Opacity);
    }

    [Fact]
    public void RequestState_ReportsUnknownLayersStatesAndAlreadyActiveState()
    {
        var idle = new AnimationStateDefinition("idle", AnimationTestFactory.CreateClip("idle"));
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "base",
            "idle",
            new[] { idle }));

        Assert.Equal(AnimationStateRequestResult.LayerNotFound, machine.RequestState("missing", "idle"));
        Assert.Equal(AnimationStateRequestResult.StateNotFound, machine.RequestState("base", "missing"));
        Assert.Equal(AnimationStateRequestResult.AlreadyActive, machine.RequestState("base", "idle"));
    }

    [Fact]
    public void StateDefinition_RejectsUntilCompleteLockForLoopingClip()
    {
        var clip = AnimationTestFactory.CreateClip("looping-action", AnimationLoopMode.Loop);

        var exception = Assert.Throws<ArgumentException>(() => new AnimationStateDefinition(
            "broken-action",
            clip,
            actionLockMode: AnimationActionLockMode.UntilClipComplete));

        Assert.Contains("once clip", exception.Message, StringComparison.Ordinal);
    }
}
