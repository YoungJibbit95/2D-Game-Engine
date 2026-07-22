using Game.Core.Movement;
using Xunit;

namespace Game.Tests.MovementTests;

public sealed class MobilityAbilityRuntimeTests
{
    [Fact]
    public void Landing_ReplenishesConsumedAirJumpsAndFlight()
    {
        var runtime = new MobilityAbilityRuntime();
        var profile = CreateProfile(extraJumps: 2, flightSeconds: 1.5f);

        runtime.Synchronize(profile, isGrounded: true);
        Assert.True(runtime.TryConsumeAirJump());
        Assert.Equal(0.5f, runtime.ConsumeFlight(0.5f));
        runtime.Synchronize(profile, isGrounded: false);
        runtime.Synchronize(profile, isGrounded: true);

        Assert.Equal(2, runtime.AirJumpsRemaining);
        Assert.Equal(1.5f, runtime.FlightTimeRemaining);
        Assert.Equal(MobilityAbilityResetReason.Landed, runtime.LastResetReason);
    }

    [Fact]
    public void EquipmentAddedInAir_DoesNotGrantFreshConsumableAbilities()
    {
        var runtime = new MobilityAbilityRuntime();
        runtime.Synchronize(MobilityAbilityProfile.Disabled, isGrounded: true);
        runtime.Synchronize(MobilityAbilityProfile.Disabled, isGrounded: false);

        var profile = CreateProfile(extraJumps: 1, flightSeconds: 2f);
        runtime.Synchronize(profile, isGrounded: false);

        Assert.Equal(0, runtime.AirJumpsRemaining);
        Assert.Equal(0f, runtime.FlightTimeRemaining);
        Assert.False(runtime.TryConsumeAirJump());
        Assert.Equal(0f, runtime.ConsumeFlight(0.25f));
        Assert.Equal(MobilityAbilityResetReason.EquipmentChangedInAir, runtime.LastResetReason);
    }

    [Fact]
    public void EquipmentRemovalInAir_ClearsRemainingAbilitiesImmediately()
    {
        var runtime = new MobilityAbilityRuntime();
        var profile = CreateProfile(extraJumps: 2, flightSeconds: 2f);
        runtime.Synchronize(profile, isGrounded: true);
        runtime.Synchronize(profile, isGrounded: false);

        runtime.Synchronize(MobilityAbilityProfile.Disabled, isGrounded: false);

        Assert.Equal(0, runtime.AirJumpsRemaining);
        Assert.Equal(0f, runtime.FlightTimeRemaining);
        Assert.Equal(MobilityAbilityResetReason.EquipmentChangedInAir, runtime.LastResetReason);
    }

    [Fact]
    public void RespawnReset_ReplenishesEquippedAbilitiesEvenBeforeLanding()
    {
        var runtime = new MobilityAbilityRuntime();
        var profile = CreateProfile(extraJumps: 1, flightSeconds: 1f);
        runtime.Synchronize(profile, isGrounded: true);
        Assert.True(runtime.TryConsumeAirJump());
        Assert.Equal(1f, runtime.ConsumeFlight(1f));

        runtime.ResetForRespawn();
        runtime.Synchronize(profile, isGrounded: false);

        Assert.Equal(1, runtime.AirJumpsRemaining);
        Assert.Equal(1f, runtime.FlightTimeRemaining);
        Assert.Equal(MobilityAbilityResetReason.Respawned, runtime.LastResetReason);
    }

    [Fact]
    public void FlightConsumption_IsTimedClampedAndCannotGoNegative()
    {
        var runtime = new MobilityAbilityRuntime();
        var profile = CreateProfile(extraJumps: 0, flightSeconds: 0.3f);
        runtime.Synchronize(profile, isGrounded: true);

        Assert.Equal(0.2f, runtime.ConsumeFlight(0.2f), precision: 5);
        Assert.Equal(0.1f, runtime.ConsumeFlight(0.2f), precision: 5);
        Assert.Equal(0f, runtime.ConsumeFlight(0.1f));
        Assert.Equal(0f, runtime.FlightTimeRemaining);
    }

    [Fact]
    public void SynchronizeAndConsume_SteadyStateAllocatesNoManagedMemory()
    {
        var runtime = new MobilityAbilityRuntime();
        var profile = CreateProfile(extraJumps: 1, flightSeconds: 1f);
        for (var index = 0; index < 128; index++)
        {
            ExecuteCycle(runtime, profile);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
        {
            ExecuteCycle(runtime, profile);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void ExecuteCycle(
        MobilityAbilityRuntime runtime,
        MobilityAbilityProfile profile)
    {
        runtime.Synchronize(profile, isGrounded: true);
        runtime.Synchronize(profile, isGrounded: false);
        _ = runtime.TryConsumeAirJump();
        _ = runtime.ConsumeFlight(1f / 60f);
    }

    private static MobilityAbilityProfile CreateProfile(
        int extraJumps,
        float flightSeconds)
    {
        return new MobilityAbilityProfile(
            extraJumps,
            0.94f,
            CanWallJump: false,
            flightSeconds,
            FlightVerticalSpeedMultiplier: 1.08f,
            FlightAccelerationMultiplier: 1.1f,
            GlideEnabled: true,
            GlideGravityScale: 0.18f,
            GlideTerminalVelocity: 105f);
    }
}