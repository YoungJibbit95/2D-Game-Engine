using Game.Core.Events;
using System.Numerics;

namespace Game.Core.Combat;

public interface ICombatEvent : IGameEvent;

public readonly record struct AttackStartedCombatEvent(
    ulong AttackInstanceId,
    string AttackId,
    int ComboIndex,
    AttackPhase InitialPhase) : ICombatEvent;

public readonly record struct AttackPhaseChangedCombatEvent(
    ulong AttackInstanceId,
    string AttackId,
    int ComboIndex,
    AttackPhase PreviousPhase,
    AttackPhase CurrentPhase) : ICombatEvent;

public readonly record struct AttackComboQueuedCombatEvent(
    ulong AttackInstanceId,
    string AttackId,
    string QueuedAttackId,
    int ComboIndex) : ICombatEvent;

public readonly record struct AttackCompletedCombatEvent(
    ulong AttackInstanceId,
    string AttackId,
    int ComboIndex) : ICombatEvent;

public readonly record struct CombatHitResolvedEvent(
    ulong AttackInstanceId,
    int? SourceEntityId,
    int TargetEntityId,
    CombatHitOutcome Outcome,
    int DamageApplied,
    bool Critical,
    Vector2 Knockback) : ICombatEvent;

public readonly record struct CombatParriedEvent(
    ulong AttackInstanceId,
    int? SourceEntityId,
    int TargetEntityId) : ICombatEvent;

public readonly record struct CombatBlockedEvent(
    ulong AttackInstanceId,
    int? SourceEntityId,
    int TargetEntityId,
    float GuardStaminaSpent,
    int DamageApplied) : ICombatEvent;

public readonly record struct GuardBrokenCombatEvent(
    ulong AttackInstanceId,
    int? SourceEntityId,
    int TargetEntityId,
    float BreakDurationSeconds) : ICombatEvent;
