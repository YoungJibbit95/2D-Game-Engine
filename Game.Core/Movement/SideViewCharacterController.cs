using System.Numerics;
using Game.Core.Physics;

namespace Game.Core.Movement;

/// <summary>
/// Fixed-step locomotion policy that translates character intent into physics-body motion.
/// Physics remains authoritative for gravity, integration and collision. The controller owns
/// input grace and one renderer-neutral mobility runtime.
/// </summary>
public sealed class SideViewCharacterController
{
    private readonly SideViewCharacterControllerOptions _options;
    private readonly MobilityAbilityRuntime _mobility = new();
    private float _coyoteTimeRemaining;
    private float _jumpBufferRemaining;
    private bool _jumpWasHeld;
    private bool _jumpCutAvailable;

    public SideViewCharacterController(
        SideViewCharacterControllerOptions? options = null)
    {
        _options = options ?? SideViewCharacterControllerOptions.Default;
        Validate(_options);
    }

    public SideViewCharacterControllerOptions Options => _options;

    public int AirJumpsRemaining => _mobility.AirJumpsRemaining;

    public float FlightTimeRemaining => _mobility.FlightTimeRemaining;

    public MobilityAbilitySnapshot MobilitySnapshot => _mobility.Snapshot;

    public void Reset()
    {
        _coyoteTimeRemaining = 0f;
        _jumpBufferRemaining = 0f;
        _jumpWasHeld = false;
        _jumpCutAvailable = false;
        _mobility.ResetForRespawn();
    }

    public void ApplyIntent(
        PhysicsBody body,
        SideViewCharacterInput input,
        float deltaSeconds = 1f / 60f,
        float speedMultiplier = 1f,
        PhysicsContactFlags contactFlags = PhysicsContactFlags.None)
    {
        var abilities = MobilityAbilityProfile.FromLegacy(input, _options);
        ApplyIntent(
            body,
            input,
            abilities,
            deltaSeconds,
            speedMultiplier,
            contactFlags);
    }

