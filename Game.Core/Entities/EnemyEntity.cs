using Game.Core.Combat;
using Game.Core.Entities.AI;
using Game.Core.Effects;
using Game.Core.Physics;
using Game.Core.Loot;
using Game.Core.Spawning;
using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class EnemyEntity : Entity, IEntityPhysicsParticipant
{
    private const float MaxFallSpeed = 620f;

    private readonly TileCollisionResolver _standaloneCollisionResolver;
    private readonly IAiBehavior _aiBehavior;
    private PhysicsWorld? _standalonePhysicsWorld;
    private double _deferredAiSeconds;
    private long _scheduledAiEpoch;
    private long _lastAiDecisionStep = -1;

    public EnemyEntity(
        string definitionId,
        Vector2 spawnPosition,
        Vector2 size,
        HealthComponent health,
        IAiBehavior aiBehavior,
        TileCollisionResolver collisionResolver,
        string? lootTableId = null,
        int contactDamage = 10,
        float contactKnockback = 180f,
        int? attackDamage = null,
        float? attackKnockback = null,
        EntityDespawnPolicyDefinition? despawnPolicy = null,
        IReadOnlyList<StatusEffectApplication>? onContactEffects = null,
        EntityFaction faction = EntityFaction.Hostile,
        EntityMovementMode movementMode = EntityMovementMode.Ground,
        IReadOnlyList<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);

        DefinitionId = definitionId;
        Health = health;
        LootTableId = lootTableId;
        ContactDamage = Math.Max(0, contactDamage);
        ContactKnockback = Math.Max(0, contactKnockback);
        AttackDamage = Math.Max(0, attackDamage ?? ContactDamage);
        AttackKnockback = Math.Max(0, attackKnockback ?? ContactKnockback);
        DespawnPolicy = despawnPolicy ?? new EntityDespawnPolicyDefinition();
        _despawnProtectionRemaining = DespawnPolicy.SpawnProtectionSeconds;
        OnContactEffects = onContactEffects ?? Array.Empty<StatusEffectApplication>();
        Faction = faction;
        MovementMode = movementMode;
        Tags = tags ?? Array.Empty<string>();
        _aiBehavior = aiBehavior ?? throw new ArgumentNullException(nameof(aiBehavior));
        ArgumentNullException.ThrowIfNull(collisionResolver);
        _standaloneCollisionResolver = collisionResolver;
        CollisionSettings = collisionResolver.Settings;
        Body = new PhysicsBody
        {
            Position = spawnPosition,
            Size = size,
            BodyType = movementMode == EntityMovementMode.Ground
                ? PhysicsBodyType.Dynamic
                : PhysicsBodyType.Kinematic,
            CollisionLayer = PhysicsCollisionLayer.Enemy,
            GravityScale = movementMode == EntityMovementMode.Ground ? 1f : 0f,
            MaximumAbsoluteVelocity = new Vector2(float.PositiveInfinity, MaxFallSpeed)
        };
        Position = spawnPosition;
    }

    public string DefinitionId { get; }

    public string? LootTableId { get; }

    public EntityFaction Faction { get; }

    public EntityMovementMode MovementMode { get; }

    public IReadOnlyList<string> Tags { get; }

    public AiState AiState => _aiBehavior.CurrentState;

    public AiTelemetrySnapshot AiTelemetry => _aiBehavior.Telemetry;

    public int? TargetEntityId => _aiBehavior.TargetEntityId;

    public string? SpawnRuleId { get; private set; }

    public string? SpawnEncounterId { get; private set; }

    public string? SpawnGroup { get; private set; }

    public SpawnRegionKey? SpawnRegion { get; private set; }

    public SpawnHabitat? SpawnHabitat { get; private set; }

    public PhysicsBody Body { get; }

    TileCollisionSettings IEntityPhysicsParticipant.CollisionSettings => CollisionSettings;

    internal TileCollisionSettings CollisionSettings { get; }

    public HealthComponent Health { get; }

    public StatusEffectCollection StatusEffects { get; } = new();

    public int ContactDamage { get; }

    public float ContactKnockback { get; }

    public int AttackDamage { get; }

    public float AttackKnockback { get; }

    public EntityDespawnPolicyDefinition DespawnPolicy { get; }

    public float DespawnProtectionRemaining => _despawnProtectionRemaining;

    public IReadOnlyList<StatusEffectApplication> OnContactEffects { get; }

    public DamageInfo? LastDamage { get; private set; }

    private float _despawnProtectionRemaining;

    public override RectI Bounds => Body.Bounds;

    public bool ApplyDamage(DamageInfo damage)
    {
        var applied = Health.ApplyDamage(damage);
        if (applied)
        {
            LastDamage = damage;
            if (damage.KnockbackForce > 0 && damage.KnockbackDirection != Vector2.Zero)
            {
                Body.ApplyKnockback(damage.KnockbackDirection, damage.KnockbackForce);
            }

            _despawnProtectionRemaining = Math.Max(
                _despawnProtectionRemaining,
                DespawnPolicy.DamageProtectionSeconds);
        }

        return applied;
    }

    public LootKillContext CreateLootKillContext(
        EntityFaction? killerFaction = null,
        bool? isNight = null,
        int? victimDepth = null)
    {
        return new LootKillContext(
            LastDamage?.SourceEntityId,
            killerFaction,
            LastDamage?.Type,
            isNight,
            victimDepth,
            Tags);
    }

    public EntityDisposition GetDispositionToward(Entity other)
    {
        return EntityFactionResolver.GetDisposition(Faction, EntityFactionResolver.GetFaction(other));
    }

    public bool HasTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return Tags.Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
    }

    public void AssignSpawnMetadata(string ruleId, string? group)
    {
        AssignSpawnMetadata(ruleId, group, null, null);
    }

    public void AssignSpawnMetadata(
        string ruleId,
        string? group,
        SpawnRegionKey? region,
        SpawnHabitat? habitat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        SpawnRuleId = ruleId;
        SpawnGroup = string.IsNullOrWhiteSpace(group) ? null : group;
        SpawnRegion = region;
        SpawnHabitat = habitat;
    }

    internal void AssignSpawnEncounter(string encounterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        SpawnEncounterId = encounterId;
    }

    public bool TryConsumeAttackIntent(out AiAttackIntent intent)
    {
        return _aiBehavior.TryConsumeAttackIntent(out intent);
    }

    public bool CanDespawnSafely()
    {
        if (DespawnPolicy.Mode == EntityDespawnMode.Never ||
            _despawnProtectionRemaining > 0 ||
            TargetEntityId is not null)
        {
            return false;
        }

        return DespawnPolicy.Mode != EntityDespawnMode.WhenIdle ||
               AiState is AiState.Idle or AiState.Wander or AiState.Patrol;
    }

    public override void Update(GameWorld world, float deltaSeconds)
    {
        Update(AiUpdateContext.WithoutEntities(world), deltaSeconds);
    }

    public void Update(AiUpdateContext context, float deltaSeconds)
    {
        if (!PreparePhysicsUpdate(context, deltaSeconds))
        {
            return;
        }

        GetStandalonePhysicsWorld().StepBody(
            context.World,
            Body,
            deltaSeconds,
            Span<PhysicsContact>.Empty);
        SynchronizePhysicsState();
    }

    internal bool PreparePhysicsUpdate(AiUpdateContext context, float deltaSeconds)
    {
        if (!PrepareAuthoritativeState(deltaSeconds))
        {
            return false;
        }

        _aiBehavior.Update(this, context, deltaSeconds);
        return true;
    }

    internal bool PreparePhysicsUpdate(
        AiUpdateContext context,
        float deltaSeconds,
        in EntityAiSchedule schedule)
    {
        if (!PrepareAuthoritativeState(deltaSeconds))
        {
            return false;
        }

        var elapsed = Math.Max(0f, deltaSeconds);
        if (_scheduledAiEpoch == schedule.Epoch)
        {
            var decisionDelta = Math.Min(float.MaxValue, elapsed + _deferredAiSeconds);
            _deferredAiSeconds = 0d;
            _aiBehavior.Update(this, context, (float)decisionDelta);
            _lastAiDecisionStep = schedule.DecisionStep;
        }
        else
        {
            _deferredAiSeconds += elapsed;
            // Behavior state and the body's current velocity are the cached intent. Do not
            // reapply an older velocity here: physics contacts and external impulses remain
            // authoritative on every skipped decision tick.
        }

        return true;
    }

    internal void ScheduleAiDecision(long epoch)
    {
        _scheduledAiEpoch = epoch;
    }

    internal int GetAiDecisionAge(long decisionStep)
    {
        if (_lastAiDecisionStep < 0)
        {
            return int.MaxValue;
        }

        return (int)Math.Min(int.MaxValue, Math.Max(0, decisionStep - _lastAiDecisionStep));
    }

    internal void ResetAiSchedulingState()
    {
        _deferredAiSeconds = 0d;
        _scheduledAiEpoch = 0;
        _lastAiDecisionStep = -1;
    }

    void IEntityPhysicsParticipant.SynchronizePhysicsState(
        GameWorld world,
        in PhysicsMoveResult moveResult,
        ReadOnlySpan<PhysicsContact> tileContacts)
    {
        SynchronizePhysicsState();
    }

    internal void SynchronizePhysicsState()
    {
        Position = Body.Position;
    }

    private bool PrepareAuthoritativeState(float deltaSeconds)
    {
        _despawnProtectionRemaining = Math.Max(0, _despawnProtectionRemaining - Math.Max(0, deltaSeconds));
        StatusEffects.Update(deltaSeconds, Health);
        Health.Update(deltaSeconds);
        if (!Health.IsDead)
        {
            return true;
        }

        IsActive = false;
        return false;
    }

    private PhysicsWorld GetStandalonePhysicsWorld()
    {
        return _standalonePhysicsWorld ??= new PhysicsWorld(
            _standaloneCollisionResolver,
            EntityPhysicsRuntime.CreateSettings(EntityPhysicsRuntime.DefaultMaximumBodies));
    }
}
