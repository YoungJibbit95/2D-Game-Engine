using Game.Core.Combat;
using System.Numerics;
using Xunit;

namespace Game.Tests.CombatTests;

public sealed class HealthComponentTests
{
    [Fact]
    public void ApplyDamage_ReducesHealthAndStartsInvulnerability()
    {
        var health = new HealthComponent(100);

        var applied = health.ApplyDamage(new DamageInfo(25, DamageType.Melee, null, Vector2.Zero, 0));

        Assert.True(applied);
        Assert.Equal(75, health.Current);
        Assert.True(health.InvulnerabilityTimeRemaining > 0);
    }

    [Fact]
    public void ApplyDamage_DoesNothingWhileInvulnerable()
    {
        var health = new HealthComponent(100);
        health.ApplyDamage(new DamageInfo(25, DamageType.Melee, null, Vector2.Zero, 0));

        var applied = health.ApplyDamage(new DamageInfo(25, DamageType.Melee, null, Vector2.Zero, 0));

        Assert.False(applied);
        Assert.Equal(75, health.Current);
    }

    [Fact]
    public void Update_AllowsDamageAfterInvulnerabilityExpires()
    {
        var health = new HealthComponent(100);
        health.ApplyDamage(new DamageInfo(25, DamageType.Melee, null, Vector2.Zero, 0), invulnerabilitySeconds: 0.1f);

        health.Update(0.2f);
        var applied = health.ApplyDamage(new DamageInfo(25, DamageType.Melee, null, Vector2.Zero, 0));

        Assert.True(applied);
        Assert.Equal(50, health.Current);
    }
}
