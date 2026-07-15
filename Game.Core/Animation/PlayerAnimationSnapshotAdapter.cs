using Game.Core.Combat;
using Game.Core.Runtime;

namespace Game.Core.Animation;

public static class PlayerAnimationStateIds
{
    public const string Idle = "idle";
    public const string Run = "run";
    public const string Jump = "jump";
    public const string Fall = "fall";
    public const string Block = "block";
    public const string Death = "death";
}

public readonly record struct PlayerAnimationSnapshotDecision(
    string StateId,
    AnimationUpdateContext UpdateContext,
    bool IsBlocking);

public readonly record struct PlayerAnimationSnapshotSyncResult(
    PlayerAnimationSnapshotDecision Decision,
    AnimationStateRequestResult RequestResult);

public sealed class PlayerAnimationSnapshotAdapter
{
    private const float MovementThreshold = 0.01f;

    public PlayerAnimationSnapshotDecision Resolve(
        PlayerFrameSnapshot snapshot,
        GuardRuntimeState? guardState = null,
        CharacterFacingDirection previousFacingDirection = CharacterFacingDirection.Right)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var isBlocking = IsBlocking(snapshot, guardState);
        var facingDirection = ResolveFacingDirection(
            snapshot,
            guardState,
            isBlocking,
            previousFacingDirection);
        var stateId = ResolveStateId(snapshot, isBlocking);
        var locomotionSpeed = ToMilliUnitsPerSecond(MathF.Abs(snapshot.Velocity.X));

        return new PlayerAnimationSnapshotDecision(
            stateId,
            new AnimationUpdateContext(facingDirection, locomotionSpeed),
            isBlocking);
    }

    public PlayerAnimationSnapshotSyncResult SynchronizeAndAdvance(
        LayeredAnimationStateMachine machine,
        string layerId,
        PlayerFrameSnapshot snapshot,
        GuardRuntimeState? guardState = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);

        var decision = Resolve(snapshot, guardState, machine.FacingDirection);
        var request = new AnimationStateRequest(
            BypassActionLock: string.Equals(
                decision.StateId,
                PlayerAnimationStateIds.Death,
                StringComparison.Ordinal));
        var result = machine.RequestState(layerId, decision.StateId, request);
        machine.AdvanceFixedTick(decision.UpdateContext);
        return new PlayerAnimationSnapshotSyncResult(decision, result);
    }

    public static bool IsBlocking(PlayerFrameSnapshot snapshot, GuardRuntimeState? guardState = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return !snapshot.IsDead && (guardState is null
            ? snapshot.IsGuarding && !snapshot.IsGuardBroken
            : guardState.IsGuarding && !guardState.IsGuardBroken);
    }

    private static string ResolveStateId(PlayerFrameSnapshot snapshot, bool isBlocking)
    {
        if (snapshot.IsDead)
        {
            return PlayerAnimationStateIds.Death;
        }

        if (isBlocking)
        {
            return PlayerAnimationStateIds.Block;
        }

        if (!snapshot.IsOnGround)
        {
            return snapshot.Velocity.Y < 0f
                ? PlayerAnimationStateIds.Jump
                : PlayerAnimationStateIds.Fall;
        }

        return MathF.Abs(snapshot.Velocity.X) >= MovementThreshold
            ? PlayerAnimationStateIds.Run
            : PlayerAnimationStateIds.Idle;
    }

    private static CharacterFacingDirection ResolveFacingDirection(
        PlayerFrameSnapshot snapshot,
        GuardRuntimeState? guardState,
        bool isBlocking,
        CharacterFacingDirection fallback)
    {
        var horizontal = isBlocking && guardState is not null
            ? guardState.Facing.X
            : snapshot.Velocity.X;
        if (horizontal < -MovementThreshold)
        {
            return CharacterFacingDirection.Left;
        }

        if (horizontal > MovementThreshold)
        {
            return CharacterFacingDirection.Right;
        }

        return fallback;
    }

    private static int ToMilliUnitsPerSecond(float speed)
    {
        if (!float.IsFinite(speed))
        {
            return 0;
        }

        var scaled = speed * 1000f;
        return scaled >= int.MaxValue
            ? int.MaxValue
            : (int)MathF.Round(scaled, MidpointRounding.AwayFromZero);
    }
}

public sealed class PlayerGuardAttachmentBindings
{
    private readonly CharacterRenderOverrides _blockingOverrides;
    private readonly CharacterRenderOverrides _relaxedOverrides;

    public PlayerGuardAttachmentBindings(
        string shieldLayerId,
        string shieldSpriteId,
        IEnumerable<CharacterLayerOverride>? equipmentOverrides = null,
        int shieldFrameIndex = 0,
        string toolLayerId = "tool",
        string? guardedToolSpriteId = null,
        bool hideToolWhileBlocking = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shieldLayerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(shieldSpriteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolLayerId);
        if (shieldFrameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shieldFrameIndex));
        }

        ShieldLayerId = shieldLayerId;
        ToolLayerId = toolLayerId;
        var equipment = equipmentOverrides?.ToArray();

        var blocking = new List<CharacterLayerOverride>
        {
            new(shieldLayerId, shieldSpriteId, visible: true, frameIndex: shieldFrameIndex)
        };
        if (guardedToolSpriteId is not null)
        {
            blocking.Add(new CharacterLayerOverride(toolLayerId, guardedToolSpriteId, visible: true));
        }
        else if (hideToolWhileBlocking)
        {
            blocking.Add(new CharacterLayerOverride(toolLayerId, visible: false));
        }

        _blockingOverrides = new CharacterRenderOverrides(equipment, blocking);
        _relaxedOverrides = new CharacterRenderOverrides(
            equipment,
            new[] { new CharacterLayerOverride(shieldLayerId, visible: false) });
    }

    public string ShieldLayerId { get; }

    public string ToolLayerId { get; }

    public CharacterRenderOverrides Resolve(
        PlayerFrameSnapshot snapshot,
        GuardRuntimeState? guardState = null)
    {
        return PlayerAnimationSnapshotAdapter.IsBlocking(snapshot, guardState)
            ? _blockingOverrides
            : _relaxedOverrides;
    }
}
