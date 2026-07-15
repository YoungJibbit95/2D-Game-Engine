using Game.Core.Combat;
using Game.Core.Effects;
using Game.Core.Entities;
using Game.Core.Projectiles;
using System.Numerics;
using Xunit;

namespace Game.Tests.ProjectileTests;

public sealed class ProjectileRuntimeStateTests
{
    [Fact]
    public void Advance_StopsExactlyAtLifetimeBoundary()
    {
        var runtime = CreateRuntime(CreateDefinition() with { Lifetime = 0.5f });

        var result = runtime.Advance(0.75f);

        Assert.True(result.Expired);
        Assert.False(runtime.IsActive);
        Assert.Equal(ProjectileTerminationReason.LifetimeExpired, runtime.TerminationReason);
        Assert.Equal(0.5f, runtime.AgeSeconds);
        Assert.Equal(50, runtime.Position.X, precision: 3);
    }

    [Fact]
    public void Advance_AppliesGravityAndExponentialDrag()
    {
        var runtime = CreateRuntime(CreateDefinition() with
        {
            Gravity = 1,
            DragPerSecond = MathF.Log(2)
        });

        runtime.Advance(1);

        Assert.Equal(50, runtime.Velocity.X, precision: 3);
        Assert.Equal(490, runtime.Velocity.Y, precision: 3);
    }

    [Fact]
    public void Advance_HomingSelectsNearestThenLowestEntityId()
    {
        var runtime = CreateRuntime(CreateDefinition() with
        {
            HomingRange = 200,
            HomingTurnRateRadiansPerSecond = MathF.PI
        });
        var targets = new[]
        {
            new ProjectileHomingTarget(9, EntityFaction.Hostile, new Vector2(100, 100)),
            new ProjectileHomingTarget(3, EntityFaction.Hostile, new Vector2(100, -100)),
            new ProjectileHomingTarget(2, EntityFaction.Friendly, new Vector2(20, 0))
        };

        var result = runtime.Advance(0.25f, targets);

        Assert.Equal(3, result.HomingTargetEntityId);
        Assert.True(runtime.Velocity.Y < 0);
    }

    [Fact]
    public void ResolveEntityCollision_PiercesThenStopsAndRejectsDuplicateTarget()
    {
        var runtime = CreateRuntime(CreateDefinition() with { Pierce = 1 });

        var first = runtime.ResolveEntityCollision(new ProjectileEntityCollision(2, EntityFaction.Hostile));
        var duplicate = runtime.ResolveEntityCollision(new ProjectileEntityCollision(2, EntityFaction.Hostile));
        var second = runtime.ResolveEntityCollision(new ProjectileEntityCollision(3, EntityFaction.Hostile));

        Assert.Equal(ProjectileEntityCollisionDecision.Hit, first.Decision);
        Assert.True(first.IsActive);
        Assert.Equal(0, first.RemainingPierces);
        Assert.Equal(ProjectileEntityCollisionDecision.IgnoredAlreadyHit, duplicate.Decision);
        Assert.Equal(ProjectileEntityCollisionDecision.HitAndStopped, second.Decision);
        Assert.False(second.IsActive);
        Assert.Equal(ProjectileTerminationReason.EntityHit, second.TerminationReason);
    }

    [Fact]
    public void ResolveEntityCollision_RejectsOwnerAndFriendlyFireWithoutConsumingPierce()
    {
        var runtime = CreateRuntime(CreateDefinition() with { Pierce = 2 });

        var owner = runtime.ResolveEntityCollision(new ProjectileEntityCollision(1, EntityFaction.Friendly));
        var friendly = runtime.ResolveEntityCollision(new ProjectileEntityCollision(2, EntityFaction.Friendly));

        Assert.Equal(ProjectileEntityCollisionDecision.IgnoredOwner, owner.Decision);
        Assert.Equal(ProjectileEntityCollisionDecision.IgnoredFriendly, friendly.Decision);
        Assert.Equal(2, runtime.RemainingPierces);
        Assert.True(runtime.IsActive);
    }

