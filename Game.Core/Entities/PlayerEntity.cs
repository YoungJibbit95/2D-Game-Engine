using Game.Core.Combat;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class PlayerEntity : Entity
{
    private const float Acceleration = 2600f;
    private const float GroundFriction = 1800f;
    private const float MaxWalkSpeed = 145f;
    private const float Gravity = 1050f;
    private const float MaxFallSpeed = 620f;
    private const float JumpVelocity = -365f;

    private readonly TileCollisionResolver _collisionResolver;

    private PlayerCommand _command;

    public PlayerEntity(Vector2 spawnPosition, TileCollisionResolver collisionResolver, int maxHealth = 100, int? currentHealth = null)
    {
        _collisionResolver = collisionResolver;
        HealthComponent = new HealthComponent(maxHealth, currentHealth);
        Body = new PhysicsBody
        {
            Position = spawnPosition,
            Size = new Vector2(12, 28)
        };
        Position = spawnPosition;
    }

    public PhysicsBody Body { get; }

    public HealthComponent HealthComponent { get; }

    public int Health => HealthComponent.Current;

    public int MaxHealth => HealthComponent.Max;

    public override RectI Bounds => Body.Bounds;

    public void SetCommand(PlayerCommand command)
    {
        _command = command;
    }

    public bool ApplyDamage(DamageInfo damage, float invulnerabilitySeconds = 0.65f)
    {
        var applied = HealthComponent.ApplyDamage(damage, invulnerabilitySeconds);
        if (applied && damage.KnockbackForce > 0 && damage.KnockbackDirection != Vector2.Zero)
        {
            var direction = Vector2.Normalize(damage.KnockbackDirection);
            Body.Velocity += direction * damage.KnockbackForce;
        }

        return applied;
    }

    public void Respawn(Vector2 position)
    {
        Body.Position = position;
        Body.Velocity = Vector2.Zero;
        Body.OnGround = false;
        Position = position;
        HealthComponent.RestoreFull();
        _command = PlayerCommand.None;
    }

    public override void Update(GameWorld world, float deltaSeconds)
    {
        HealthComponent.Update(deltaSeconds);
        ApplyHorizontalMovement(deltaSeconds);
        ApplyJump();
        ApplyGravity(deltaSeconds);

        _collisionResolver.Move(world, Body, deltaSeconds);
        Position = Body.Position;
    }

    private void ApplyHorizontalMovement(float deltaSeconds)
    {
        var targetSpeed = Math.Clamp(_command.MoveAxis, -1f, 1f) * MaxWalkSpeed;
        var velocity = Body.Velocity;

        if (Math.Abs(targetSpeed) > 0.01f)
        {
            velocity.X = MoveToward(velocity.X, targetSpeed, Acceleration * deltaSeconds);
        }
        else
        {
            velocity.X = MoveToward(velocity.X, 0, GroundFriction * deltaSeconds);
        }

        Body.Velocity = velocity;
    }

    private void ApplyJump()
    {
        if (!_command.WantsJump || !Body.OnGround)
        {
            return;
        }

        Body.Velocity = new Vector2(Body.Velocity.X, JumpVelocity);
        Body.OnGround = false;
    }

    private void ApplyGravity(float deltaSeconds)
    {
        Body.Velocity = new Vector2(
            Body.Velocity.X,
            Math.Min(Body.Velocity.Y + Gravity * deltaSeconds, MaxFallSpeed));
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maxDelta;
    }
}
