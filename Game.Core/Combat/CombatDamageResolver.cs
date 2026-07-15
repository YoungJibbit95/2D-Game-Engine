using Game.Core.Effects;
using Game.Core.Randomness;
using System.Numerics;

namespace Game.Core.Combat;

public sealed class CombatDamageResolver
{
    private static readonly DamageMitigationProfile NoMitigation = new();
    private static readonly CombatResolutionPolicy DefaultPolicy = new();

    private readonly DeterministicRandomStream _criticalRandom;
    private readonly DeterministicRandomStream _statusRandom;

    public CombatDamageResolver(
        DeterministicRandomStream criticalRandom,
        DeterministicRandomStream statusRandom,
        CombatHitTracker? hitTracker = null)
    {
        _criticalRandom = criticalRandom ?? throw new ArgumentNullException(nameof(criticalRandom));
        _statusRandom = statusRandom ?? throw new ArgumentNullException(nameof(statusRandom));
        HitTracker = hitTracker ?? new CombatHitTracker();
    }

    public CombatHitTracker HitTracker { get; }

    public CombatHitResult Resolve(
        CombatDamageRequest request,
        DamageMitigationProfile? mitigation = null,
        GuardRuntimeState? guard = null,
        CombatResolutionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        mitigation ??= NoMitigation;
        policy ??= DefaultPolicy;
        mitigation.Validate();
        policy.Validate();

        if (request.SourceEntityId == request.TargetEntityId && !policy.SelfDamageEnabled)
        {
            return Rejected(request, CombatHitOutcome.RejectedSelfHit);
        }

        if (request.SourceEntityId is not null &&
            request.SourceFaction == request.TargetFaction &&
            !policy.FriendlyFireEnabled)
        {
            return Rejected(request, CombatHitOutcome.RejectedFriendlyFire);
        }

        if (!HitTracker.TryRegister(request.AttackInstanceId, request.TargetEntityId))
        {
            return Rejected(request, CombatHitOutcome.RejectedDuplicate);
        }

        if (guard is not null && guard.CoversImpact(request.ImpactDirection) && guard.IsParryWindowOpen)
        {
            var parryEvents = new ICombatEvent[]
            {
                new CombatParriedEvent(request.AttackInstanceId, request.SourceEntityId, request.TargetEntityId),
                new CombatHitResolvedEvent(
                    request.AttackInstanceId,
                    request.SourceEntityId,
                    request.TargetEntityId,
                    CombatHitOutcome.Parried,
                    0,
                    false,
                    Vector2.Zero)
            };
            return new CombatHitResult(
                CombatHitOutcome.Parried,
                request.AttackInstanceId,
                request.SourceEntityId,
                request.TargetEntityId,
                request.BaseDamage,
                0,
                request.BaseDamage,
                0,
                false,
                Vector2.Zero,
                Array.Empty<StatusEffectApplication>(),
                parryEvents);
        }

        var critical = RollCritical(request.CriticalChance);
        var damageBeforeMitigation = critical
            ? Math.Max(0, (int)MathF.Round(
                request.BaseDamage * request.CriticalMultiplier,
                MidpointRounding.AwayFromZero))
            : request.BaseDamage;
        var damage = mitigation.Apply(damageBeforeMitigation, request.DamageType);
        if (damage > 0)
        {
            damage = Math.Min(
                damageBeforeMitigation,
                Math.Max(damage, policy.MinimumAppliedDamage));
        }

        var knockbackForce = Math.Min(
            mitigation.ApplyKnockback(request.KnockbackForce) * policy.KnockbackScale,
            policy.MaximumKnockback);
        var outcome = damage > 0 ? CombatHitOutcome.Applied : CombatHitOutcome.NoDamage;
        var guardStaminaSpent = 0f;
        var coveredByGuard = guard is not null && guard.CoversImpact(request.ImpactDirection);
        if (coveredByGuard)
        {
            var guardCost = Math.Max(
                guard!.Definition.MinimumGuardStaminaCost,
                damageBeforeMitigation * guard.Definition.GuardStaminaCostPerDamage * request.GuardDamageMultiplier);
            if (guardCost >= guard.Stamina)
            {
                guardStaminaSpent = guard.Stamina;
                guard.Break();
                outcome = CombatHitOutcome.GuardBroken;
            }
            else
            {
                guardStaminaSpent = guard.SpendStamina(guardCost);
                damage = Math.Max(0, (int)MathF.Round(
                    damage * (1 - guard.Definition.BlockDamageReduction),
                    MidpointRounding.AwayFromZero));
                knockbackForce *= guard.Definition.BlockKnockbackMultiplier;
                outcome = CombatHitOutcome.Blocked;
            }
        }

        var knockback = ResolveKnockback(request.ImpactDirection, knockbackForce);
        var applyStatuses = damage > 0 &&
            (outcome != CombatHitOutcome.Blocked || policy.ApplyStatusEffectsOnBlockedHits);
        var statuses = applyStatuses
            ? ResolveStatusEffects(request.StatusEffects)
            : Array.Empty<StatusEffectApplication>();
        var events = BuildEvents(request, outcome, damage, guardStaminaSpent, critical, knockback, guard);

        return new CombatHitResult(
            outcome,
            request.AttackInstanceId,
            request.SourceEntityId,
            request.TargetEntityId,
            damageBeforeMitigation,
            damage,
            Math.Max(0, damageBeforeMitigation - damage),
            guardStaminaSpent,
            critical,
            knockback,
            statuses,
            events);
    }