    public void ApplyIntent(
        PhysicsBody body,
        SideViewCharacterInput input,
        MobilityAbilityProfile abilities,
        float deltaSeconds = 1f / 60f,
        float speedMultiplier = 1f,
        PhysicsContactFlags contactFlags = PhysicsContactFlags.None)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            return;
        }

        var moveAxis = float.IsFinite(input.MoveAxis)
            ? Math.Clamp(input.MoveAxis, -1f, 1f)
            : 0f;
        var normalizedMultiplier = float.IsFinite(speedMultiplier)
            ? Math.Max(0f, speedMultiplier)
            : 0f;
        var jumpPressed = input.WantsJump && !_jumpWasHeld;
        if (jumpPressed)
        {
            _jumpBufferRemaining = _options.JumpBufferSeconds;
        }

        var isOnGround = (contactFlags & PhysicsContactFlags.Ground) != 0 || body.OnGround;
        var isTouchingLeftWall = (contactFlags & PhysicsContactFlags.LeftWall) != 0;
        var isTouchingRightWall = (contactFlags & PhysicsContactFlags.RightWall) != 0;
        var isTouchingWall = isTouchingLeftWall || isTouchingRightWall;
        var wallJumpDirection = isTouchingLeftWall ? 1f : isTouchingRightWall ? -1f : 0f;
        _mobility.Synchronize(abilities, isOnGround);

        if (isOnGround)
        {
            _coyoteTimeRemaining = _options.CoyoteTimeSeconds;
            _jumpCutAvailable = false;
        }

        var targetSpeed = moveAxis * _options.MaxGroundSpeed * normalizedMultiplier;
        var velocity = body.Velocity;
        var hasMovementIntent = MathF.Abs(targetSpeed) > 0.01f;
        var acceleration = isOnGround ? _options.GroundAcceleration : _options.AirAcceleration;
        var friction = isOnGround ? _options.GroundFriction : _options.AirFriction;

        velocity.X = MoveToward(
            velocity.X,
            hasMovementIntent ? targetSpeed : 0f,
            (hasMovementIntent ? acceleration : friction) * deltaSeconds);

        var canJumpFromGround = _coyoteTimeRemaining > 0f;
        var canReachWallJump = abilities.CanWallJump &&
                               isTouchingWall &&
                               !_jumpWasHeld &&
                               !isOnGround;
        var canReachDoubleJump = abilities.HasDoubleJump &&
                                 _mobility.AirJumpsRemaining > 0 &&
                                 !canJumpFromGround;

        if (_jumpBufferRemaining > 0f && (canJumpFromGround || canReachWallJump || canReachDoubleJump))
        {
            if (canJumpFromGround)
            {
                velocity.Y = -_options.JumpSpeed;
            }
            else if (canReachWallJump)
            {
                velocity.X = wallJumpDirection * _options.WallJumpHorizontalSpeed;
                velocity.Y = -_options.WallJumpVerticalSpeed;
            }
            else if (_mobility.TryConsumeAirJump())
            {
                velocity.Y = -_options.JumpSpeed * abilities.AirJumpVelocityMultiplier;
            }

            body.OnGround = false;
            _jumpBufferRemaining = 0f;
            _coyoteTimeRemaining = 0f;
            _jumpCutAvailable = input.WantsJump;
            if (!input.WantsJump)
            {
                velocity.Y *= _options.JumpReleaseVelocityMultiplier;
            }
        }
        else if (!input.WantsJump && _jumpWasHeld && _jumpCutAvailable && velocity.Y < 0f)
        {
            velocity.Y *= _options.JumpReleaseVelocityMultiplier;
            _jumpCutAvailable = false;
        }

        var flightConsumed = input.WantsFly
            ? _mobility.ConsumeFlight(deltaSeconds)
            : 0f;
        if (flightConsumed > 0f)
        {
            body.OnGround = false;
            body.GravityScale = _options.FlyGravityScale;
            var flyTarget = -_options.FlyVerticalSpeed * abilities.FlightVerticalSpeedMultiplier;
            var flyAcceleration = _options.FlyAscendAcceleration *
                                  abilities.FlightAccelerationMultiplier;
            velocity.Y = MoveToward(
                velocity.Y,
                flyTarget,
                flyAcceleration * flightConsumed);
        }
        else if (abilities.HasGlide && input.WantsGlide && !isOnGround && velocity.Y >= 0f)
        {
            body.GravityScale = abilities.GlideGravityScale;
            if (velocity.Y > abilities.GlideTerminalVelocity)
            {
                velocity.Y = MoveToward(
                    velocity.Y,
                    abilities.GlideTerminalVelocity,
                    _options.AirAcceleration * 0.6f * deltaSeconds);
            }
        }
        else
        {
            body.GravityScale = 1f;
        }

        body.Velocity = velocity;
        _jumpWasHeld = input.WantsJump;
        if (!isOnGround || !body.OnGround)
        {
            _coyoteTimeRemaining = CountDown(_coyoteTimeRemaining, deltaSeconds);
        }

        _jumpBufferRemaining = CountDown(_jumpBufferRemaining, deltaSeconds);
    }

    private static float CountDown(float remaining, float deltaSeconds)
    {
        var next = remaining - deltaSeconds;
        return next > 0.000001f ? next : 0f;
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + (MathF.Sign(target - current) * maxDelta);
    }

    private static void Validate(SideViewCharacterControllerOptions options)
    {
        ValidateNonNegativeFinite(options.GroundAcceleration, nameof(options.GroundAcceleration));
        ValidateNonNegativeFinite(options.AirAcceleration, nameof(options.AirAcceleration));
        ValidateNonNegativeFinite(options.GroundFriction, nameof(options.GroundFriction));
        ValidateNonNegativeFinite(options.AirFriction, nameof(options.AirFriction));
        ValidateNonNegativeFinite(options.MaxGroundSpeed, nameof(options.MaxGroundSpeed));
        ValidateNonNegativeFinite(options.JumpSpeed, nameof(options.JumpSpeed));
        ValidateNonNegativeFinite(options.CoyoteTimeSeconds, nameof(options.CoyoteTimeSeconds));
        ValidateNonNegativeFinite(options.JumpBufferSeconds, nameof(options.JumpBufferSeconds));
        ValidateNonNegative(options.MaxAirJumps, nameof(options.MaxAirJumps));
        ValidateNonNegativeFinite(options.DoubleJumpVelocityMultiplier, nameof(options.DoubleJumpVelocityMultiplier));
        ValidateNonNegativeFinite(options.WallJumpHorizontalSpeed, nameof(options.WallJumpHorizontalSpeed));
        ValidateNonNegativeFinite(options.WallJumpVerticalSpeed, nameof(options.WallJumpVerticalSpeed));
        ValidateFiniteInRange(options.GlideGravityScale, 0.01f, 1f, nameof(options.GlideGravityScale));
        ValidateNonNegativeFinite(options.GlideTerminalVelocity, nameof(options.GlideTerminalVelocity));
        ValidateFiniteInRange(options.FlyGravityScale, 0f, 0.3f, nameof(options.FlyGravityScale));
        ValidateNonNegativeFinite(options.FlightDurationSeconds, nameof(options.FlightDurationSeconds));
        ValidateNonNegativeFinite(options.FlyVerticalSpeed, nameof(options.FlyVerticalSpeed));
        ValidateNonNegativeFinite(options.FlyAscendAcceleration, nameof(options.FlyAscendAcceleration));
        ValidateNonNegativeFinite(options.FlyDescentHoldSpeed, nameof(options.FlyDescentHoldSpeed));
        if (!float.IsFinite(options.JumpReleaseVelocityMultiplier) ||
            options.JumpReleaseVelocityMultiplier is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.JumpReleaseVelocityMultiplier),
                options.JumpReleaseVelocityMultiplier,
                "Jump release velocity multiplier must be finite and between zero and one.");
        }
    }

    private static void ValidateFiniteInRange(
        float value,
        float minInclusive,
        float maxInclusive,
        string parameterName)
    {
        if (!float.IsFinite(value) || value < minInclusive || value > maxInclusive)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} must be finite and between {minInclusive} and {maxInclusive}.");
        }
    }

    private static void ValidateNonNegativeFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Movement tuning must be finite and non-negative.");
        }
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Movement tuning must be non-negative.");
        }
    }
}