    [Fact]
    public void ResolveEntityCollision_FriendlyFireCanBeEnabledExplicitly()
    {
        var runtime = CreateRuntime(CreateDefinition() with { FriendlyFire = true });

        var result = runtime.ResolveEntityCollision(new ProjectileEntityCollision(2, EntityFaction.Friendly));

        Assert.True(result.Accepted);
        Assert.NotNull(result.DamageRequest);
        Assert.Equal(EntityFaction.Friendly, result.DamageRequest.TargetFaction);
    }

    [Fact]
    public void ResolveEntityCollision_ProducesTypedDamageRequest()
    {
        var definition = CreateDefinition() with
        {
            Damage = 17,
            DamageType = DamageType.Magic,
            Knockback = 25,
            CriticalChance = 0.4f,
            OnHitEffects = new[]
            {
                new StatusEffectApplication { EffectId = "burning", Chance = 0.5f }
            }
        };
        var runtime = CreateRuntime(definition);

        var result = runtime.ResolveEntityCollision(new ProjectileEntityCollision(4, EntityFaction.Hostile));

        Assert.NotNull(result.DamageRequest);
        Assert.Equal(17, result.DamageRequest.BaseDamage);
        Assert.Equal(DamageType.Magic, result.DamageRequest.DamageType);
        Assert.Equal(25, result.DamageRequest.KnockbackForce);
        Assert.Single(result.DamageRequest.StatusEffects);
    }

    [Fact]
    public void ResolveTileCollision_BouncesConfiguredCountThenDestroys()
    {
        var runtime = CreateRuntime(CreateDefinition() with
        {
            TileCollisionBehavior = ProjectileTileCollisionBehavior.Bounce,
            BounceCount = 1,
            BounceRestitution = 0.5f
        }, new Vector2(100, 20));

        var first = runtime.ResolveTileCollision(new ProjectileTileCollision(
            new Vector2(10, 0),
            -Vector2.UnitX));
        var second = runtime.ResolveTileCollision(new ProjectileTileCollision(
            new Vector2(0, 0),
            Vector2.UnitX));

        Assert.Equal(ProjectileTileCollisionDecision.Bounced, first.Decision);
        Assert.Equal(new Vector2(-50, 10), first.Velocity);
        Assert.Equal(0, first.RemainingBounces);
        Assert.True(first.IsActive);
        Assert.Equal(ProjectileTileCollisionDecision.Destroyed, second.Decision);
        Assert.False(second.IsActive);
    }

    [Fact]
    public void ResolveTileCollision_IgnorePolicyLeavesRuntimeUnchanged()
    {
        var runtime = CreateRuntime(CreateDefinition() with
        {
            TileCollisionBehavior = ProjectileTileCollisionBehavior.Ignore
        });
        var velocity = runtime.Velocity;

        var result = runtime.ResolveTileCollision(new ProjectileTileCollision(
            new Vector2(10, 0),
            -Vector2.UnitX));

        Assert.Equal(ProjectileTileCollisionDecision.IgnoredByDefinition, result.Decision);
        Assert.Equal(velocity, runtime.Velocity);
        Assert.True(runtime.IsActive);
    }

    private static ProjectileRuntimeState CreateRuntime(
        ProjectileDefinition definition,
        Vector2? velocity = null)
    {
        return new ProjectileRuntimeState(
            instanceId: 99,
            definition,
            position: Vector2.Zero,
            velocity ?? new Vector2(100, 0),
            ownerEntityId: 1,
            ownerFaction: EntityFaction.Friendly);
    }

    private static ProjectileDefinition CreateDefinition()
    {
        return new ProjectileDefinition
        {
            Id = "test-projectile",
            TexturePath = "projectiles/test",
            Speed = 100,
            Damage = 5,
            Lifetime = 5
        };
    }
}
