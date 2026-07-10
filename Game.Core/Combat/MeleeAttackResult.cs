using Game.Core.Events;

namespace Game.Core.Combat;

public readonly record struct MeleeAttackResult(
    bool Attacked,
    int Hits,
    int EnemyDeaths,
    int DroppedItems,
    int StatusEffectsApplied = 0,
    GameplayActionFailureReason FailureReason = GameplayActionFailureReason.None,
    float CooldownRemaining = 0,
    float CooldownDuration = 0)
{
    public static MeleeAttackResult None { get; } = new(false, 0, 0, 0);

    public bool Blocked => !Attacked && FailureReason != GameplayActionFailureReason.None;

    public float CooldownProgress => CooldownDuration <= 0
        ? 1f
        : Math.Clamp(1f - CooldownRemaining / CooldownDuration, 0f, 1f);

    public static MeleeAttackResult BlockedResult(
        GameplayActionFailureReason reason,
        float cooldownRemaining = 0,
        float cooldownDuration = 0)
    {
        return new MeleeAttackResult(
            false,
            0,
            0,
            0,
            0,
            reason,
            Math.Max(0, cooldownRemaining),
            Math.Max(0, cooldownDuration));
    }
}
