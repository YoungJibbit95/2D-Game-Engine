using Game.Core.Combat;
using Game.Core.Equipment;
using Game.Core.Effects;
using Game.Core.Movement;
using Game.Core.Physics;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class PlayerEntity : Entity
{
    private readonly SideViewCharacterController _movementController;
    private readonly PhysicsWorld _physicsWorld;
    private readonly PhysicsBody[] _physicsBodies;
    private readonly PhysicsMoveResult[] _physicsResults = new PhysicsMoveResult[1];
    private readonly PhysicsContact[] _physicsContacts = new PhysicsContact[4];

    private PlayerCommand _command;

    public PlayerEntity(
        Vector2 spawnPosition,
        TileCollisionResolver collisionResolver,
        int maxHealth = 100,
        int? currentHealth = null,
        int maxMana = 20,
        int? currentMana = null)
    {
        HealthComponent = new HealthComponent(maxHealth, currentHealth);
        ManaComponent = new ManaComponent(maxMana, currentMana);
        Body = new PhysicsBody
        {
            Position = spawnPosition,
            Size = new Vector2(12, 28),
            BodyType = PhysicsBodyType.Dynamic,
            CollisionLayer = PhysicsCollisionLayer.Player
        };
        _movementController = new SideViewCharacterController();
        _physicsWorld = new PhysicsWorld(
            collisionResolver,
            new PhysicsStepSettings(
                new Vector2(0f, 1050f),
                1f / 15f,
                620f,
                1,
                _physicsContacts.Length));
        _physicsBodies = [Body];
        Position = spawnPosition;
    }

    public PhysicsBody Body { get; }

    public HealthComponent HealthComponent { get; }

    public ManaComponent ManaComponent { get; }

    public StatusEffectCollection StatusEffects { get; } = new();

    public PlayerStatBlock Stats { get; private set; } = PlayerStatBlock.Base;

    public int Health => HealthComponent.Current;

    public int MaxHealth => HealthComponent.Max;

    public int Mana => ManaComponent.Current;

    public int MaxMana => ManaComponent.Max;

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
        _movementController.Reset();
        HealthComponent.RestoreFull();
        ManaComponent.RestoreFull();
        _command = PlayerCommand.None;
    }

    public void ApplyStats(PlayerStatBlock stats)
    {
        Stats = stats;
        HealthComponent.SetMax(stats.MaxHealth);
        ManaComponent.SetMax(stats.MaxMana);
    }

    public override void Update(GameWorld world, float deltaSeconds)
    {
        StatusEffects.Update(deltaSeconds, HealthComponent);
        HealthComponent.Update(deltaSeconds);
        ManaComponent.Update(deltaSeconds, regenMultiplier: Stats.ManaRegenMultiplier);
        _movementController.ApplyIntent(
            Body,
            new SideViewCharacterInput(_command.MoveAxis, _command.WantsJump),
            deltaSeconds,
            Stats.MovementSpeedMultiplier);
        _physicsWorld.Step(world, _physicsBodies, deltaSeconds, _physicsResults, _physicsContacts);
        Position = Body.Position;
    }
}
