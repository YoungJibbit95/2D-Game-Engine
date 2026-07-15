using Game.Core.Combat;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Randomness;
using System.Numerics;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class AdvancedCombatResolutionTests
{
    [Fact]
    public void Resolve_FrontAttackInsideParryWindowIsParried()
    {
        var guard = CreateGuard();
        Assert.True(guard.TryBeginGuard(-Vector2.UnitX));

        var result = CreateResolver().Resolve(CreateRequest(), guard: guard);

        Assert.Equal(CombatHitOutcome.Parried, result.Outcome);
        Assert.Equal(0, result.DamageApplied);
        Assert.Equal(100, guard.Stamina);
        Assert.Contains(result.Events, gameEvent => gameEvent is CombatParriedEvent);
    }

    [Fact]
    public void Resolve_AfterParryWindowBlocksAndConsumesGuardStamina()
    {
        var guard = CreateGuard();
        Assert.True(guard.TryBeginGuard(-Vector2.UnitX));
        guard.Update(0.2f);

        var result = CreateResolver().Resolve(CreateRequest(), guard: guard);

        Assert.Equal(CombatHitOutcome.Blocked, result.Outcome);
        Assert.Equal(5, result.DamageApplied);
        Assert.Equal(20, result.GuardStaminaSpent);
        Assert.Equal(80, guard.Stamina);
        Assert.Equal(new Vector2(8.75f, 0), result.Knockback);
        Assert.Contains(result.Events, gameEvent => gameEvent is CombatBlockedEvent);
    }

    [Fact]
    public void Resolve_RespectsConfiguredBlockAndParryWindows()
    {
        var guard = CreateGuard(new GuardDefinition
        {
            BlockWindowStartSeconds = 0.1f,
            BlockWindowEndSeconds = 0.5f,
            ParryWindowStartSeconds = 0.1f,
            ParryWindowSeconds = 0.05f
        });
        Assert.True(guard.TryBeginGuard(-Vector2.UnitX));
        var resolver = CreateResolver();

        var beforeBlock = resolver.Resolve(CreateRequest(), guard: guard);
        guard.Update(0.12f);
        var duringParry = resolver.Resolve(CreateRequest() with { AttackInstanceId = 43 }, guard: guard);
        guard.Update(0.5f);
        var afterBlock = resolver.Resolve(CreateRequest() with { AttackInstanceId = 44 }, guard: guard);

        Assert.Equal(CombatHitOutcome.Applied, beforeBlock.Outcome);
        Assert.Equal(CombatHitOutcome.Parried, duringParry.Outcome);
        Assert.Equal(CombatHitOutcome.Applied, afterBlock.Outcome);
    }

    [Fact]
    public void Resolve_AttackOutsideShieldArcBypassesGuard()
    {
        var guard = CreateGuard(new GuardDefinition
        {
            ShieldArcRadians = MathF.PI / 2,
            ParryWindowSeconds = 0.1f
        });
        Assert.True(guard.TryBeginGuard(-Vector2.UnitX));

        var result = CreateResolver().Resolve(CreateRequest() with
        {
            ImpactDirection = -Vector2.UnitX
        }, guard: guard);

        Assert.Equal(CombatHitOutcome.Applied, result.Outcome);
        Assert.Equal(20, result.DamageApplied);
        Assert.Equal(100, guard.Stamina);
    }

    [Fact]
    public void Resolve_InsufficientGuardStaminaCausesGuardBreak()
    {
        var guard = CreateGuard(stamina: 10);
        Assert.True(guard.TryBeginGuard(-Vector2.UnitX));
        guard.Update(0.2f);

        var result = CreateResolver().Resolve(CreateRequest(), guard: guard);

        Assert.Equal(CombatHitOutcome.GuardBroken, result.Outcome);
        Assert.Equal(20, result.DamageApplied);
        Assert.Equal(10, result.GuardStaminaSpent);
        Assert.True(guard.IsGuardBroken);
        Assert.False(guard.IsGuarding);
        Assert.Contains(result.Events, gameEvent => gameEvent is GuardBrokenCombatEvent);
    }

    [Fact]
    public void GuardBreak_UpdateRecoversThenRegeneratesStamina()
    {
        var guard = CreateGuard(new GuardDefinition
        {
            GuardBreakDurationSeconds = 0.5f,
            StaminaRegenerationPerSecond = 20
        }, stamina: 1);
        Assert.True(guard.TryBeginGuard(-Vector2.UnitX));
        guard.Update(0.2f);
        _ = CreateResolver().Resolve(CreateRequest(), guard: guard);

        guard.Update(0.75f);

        Assert.False(guard.IsGuardBroken);
        Assert.Equal(5, guard.Stamina, precision: 3);
        Assert.True(guard.TryBeginGuard(-Vector2.UnitX));
    }

    [Fact]
    public void ApplyCommand_SupportsHoldAndToggleWithoutReopeningParryWindow()
    {
        var guard = CreateGuard();

        var begin = guard.ApplyCommand(GuardCommand.Begin(-Vector2.UnitX));
        guard.Update(0.2f);
        var repeatedHold = guard.ApplyCommand(GuardCommand.Begin(-Vector2.UnitX));
        var parryReopenedByHold = guard.IsParryWindowOpen;
        var toggleOff = guard.ApplyCommand(GuardCommand.Toggle(-Vector2.UnitX));
        var toggleOn = guard.ApplyCommand(GuardCommand.Toggle(-Vector2.UnitX));

        Assert.True(begin.Accepted);
        Assert.True(repeatedHold.Accepted);
        Assert.False(parryReopenedByHold);
        Assert.False(toggleOff.IsGuarding);
        Assert.True(toggleOn.IsGuarding);
        Assert.True(guard.IsParryWindowOpen);
    }

    [Fact]
    public void Resolve_CombinesFlatGlobalAndDamageTypeMitigation()
    {
        var mitigation = new DamageMitigationProfile
        {
            FlatReduction = 10,
            GlobalReduction = 0.2f,
            TypeReductions = new Dictionary<DamageType, float>
            {
                [DamageType.Melee] = 0.25f
            },
            KnockbackResistance = 0.5f
        };

        var result = CreateResolver().Resolve(CreateRequest() with
        {
            BaseDamage = 100,
            KnockbackForce = 40
        }, mitigation);

        Assert.Equal(54, result.DamageApplied);
        Assert.Equal(new Vector2(20, 0), result.Knockback);
    }

    [Fact]
    public void Resolve_RejectsFriendlyFireWithoutConsumingExactOnceHit()
    {
        var resolver = CreateResolver();
        var friendlyRequest = CreateRequest() with
        {
            SourceFaction = EntityFaction.Friendly,
            TargetFaction = EntityFaction.Friendly
        };

        var rejected = resolver.Resolve(friendlyRequest);
        var hostile = resolver.Resolve(friendlyRequest with { TargetFaction = EntityFaction.Hostile });

        Assert.Equal(CombatHitOutcome.RejectedFriendlyFire, rejected.Outcome);
        Assert.Equal(CombatHitOutcome.Applied, hostile.Outcome);
    }

    [Fact]
    public void Resolve_RegistersEachAttackTargetExactlyOnce()
    {
        var resolver = CreateResolver();
        var request = CreateRequest();

        var first = resolver.Resolve(request);
        var duplicate = resolver.Resolve(request);
        resolver.HitTracker.CompleteAttack(request.AttackInstanceId);
        var afterCompletion = resolver.Resolve(request);

        Assert.Equal(CombatHitOutcome.Applied, first.Outcome);
        Assert.Equal(CombatHitOutcome.RejectedDuplicate, duplicate.Outcome);
        Assert.Equal(CombatHitOutcome.Applied, afterCompletion.Outcome);
    }

    [Fact]
    public void Resolve_CriticalSequenceIsDeterministicAndStreamIsolated()
    {
        var firstRegistry = new SessionRandomRegistry(7744);
        var secondRegistry = new SessionRandomRegistry(7744);
        _ = secondRegistry.GetStream("unrelated").NextUInt64();
        var first = new CombatDamageResolver(
            firstRegistry.GetStream("combat.critical"),
            firstRegistry.GetStream("combat.status"));
        var second = new CombatDamageResolver(
            secondRegistry.GetStream("combat.critical"),
            secondRegistry.GetStream("combat.status"));

        var firstTrace = ResolveCriticalTrace(first);
        var secondTrace = ResolveCriticalTrace(second);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Contains(true, firstTrace);
        Assert.Contains(false, firstTrace);
    }

    [Fact]
    public void Resolve_SelectsStatusesOnceAndNormalizesTheirChance()
    {
        var result = CreateResolver().Resolve(CreateRequest() with
        {
            StatusEffects = new[]
            {
                new StatusEffectApplication { EffectId = "always", Chance = 1 },
                new StatusEffectApplication { EffectId = "never", Chance = 0 }
            }
        });

        var selected = Assert.Single(result.StatusEffects);
        Assert.Equal("always", selected.EffectId);
        Assert.Equal(1, selected.Chance);
    }

    private static GuardRuntimeState CreateGuard(GuardDefinition? definition = null, float? stamina = null)
    {
        return new GuardRuntimeState(definition ?? new GuardDefinition
        {
            ParryWindowSeconds = 0.1f,
            BlockDamageReduction = 0.75f,
            BlockKnockbackMultiplier = 0.35f
        }, stamina);
    }

    private static CombatDamageResolver CreateResolver()
    {
        var randoms = new SessionRandomRegistry(12345);
        return new CombatDamageResolver(
            randoms.GetStream("combat.critical"),
            randoms.GetStream("combat.status"));
    }

    private static CombatDamageRequest CreateRequest()
    {
        return new CombatDamageRequest
        {
            AttackInstanceId = 42,
            SourceEntityId = 1,
            TargetEntityId = 2,
            SourceFaction = EntityFaction.Friendly,
            TargetFaction = EntityFaction.Hostile,
            BaseDamage = 20,
            DamageType = DamageType.Melee,
            ImpactDirection = Vector2.UnitX,
            KnockbackForce = 25
        };
    }

    private static bool[] ResolveCriticalTrace(CombatDamageResolver resolver)
    {
        var trace = new bool[32];
        for (var index = 0; index < trace.Length; index++)
        {
            trace[index] = resolver.Resolve(CreateRequest() with
            {
                AttackInstanceId = (ulong)(index + 1),
                CriticalChance = 0.5f
            }).Critical;
        }

        return trace;
    }
}
