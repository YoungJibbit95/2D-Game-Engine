using Game.Core.Combat;
using Game.Core.Events;
using Game.Core.Farming;
using Game.Core.Interaction;
using Game.Core.Projectiles;

namespace Game.Core.Actions;

public readonly record struct PlayerItemUseResult(
    PlayerItemUseKind Kind,
    MiningResult Mining,
    bool PlacedTile,
    MeleeAttackResult Melee,
    ProjectileEntity? Projectile = null,
    FarmActionResult? Farming = null)
{
    public PlayerItemUseStatus Status { get; init; }

    public PlayerItemUseKind AttemptedKind { get; init; }

    public GameplayActionFailureReason FailureReason { get; init; }

    public GameplayActionSuccessReason SuccessReason { get; init; }

    public float ActionProgress { get; init; }

    public float CooldownRemaining { get; init; }

    public float CooldownDuration { get; init; }

    public int HealthRestored { get; init; }

    public int ManaRestored { get; init; }

    public int StatusEffectsApplied { get; init; }

    public bool ConsumedItem { get; init; }

    public bool Success => Status == PlayerItemUseStatus.Succeeded ||
                           Status == PlayerItemUseStatus.NoAction && Kind != PlayerItemUseKind.None;

    public bool Blocked => Status == PlayerItemUseStatus.Blocked;

    public bool InProgress => Status == PlayerItemUseStatus.InProgress;

    public float CooldownProgress => CooldownDuration <= 0
        ? 1f
        : Math.Clamp(1f - CooldownRemaining / CooldownDuration, 0f, 1f);

    public static PlayerItemUseResult None { get; } = new(
        PlayerItemUseKind.None,
        MiningResult.None,
        false,
        MeleeAttackResult.None,
        null,
        null)
    {
        Status = PlayerItemUseStatus.NoAction,
        AttemptedKind = PlayerItemUseKind.None
    };

    public static PlayerItemUseResult BlockedResult(
        PlayerItemUseKind attemptedKind,
        GameplayActionFailureReason reason,
        float cooldownRemaining = 0,
        float cooldownDuration = 0,
        MiningResult mining = default,
        MeleeAttackResult melee = default)
    {
        return None with
        {
            Status = PlayerItemUseStatus.Blocked,
            AttemptedKind = attemptedKind,
            FailureReason = reason,
            CooldownRemaining = Math.Max(0, cooldownRemaining),
            CooldownDuration = Math.Max(0, cooldownDuration),
            ActionProgress = mining.Progress,
            Mining = mining,
            Melee = melee
        };
    }

    public static PlayerItemUseResult Progressing(PlayerItemUseKind attemptedKind, MiningResult mining)
    {
        return None with
        {
            Status = PlayerItemUseStatus.InProgress,
            AttemptedKind = attemptedKind,
            SuccessReason = mining.Started
                ? GameplayActionSuccessReason.ActionStarted
                : GameplayActionSuccessReason.ActionProgressed,
            ActionProgress = mining.Progress,
            Mining = mining
        };
    }
}
