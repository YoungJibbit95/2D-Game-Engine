namespace Game.Core.Movement;

public enum MobilityAbilityResetReason
{
    None,
    InitialSynchronization,
    Landed,
    Respawned,
    EquipmentChangedOnGround,
    EquipmentChangedInAir
}

public readonly record struct MobilityAbilitySnapshot(
    MobilityAbilityProfile Profile,
    int AirJumpsRemaining,
    float FlightTimeRemaining,
    bool IsGrounded,
    MobilityAbilityResetReason LastResetReason);

public sealed class MobilityAbilityRuntime
{
    private MobilityAbilityProfile _profile = MobilityAbilityProfile.Disabled;
    private int _airJumpsRemaining;
    private float _flightTimeRemaining;
    private bool _initialized;
    private bool _wasGrounded;
    private bool _respawnResetPending;

    public MobilityAbilityProfile Profile => _profile;

    public int AirJumpsRemaining => _airJumpsRemaining;

    public float FlightTimeRemaining => _flightTimeRemaining;

    public MobilityAbilityResetReason LastResetReason { get; private set; }

    public MobilityAbilitySnapshot Snapshot => new(
        _profile,
        _airJumpsRemaining,
        _flightTimeRemaining,
        _wasGrounded,
        LastResetReason);

    public void ResetForRespawn()
    {
        _profile = MobilityAbilityProfile.Disabled;
        _airJumpsRemaining = 0;
        _flightTimeRemaining = 0f;
        _initialized = false;
        _wasGrounded = false;
        _respawnResetPending = true;
        LastResetReason = MobilityAbilityResetReason.Respawned;
    }

    public MobilityAbilityResetReason Synchronize(
        MobilityAbilityProfile profile,
        bool isGrounded)
    {
        profile.Validate();

        if (!_initialized)
        {
            _profile = profile;
            Replenish();
            _initialized = true;
            _wasGrounded = isGrounded;
            LastResetReason = _respawnResetPending
                ? MobilityAbilityResetReason.Respawned
                : MobilityAbilityResetReason.InitialSynchronization;
            _respawnResetPending = false;
            return LastResetReason;
        }

        var profileChanged = profile != _profile;
        var landed = isGrounded && !_wasGrounded;
        if (profileChanged)
        {
            ApplyProfileChange(profile, isGrounded);
            LastResetReason = isGrounded
                ? MobilityAbilityResetReason.EquipmentChangedOnGround
                : MobilityAbilityResetReason.EquipmentChangedInAir;
        }
        else if (landed)
        {
            Replenish();
            LastResetReason = MobilityAbilityResetReason.Landed;
        }
        else
        {
            LastResetReason = MobilityAbilityResetReason.None;
        }

        if (isGrounded)
        {
            Replenish();
        }

        _wasGrounded = isGrounded;
        return LastResetReason;
    }

    public bool TryConsumeAirJump()
    {
        if (_airJumpsRemaining <= 0)
        {
            return false;
        }

        _airJumpsRemaining--;
        return true;
    }

    public float ConsumeFlight(float requestedSeconds)
    {
        if (!float.IsFinite(requestedSeconds) || requestedSeconds <= 0f ||
            !_profile.HasFlight || _flightTimeRemaining <= 0f)
        {
            return 0f;
        }

        var consumed = Math.Min(requestedSeconds, _flightTimeRemaining);
        _flightTimeRemaining = CountDown(_flightTimeRemaining, consumed);
        return consumed;
    }

    private void ApplyProfileChange(MobilityAbilityProfile profile, bool isGrounded)
    {
        if (isGrounded)
        {
            _profile = profile;
            Replenish();
            return;
        }

        _airJumpsRemaining = Math.Min(_airJumpsRemaining, profile.ExtraJumpCount);
        _flightTimeRemaining = Math.Min(_flightTimeRemaining, profile.FlightDurationSeconds);
        _profile = profile;
    }

    private void Replenish()
    {
        _airJumpsRemaining = _profile.ExtraJumpCount;
        _flightTimeRemaining = _profile.FlightDurationSeconds;
    }

    private static float CountDown(float remaining, float consumed)
    {
        var next = remaining - consumed;
        return next > 0.000001f ? next : 0f;
    }
}