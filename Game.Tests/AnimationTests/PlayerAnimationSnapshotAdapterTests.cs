using Game.Client.Rendering.Character;
using Game.Core.Animation;
using Game.Core.Combat;
using Game.Core.Equipment;
using Game.Core.Runtime;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;
using CoreVector2 = System.Numerics.Vector2;

namespace Game.Tests.AnimationTests;

public sealed class PlayerAnimationSnapshotAdapterTests
{
    [Fact]
    public void Resolve_UsesLiveGuardFacingForBlockStateWithoutOwningGuardSimulation()
    {
        var guard = new GuardRuntimeState(new GuardDefinition());
        Assert.True(guard.TryBeginGuard(-CoreVector2.UnitX));
        var snapshot = CreateSnapshot(
            velocity: new CoreVector2(80, 0),
            isOnGround: true,
            isGuarding: false);

        var decision = new PlayerAnimationSnapshotAdapter().Resolve(snapshot, guard);

        Assert.Equal(PlayerAnimationStateIds.Block, decision.StateId);
        Assert.Equal(CharacterFacingDirection.Left, decision.UpdateContext.FacingDirection);
        Assert.Equal(80_000, decision.UpdateContext.LocomotionSpeedMilliUnitsPerSecond);
        Assert.True(decision.IsBlocking);
    }

    [Fact]
    public void Resolve_MapsDeathAirborneLocomotionAndIdleSnapshotStates()
    {
        var adapter = new PlayerAnimationSnapshotAdapter();

        Assert.Equal(PlayerAnimationStateIds.Death, adapter.Resolve(CreateSnapshot(isDead: true)).StateId);
        Assert.Equal(
            PlayerAnimationStateIds.Jump,
            adapter.Resolve(CreateSnapshot(velocity: new CoreVector2(0, -4), isOnGround: false)).StateId);
        Assert.Equal(
            PlayerAnimationStateIds.Fall,
            adapter.Resolve(CreateSnapshot(velocity: new CoreVector2(0, 4), isOnGround: false)).StateId);
        Assert.Equal(
            PlayerAnimationStateIds.Run,
            adapter.Resolve(CreateSnapshot(velocity: new CoreVector2(-3, 0), isOnGround: true)).StateId);
        Assert.Equal(PlayerAnimationStateIds.Idle, adapter.Resolve(CreateSnapshot()).StateId);
    }

