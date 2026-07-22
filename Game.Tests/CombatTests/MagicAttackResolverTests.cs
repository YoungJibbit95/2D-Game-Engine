using Game.Core.Combat;
using Game.Core.Effects;
using Game.Core.Equipment;
using Game.Core.Items;
using Game.Core.Projectiles;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class MagicAttackResolverTests
{
    [Fact]
    public void ResolveProjectile_AppliesMagicStatsKnockbackAndMergedStatusEffects()
    {
        var projectile = new ProjectileDefinition
        {
            Id = "arcane_bolt",
            TexturePath = "projectiles/arcane_bolt",
            Speed = 240f,
            Damage = 4,
            DamageType = DamageType.Ranged,
            Knockback = 0.75f,
            Lifetime = 3f,
            OnHitEffects =
            [
                new StatusEffectApplication
                {
                    EffectId = "arcane_chill",
                    Chance = 0.25f,
                    DurationSeconds = 2f
                }
            ]
        };
        var item = new ItemDefinition
        {
            Id = "test_wand",
            DisplayName = "Test Wand",
            Type = ItemType.WeaponMagic,
            TexturePath = "items/test_wand",
            Damage = 6,
            Knockback = 2f,
            OnHitEffects =
            [
                new StatusEffectApplication
                {
                    EffectId = "ARCANE_CHILL",
                    Chance = 0.5f,
                    DurationSeconds = 4f
                },
                new StatusEffectApplication
                {
                    EffectId = "exposed",
                    Chance = 1f
                }
            ]
        };
        var stats = PlayerStatBlock.Base with { MagicDamageMultiplier = 1.5f };

        var resolved = MagicAttackResolver.ResolveProjectile(projectile, item, stats);

        Assert.Equal(DamageType.Magic, resolved.DamageType);
        Assert.Equal(15, resolved.Damage);
        Assert.Equal(1.5f, resolved.Knockback, precision: 4);
        Assert.Equal(2, resolved.OnHitEffects.Count);
        var chill = resolved.OnHitEffects[0];
        Assert.Equal("arcane_chill", chill.EffectId);
        Assert.Equal(0.625f, chill.Chance, precision: 4);
        Assert.Equal(4f, chill.DurationSeconds);
        Assert.Equal("exposed", resolved.OnHitEffects[1].EffectId);
        Assert.Equal(DamageType.Ranged, projectile.DamageType);
        Assert.Single(projectile.OnHitEffects);
    }

    [Fact]
    public void ResolveProjectile_UsesProjectileKnockbackWhenItemDoesNotAuthorOne()
    {
        var projectile = new ProjectileDefinition
        {
            Id = "mote",
            TexturePath = "projectiles/mote",
            Speed = 100f,
            Damage = 1,
            Knockback = 1.75f,
            Lifetime = 1f
        };
        var item = new ItemDefinition
        {
            Id = "tome",
            DisplayName = "Tome",
            Type = ItemType.WeaponMagic,
            TexturePath = "items/tome"
        };

        var resolved = MagicAttackResolver.ResolveProjectile(
            projectile,
            item,
            PlayerStatBlock.Base);

        Assert.Equal(1.75f, resolved.Knockback);
        Assert.Equal(DamageType.Magic, resolved.DamageType);
    }
}