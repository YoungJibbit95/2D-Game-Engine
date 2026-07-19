using System.Numerics;
using Game.Core.Physics;

namespace Game.Core.Movement;

/// <summary>
/// Fixed-step locomotion policy that translates character intent into physics-body motion.
/// The controller owns only short-lived input grace state; physics remains authoritative
/// for gravity, integration and collision contacts.
/// </summary>
public sealed class SideViewCharacterController
{
    private readonly SideViewCharacterControllerOptions _options;
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

    public void Reset()
    {
        _coyoteTimeRemaining = 0f;
        _jumpBufferRemaining = 0f;
        _jumpWasHeld = false;
        _jumpCutAvailable = false;
    }

    public void ApplyIntent(
        PhysicsBody body,
        SideViewCharacterInput input,
        float deltaSeconds,
        float speedMultiplier = 1f)
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

        if (body.OnGround)
        {
            _coyoteTimeRemaining = _options.CoyoteTimeSeconds;
            _jumpCutAvailable = false;
        }

        var targetSpeed = moveAxis * _options.MaxGroundSpeed * normalizedMultiplier;
        var velocity = body.Velocity;
        var hasMovementIntent = MathF.Abs(targetSpeed) > 0.01f;
        var acceleration = body.OnGround ? _options.GroundAcceleration : _options.AirAcceleration;
        var friction = body.OnGround ? _options.GroundFriction : _options.AirFriction;

        velocity.X = MoveToward(
            velocity.X,
            hasMovementIntent ? targetSpeed : 0f,
            (hasMovementIntent ? acceleration : friction) * deltaSeconds);

        if (_jumpBufferRemaining > 0f && _coyoteTimeRemaining > 0f)
        {
            velocity.Y = -_options.JumpSpeed;
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

        body.Velocity = velocity;
        _jumpWasHeld = input.WantsJump;
        _coyoteTimeRemaining = CountDown(_coyoteTimeRemaining, deltaSeconds);
        _jumpBufferRemaining = CountDown(_jumpBufferRemaining, deltaSeconds);
    }

    private static float CountDown(float remaining, float deltaSeconds)
    {
        return remaining > deltaSeconds ? remaining - deltaSeconds : 0f;
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
        if (!float.IsFinite(options.JumpReleaseVelocityMultiplier) ||
            options.JumpReleaseVelocityMultiplier is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.JumpReleaseVelocityMultiplier),
                options.JumpReleaseVelocityMultiplier,
                "Jump release velocity multiplier must be finite and between zero and one.");
        }
    }

    private static void ValidateNonNegativeFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Movement tuning must be finite and non-negative.");
        }
    }
}
