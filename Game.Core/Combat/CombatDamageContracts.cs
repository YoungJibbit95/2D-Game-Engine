using Game.Core.Effects;
using Game.Core.Entities;
using System.Numerics;

namespace Game.Core.Combat;

public sealed record DamageMitigationProfile
{
    public int FlatReduction { get; init; }

    public float GlobalReduction { get; init; }

    public IReadOnlyDictionary<DamageType, float> TypeReductions { get; init; } =
        new Dictionary<DamageType, float>();

    public float KnockbackResistance { get; init; }

    public void Validate()
    {
        if (FlatReduction < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FlatReduction));
        }

        ValidateFraction(GlobalReduction, nameof(GlobalReduction));
        ValidateFraction(KnockbackResistance, nameof(KnockbackResistance));
        foreach (var reduction in TypeReductions)
        {
            ValidateFraction(reduction.Value, nameof(TypeReductions));
        }
    }

    public int Apply(int damage, DamageType damageType)
    {
        Validate();
        var afterFlat = Math.Max(0, damage - FlatReduction);
        var typeReduction = TypeReductions.TryGetValue(damageType, out var configured) ? configured : 0;
        var retainedFraction = (1 - GlobalReduction) * (1 - typeReduction);
        return Math.Max(0, (int)MathF.Round(afterFlat * retainedFraction, MidpointRounding.AwayFromZero));
    }

    public float ApplyKnockback(float force)
    {
        Validate();
        return Math.Max(0, force) * (1 - KnockbackResistance);
    }

    private static void ValidateFraction(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public sealed record CombatResolutionPolicy
{
    public bool FriendlyFireEnabled { get; init; }

    public bool SelfDamageEnabled { get; init; }

    public bool ApplyStatusEffectsOnBlockedHits { get; init; }

    public int MinimumAppliedDamage { get; init; }

    public float KnockbackScale { get; init; } = 1;

    public float MaximumKnockback { get; init; } = float.MaxValue;

    public void Validate()
    {
        if (MinimumAppliedDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumAppliedDamage));
        }

        if (!float.IsFinite(KnockbackScale) || KnockbackScale < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(KnockbackScale));
        }

        if (float.IsNaN(MaximumKnockback) || MaximumKnockback < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumKnockback));
        }
    }
}

public sealed record CombatDamageRequest
{
    public ulong AttackInstanceId { get; init; }

    public int? SourceEntityId { get; init; }

    public required int TargetEntityId { get; init; }

    public EntityFaction SourceFaction { get; init; } = EntityFaction.Neutral;

    public EntityFaction TargetFaction { get; init; } = EntityFaction.Neutral;

    public required int BaseDamage { get; init; }

    public DamageType DamageType { get; init; } = DamageType.Generic;

    public Vector2 ImpactDirection { get; init; }

    public float KnockbackForce { get; init; }

    public float CriticalChance { get; init; }

    public float CriticalMultiplier { get; init; } = 2;

    public float GuardDamageMultiplier { get; init; } = 1;

    public IReadOnlyList<StatusEffectApplication> StatusEffects { get; init; } =
        Array.Empty<StatusEffectApplication>();