    private static CombatHitResult Rejected(CombatDamageRequest request, CombatHitOutcome outcome)
    {
        return new CombatHitResult(
            outcome,
            request.AttackInstanceId,
            request.SourceEntityId,
            request.TargetEntityId,
            request.BaseDamage,
            0,
            request.BaseDamage,
            0,
            false,
            Vector2.Zero,
            Array.Empty<StatusEffectApplication>(),
            Array.Empty<ICombatEvent>());
    }

    private bool RollCritical(float chance)
    {
        return chance >= 1 || chance > 0 && _criticalRandom.NextSingle() < chance;
    }

    private IReadOnlyList<StatusEffectApplication> ResolveStatusEffects(
        IReadOnlyList<StatusEffectApplication> applications)
    {
        if (applications.Count == 0)
        {
            return Array.Empty<StatusEffectApplication>();
        }

        var selected = new List<StatusEffectApplication>(applications.Count);
        foreach (var application in applications)
        {
            if (string.IsNullOrWhiteSpace(application.EffectId) ||
                application.Chance <= 0 ||
                application.Chance < 1 && _statusRandom.NextSingle() >= application.Chance)
            {
                continue;
            }

            selected.Add(application with { Chance = 1 });
        }

        return selected.Count == 0 ? Array.Empty<StatusEffectApplication>() : selected;
    }

    private static Vector2 ResolveKnockback(Vector2 impactDirection, float force)
    {
        return force <= 0 || impactDirection.LengthSquared() <= float.Epsilon
            ? Vector2.Zero
            : Vector2.Normalize(impactDirection) * force;
    }

    private static IReadOnlyList<ICombatEvent> BuildEvents(
        CombatDamageRequest request,
        CombatHitOutcome outcome,
        int damage,
        float guardStaminaSpent,
        bool critical,
        Vector2 knockback,
        GuardRuntimeState? guard)
    {
        var events = new List<ICombatEvent>(2);
        if (outcome == CombatHitOutcome.Blocked)
        {
            events.Add(new CombatBlockedEvent(
                request.AttackInstanceId,
                request.SourceEntityId,
                request.TargetEntityId,
                guardStaminaSpent,
                damage));
        }
        else if (outcome == CombatHitOutcome.GuardBroken)
        {
            events.Add(new GuardBrokenCombatEvent(
                request.AttackInstanceId,
                request.SourceEntityId,
                request.TargetEntityId,
                guard!.Definition.GuardBreakDurationSeconds));
        }

        events.Add(new CombatHitResolvedEvent(
            request.AttackInstanceId,
            request.SourceEntityId,
            request.TargetEntityId,
            outcome,
            damage,
            critical,
            knockback));
        return events;
    }
}
