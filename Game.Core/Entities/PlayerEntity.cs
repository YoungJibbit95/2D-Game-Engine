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
    private PhysicsContactFlags _lastContactFlags;

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

    public MobilityAbilityProfile MobilityAbilities { get; private set; } = MobilityAbilityProfile.Disabled;

    public MobilityAbilitySnapshot MobilitySnapshot => _movementController.MobilitySnapshot;

    public bool IsDeveloperInvulnerable { get; private set; }

    public bool IsDeveloperNoClip { get; private set; }

    public bool IsDeveloperFreeFlight { get; private set; }

    public float DeveloperMovementSpeedMultiplier { get; private set; } = 1f;

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
        if (IsDeveloperInvulnerable)
        {
            return false;
        }

        var applied = HealthComponent.ApplyDamage(damage, invulnerabilitySeconds);
        if (applied && damage.KnockbackForce > 0 && damage.KnockbackDirection != Vector2.Zero)
        {
            Body.ApplyImpulse(CalculateKnockbackImpulse(damage.KnockbackDirection, damage.KnockbackForce));
        }

        return applied;
    }

    private static Vector2 CalculateKnockbackImpulse(Vector2 direction, float force)
    {
        if (direction == Vector2.Zero)
        {
            return Vector2.Zero;
        }

        const float maxKnockbackForce = 720f;
        var normalized = Vector2.Normalize(direction);
        return normalized * MathF.Min(force, maxKnockbackForce);
    }

    public void Teleport(Vector2 position, bool clearVelocity = true)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        Body.Position = position;
        if (clearVelocity)
        {
            Body.Velocity = Vector2.Zero;
        }

        Body.OnGround = false;
        _lastContactFlags = PhysicsContactFlags.None;
        Position = position;
    }
    public void Respawn(Vector2 position)
    {
        Body.Position = position;
        Body.Velocity = Vector2.Zero;
        Body.OnGround = false;
        _lastContactFlags = PhysicsContactFlags.None;
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

    public void ApplyMobilityAbilities(MobilityAbilityProfile abilities)
    {
        abilities.Validate();
        MobilityAbilities = abilities;
    }

    public void ApplyDeveloperOptions(
        bool invulnerable,
        bool noClip,
        bool freeFlight,
        float movementSpeedMultiplier)
    {
        if (!float.IsFinite(movementSpeedMultiplier) || movementSpeedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(movementSpeedMultiplier));
        }

        IsDeveloperInvulnerable = invulnerable;
        IsDeveloperNoClip = noClip;
        Body.CollidesWithTiles = !noClip;
        DeveloperMovementSpeedMultiplier = movementSpeedMultiplier;
        if (IsDeveloperFreeFlight != freeFlight)
        {
            IsDeveloperFreeFlight = freeFlight;
            _movementController.Reset();
        }
    }

    public override void Update(GameWorld world, float deltaSeconds)
    {
        StatusEffects.Update(deltaSeconds, HealthComponent);
        HealthComponent.Update(deltaSeconds);
        ManaComponent.Update(deltaSeconds, regenMultiplier: Stats.ManaRegenMultiplier);
        var mobility = IsDeveloperFreeFlight
            ? MobilityAbilities with
            {
                ExtraJumpCount = MobilityAbilityDefinition.MaximumExtraJumpCount,
                FlightDurationSeconds = float.MaxValue,
                FlightVerticalSpeedMultiplier = Math.Max(1f, MobilityAbilities.FlightVerticalSpeedMultiplier),
                FlightAccelerationMultiplier = Math.Max(1f, MobilityAbilities.FlightAccelerationMultiplier),
                GlideEnabled = true,
                GlideGravityScale = Math.Min(0.18f, MobilityAbilities.GlideGravityScale),
                GlideTerminalVelocity = MobilityAbilities.GlideTerminalVelocity > 0f
                    ? Math.Min(125f, MobilityAbilities.GlideTerminalVelocity)
                    : 125f
            }
            : MobilityAbilities;
        _movementController.ApplyIntent(
            Body,
            new SideViewCharacterInput(
                _command.MoveAxis,
                _command.WantsJump,
                _command.WantsFly,
                _command.WantsGlide,
                Stats.CanDoubleJump || IsDeveloperFreeFlight,
                Stats.CanWallJump,
                Stats.CanFly || IsDeveloperFreeFlight,
                Stats.CanGlide || IsDeveloperFreeFlight),
            mobility,
            deltaSeconds,
            Stats.MovementSpeedMultiplier * DeveloperMovementSpeedMultiplier,
            _lastContactFlags);
        _physicsWorld.Step(world, _physicsBodies, deltaSeconds, _physicsResults, _physicsContacts);
        if (_physicsResults.Length > 0)
        {
            _lastContactFlags = _physicsResults[0].ContactFlags;
        }
        Position = Body.Position;
    }
}