    public void Validate()
    {
        if (TargetEntityId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TargetEntityId));
        }

        if (SourceEntityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SourceEntityId));
        }

        if (BaseDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseDamage));
        }

        if (!float.IsFinite(ImpactDirection.X) || !float.IsFinite(ImpactDirection.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(ImpactDirection));
        }

        ValidateNonNegative(KnockbackForce, nameof(KnockbackForce));
        ValidateFraction(CriticalChance, nameof(CriticalChance));
        if (!float.IsFinite(CriticalMultiplier) || CriticalMultiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CriticalMultiplier));
        }

        ValidateNonNegative(GuardDamageMultiplier, nameof(GuardDamageMultiplier));
        ArgumentNullException.ThrowIfNull(StatusEffects);
        foreach (var statusEffect in StatusEffects)
        {
            if (string.IsNullOrWhiteSpace(statusEffect.EffectId) ||
                !float.IsFinite(statusEffect.Chance) ||
                statusEffect.Chance < 0 ||
                statusEffect.DurationSeconds is <= 0)
            {
                throw new ArgumentException("Status effect applications must contain valid ids, chances and durations.", nameof(StatusEffects));
            }
        }
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateFraction(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public enum CombatHitOutcome
{
    Applied,
    Blocked,
    Parried,
    GuardBroken,
    RejectedFriendlyFire,
    RejectedSelfHit,
    RejectedDuplicate,
    NoDamage
}

public readonly record struct CombatHitResult(
    CombatHitOutcome Outcome,
    ulong AttackInstanceId,
    int? SourceEntityId,
    int TargetEntityId,
    int DamageBeforeMitigation,
    int DamageApplied,
    int DamagePrevented,
    float GuardStaminaSpent,
    bool Critical,
    Vector2 Knockback,
    IReadOnlyList<StatusEffectApplication> StatusEffects,
    IReadOnlyList<ICombatEvent> Events)
{
    public bool Accepted => Outcome is
        CombatHitOutcome.Applied or
        CombatHitOutcome.Blocked or
        CombatHitOutcome.Parried or
        CombatHitOutcome.GuardBroken or
        CombatHitOutcome.NoDamage;

    public bool Defended => Outcome is CombatHitOutcome.Blocked or CombatHitOutcome.Parried;
}

public sealed class CombatHitTracker
{
    public const int DefaultConcurrentAttackCapacity = 64;
    public const int DefaultTargetsPerAttackCapacity = 64;

    private readonly ulong[] _attackIds;
    private readonly int[] _targetCounts;
    private readonly int[] _targets;

    public CombatHitTracker(
        int concurrentAttackCapacity = DefaultConcurrentAttackCapacity,
        int targetsPerAttackCapacity = DefaultTargetsPerAttackCapacity)
    {
        if (concurrentAttackCapacity is <= 0 or > AttackSequenceDefinition.MaximumCommandCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrentAttackCapacity));
        }

        if (targetsPerAttackCapacity is <= 0 or > AttackSequenceDefinition.MaximumHitCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(targetsPerAttackCapacity));
        }

        ConcurrentAttackCapacity = concurrentAttackCapacity;
        TargetsPerAttackCapacity = targetsPerAttackCapacity;
        _attackIds = new ulong[concurrentAttackCapacity];
        _targetCounts = new int[concurrentAttackCapacity];
        _targets = new int[checked(concurrentAttackCapacity * targetsPerAttackCapacity)];
    }

    public int ConcurrentAttackCapacity { get; }

    public int TargetsPerAttackCapacity { get; }

    public bool TryRegister(ulong attackInstanceId, int targetEntityId)
    {
        if (targetEntityId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetEntityId));
        }

        if (attackInstanceId == 0)
        {
            return true;
        }

        var attackSlot = FindAttackSlot(attackInstanceId);
        if (attackSlot < 0)
        {
            attackSlot = FindFreeSlot();
            if (attackSlot < 0)
            {
                return false;
            }

            _attackIds[attackSlot] = attackInstanceId;
        }

        var targetOffset = attackSlot * TargetsPerAttackCapacity;
        var targetCount = _targetCounts[attackSlot];
        for (var index = 0; index < targetCount; index++)
        {
            if (_targets[targetOffset + index] == targetEntityId)
            {
                return false;
            }
        }

        if (targetCount == TargetsPerAttackCapacity)
        {
            return false;
        }

        _targets[targetOffset + targetCount] = targetEntityId;
        _targetCounts[attackSlot] = targetCount + 1;
        return true;
    }

    public bool CompleteAttack(ulong attackInstanceId)
    {
        if (attackInstanceId == 0)
        {
            return false;
        }

        var slot = FindAttackSlot(attackInstanceId);
        if (slot < 0)
        {
            return false;
        }

        ClearSlot(slot);
        return true;
    }

    public void Clear()
    {
        Array.Clear(_attackIds);
        Array.Clear(_targetCounts);
        Array.Clear(_targets);
    }

    private int FindAttackSlot(ulong attackInstanceId)
    {
        for (var index = 0; index < _attackIds.Length; index++)
        {
            if (_attackIds[index] == attackInstanceId)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindFreeSlot()
    {
        for (var index = 0; index < _attackIds.Length; index++)
        {
            if (_attackIds[index] == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private void ClearSlot(int slot)
    {
        Array.Clear(_targets, slot * TargetsPerAttackCapacity, _targetCounts[slot]);
        _attackIds[slot] = 0;
        _targetCounts[slot] = 0;
    }
}
