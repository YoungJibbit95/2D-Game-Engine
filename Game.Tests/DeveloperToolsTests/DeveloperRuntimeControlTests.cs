using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Physics;
using System.Numerics;
using Xunit;

namespace Game.Tests.DeveloperToolsTests;

public sealed class DeveloperRuntimeControlTests
{
    [Fact]
    public void ResourceSettersClampAndInvalidateTransientState()
    {
        var health = new HealthComponent(100, 40);
        var mana = new ManaComponent(50, 30);
        var reservation = mana.TryReserve(5);

        health.SetCurrent(500);
        mana.SetCurrent(-10);

        Assert.Equal(100, health.Current);
        Assert.Equal(0, mana.Current);
        Assert.Equal(0, mana.OpenReservationCount);
        Assert.Equal(
            ManaReservationFinalizationStatus.InvalidReservation,
            mana.Commit(reservation.Reservation).Status);
    }

    [Fact]
    public void DeveloperOptionsControlInvulnerabilityCollisionAndSpeed()
    {
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        player.ApplyDeveloperOptions(
            invulnerable: true,
            noClip: true,
            freeFlight: true,
            movementSpeedMultiplier: 3f);

        var applied = player.ApplyDamage(new DamageInfo(
            20,
            DamageType.Contact,
            SourceEntityId: 7,
            Vector2.UnitX,
            KnockbackForce: 10f));

        Assert.False(applied);
        Assert.Equal(100, player.Health);
        Assert.False(player.Body.CollidesWithTiles);
        Assert.True(player.IsDeveloperFreeFlight);
        Assert.Equal(3f, player.DeveloperMovementSpeedMultiplier);
    }

    [Fact]
    public void TeleportSynchronizesBodyAndEntityAndClearsVelocity()
    {
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());
        player.Body.Velocity = new Vector2(80f, -20f);

        player.Teleport(new Vector2(-640f, 320f));

        Assert.Equal(new Vector2(-640f, 320f), player.Position);
        Assert.Equal(player.Position, player.Body.Position);
        Assert.Equal(Vector2.Zero, player.Body.Velocity);
        Assert.False(player.Body.OnGround);
    }

    [Fact]
    public void DeveloperOptionsRejectNonFiniteOrNonPositiveSpeed()
    {
        var player = new PlayerEntity(Vector2.Zero, new TileCollisionResolver());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            player.ApplyDeveloperOptions(false, false, false, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            player.ApplyDeveloperOptions(false, false, false, float.NaN));
    }
}