    [Fact]
    public void Synchronize_BlockRespectsActionLockButDeathCanInterruptIt()
    {
        var states = new[]
        {
            new AnimationStateDefinition("idle", AnimationTestFactory.CreateClip("idle")),
            new AnimationStateDefinition(
                "mine",
                AnimationTestFactory.CreateClip("mine", AnimationLoopMode.Once),
                actionLockMode: AnimationActionLockMode.UntilClipComplete),
            new AnimationStateDefinition("block", AnimationTestFactory.CreateClip("block")),
            new AnimationStateDefinition("death", AnimationTestFactory.CreateClip("death"))
        };
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "base",
            "idle",
            states));
        var adapter = new PlayerAnimationSnapshotAdapter();
        machine.RequestState("base", "mine");

        var blocked = adapter.SynchronizeAndAdvance(
            machine,
            "base",
            CreateSnapshot(isGuarding: true));

        Assert.Equal(AnimationStateRequestResult.BlockedByActionLock, blocked.RequestResult);
        Assert.True(machine.TryGetCurrentState("base", out var current));
        Assert.Equal("mine", current);

        var interrupted = adapter.SynchronizeAndAdvance(
            machine,
            "base",
            CreateSnapshot(isDead: true, isGuarding: true));

        Assert.Equal(AnimationStateRequestResult.Applied, interrupted.RequestResult);
        Assert.True(machine.TryGetCurrentState("base", out current));
        Assert.Equal("death", current);
        Assert.False(interrupted.Decision.IsBlocking);
    }

    [Fact]
    public void GuardBindings_ShowShieldAndGuardToolOnTheirOwnAttachmentSockets()
    {
        var bodyFrame = new AnimationFrame(
            0,
            2,
            sockets: new[]
            {
                new AttachmentSocketPose("tool-hand", new CoreVector2(-2, 0)),
                new AttachmentSocketPose("shield-hand", new CoreVector2(2, 0))
            });
        var clip = new AnimationClip(
            "block",
            AnimationLoopMode.Loop,
            new[]
            {
                new AnimationTrack("body", "body", "body", new[] { bodyFrame }),
                new AnimationTrack("tool", "tool", "tool", new[] { new AnimationFrame(0, 2) }),
                new AnimationTrack("shield", "shield", "shield", new[] { new AnimationFrame(0, 2) })
            });
        var machine = AnimationTestFactory.CreateMachine(AnimationTestFactory.CreateLayer(
            "base",
            "block",
            new[] { new AnimationStateDefinition("block", clip) }));
        var rig = new CharacterRigProfile("guard", new[]
        {
            new CharacterRigLayerDefinition("body", CharacterAppearanceSlot.Body, "body", 0),
            new CharacterRigLayerDefinition(
                "tool",
                CharacterAppearanceSlot.Tool,
                "tool",
                10,
                parentLayerId: "body",
                parentSocketId: "tool-hand"),
            new CharacterRigLayerDefinition(
                "shield",
                CharacterAppearanceSlot.Shield,
                "shield",
                20,
                parentLayerId: "body",
                parentSocketId: "shield-hand")
        });
        var appearance = new CharacterAppearanceProfile(
            new CharacterPartAppearance("player/body"),
            tool: new CharacterPartAppearance("tools/sword"));
        var bindings = new PlayerGuardAttachmentBindings(
            "shield",
            "equipment/wooden-shield",
            equipmentOverrides: new[]
            {
                new CharacterLayerOverride("tool", "tools/sword", visible: true)
            },
            shieldFrameIndex: 2,
            guardedToolSpriteId: "tools/sword-guard");
        var poseBuilder = new CharacterSpriteLayerPoseBuilder();

        var blocking = poseBuilder.Build(
            rig,
            appearance,
            machine.Sample(),
            new Vector2(10, 10),
            bindings.Resolve(CreateSnapshot(isGuarding: true)));

        Assert.Collection(
            blocking.Layers,
            body => Assert.Equal(new Vector2(10, 10), body.Position),
            tool =>
            {
                Assert.Equal("tools/sword-guard", tool.SpriteId);
                Assert.Equal(new Vector2(8, 10), tool.Position);
            },
            shield =>
            {
                Assert.Equal("equipment/wooden-shield", shield.SpriteId);
                Assert.Equal(2, shield.SpriteFrameIndex);
                Assert.Equal(new Vector2(12, 10), shield.Position);
                Assert.True(shield.Visible);
            });

        var relaxed = poseBuilder.Build(
            rig,
            appearance,
            machine.Sample(),
            Vector2.Zero,
            bindings.Resolve(CreateSnapshot()));
        var commands = new CharacterDrawCommandBuilder().Build(relaxed);

        Assert.DoesNotContain(commands, command => command.RigLayerId == "shield");
        Assert.Equal("tools/sword", Assert.Single(commands, command => command.RigLayerId == "tool").SpriteId);
    }

    private static PlayerFrameSnapshot CreateSnapshot(
        CoreVector2? velocity = null,
        bool isOnGround = true,
        bool isDead = false,
        bool isGuarding = false,
        bool isGuardBroken = false)
    {
        return new PlayerFrameSnapshot(
            CoreVector2.Zero,
            velocity ?? CoreVector2.Zero,
            new RectI(0, 0, 12, 28),
            isOnGround,
            isDead,
            100,
            100,
            20,
            20,
            PlayerStatBlock.Base,
            isGuarding,
            isGuardBroken,
            100,
            100,
            0,
            ImmutableSnapshotList<InventorySlotFrameSnapshot>.Empty);
    }
}
