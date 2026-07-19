using Game.Core.Entities.AI.Sensing;
using System.Numerics;

namespace Game.Core.Entities.AI;

public sealed class HostileAiBehavior : IAiBehavior
{
    private readonly AiProfileDefinition _profile;
    private readonly DistanceSensor _distance = new();
    private readonly LineOfSightSensor _lineOfSight = new();
    private readonly AiSteering _steering = new();
    private readonly AiPerceptionMemory _memory = new();
    private AiDecisionSequence _decisions;
    private Vector2? _home;
    private float _decisionRemaining;
    private float _attackCooldownRemaining;
    private int _patrolDirection = 1;
    private AiAttackIntent? _pendingAttack;
    private bool _targetVisible;
    private bool _activePeriod = true;
    private int _nearbyAllies;
    private long _updateCount;
    private long _decisionCount;
    private long _stateTransitions;

    public HostileAiBehavior(AiProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
    }

    public AiState CurrentState { get; private set; } = AiState.Patrol;

    public int? TargetEntityId => _memory.TargetEntityId;

    public AiTelemetrySnapshot Telemetry => new(
        CurrentState,
        TargetEntityId,
        _home ?? default,
        _memory.LastKnownPosition,
        _memory.RemainingSeconds,
        _targetVisible,
        _activePeriod,
        _nearbyAllies,
        _updateCount,
        _decisionCount,
        _stateTransitions);

    public void Update(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
    {
        _home ??= entity.Body.Position;
        _updateCount++;
        var elapsed = Math.Max(0, deltaSeconds);
        _memory.Advance(elapsed);
        _attackCooldownRemaining = Math.Max(0, _attackCooldownRemaining - elapsed);
        _activePeriod = IsActivePeriod(context.IsNight);
        _targetVisible = false;
        _nearbyAllies = 0;
        var nearbyEntities = context.QueryNeighborhood(
            entity.Body.Center,
            Math.Max(_profile.DetectionRange, _profile.FlockRadius));

        var target = _activePeriod
            ? ResolveTarget(entity, context, nearbyEntities)
            : ResolveRememberedTarget(entity, context);
        if (target is null)
        {
            if (!_activePeriod)
            {
                Inactive(entity, context);
            }
            else if (ShouldReturnHome(entity))
            {
                ReturnHome(entity, context);
            }
            else if (TryResolveFlock(entity, nearbyEntities, out var flockDirection))
            {
                SetState(AiState.Flock);
                _steering.Move(
                    entity,
                    context,
                    flockDirection,
                    ResolveMoveSpeed(context.IsNight),
                    _profile);
            }
            else
            {
                Patrol(entity, context, elapsed);
            }

            return;
        }

        _targetVisible = target.Value.IsVisible;
        var offset = target.Value.Position - entity.Body.Center;
        var healthRatio = entity.Health.Current / (float)entity.Health.Max;
        if (_profile.FleeHealthThreshold > 0 && healthRatio <= _profile.FleeHealthThreshold)
        {
            SetState(AiState.Flee);
            _steering.Move(entity, context, -offset, _profile.FleeSpeed, _profile);
            return;
        }

        if (!target.Value.IsVisible)
        {
            SetState(AiState.Investigate);
            if (offset.LengthSquared() <= Math.Max(4f, entity.Body.Size.X * 0.5f) *
                Math.Max(4f, entity.Body.Size.X * 0.5f))
            {
                _memory.Clear();
                ReturnHome(entity, context);
                return;
            }

            _steering.Move(
                entity,
                context,
                offset,
                ResolveMoveSpeed(context.IsNight),
                _profile);
            return;
        }

        if (offset.LengthSquared() <= _profile.AttackRange * _profile.AttackRange)
        {
            SetState(AiState.Attack);
            _steering.Move(entity, context, Vector2.Zero, 0, _profile);
            if (_attackCooldownRemaining <= 0 && _pendingAttack is null)
            {
                _pendingAttack = new AiAttackIntent(
                    entity.Id,
                    target.Value.EntityId,
                    entity.AttackDamage,
                    _profile.AttackRange,
                    entity.AttackKnockback);
                _attackCooldownRemaining = _profile.AttackCooldown;
            }

            return;
        }

        SetState(AiState.Chase);
        _steering.Move(
            entity,
            context,
            offset,
            ResolveMoveSpeed(context.IsNight),
            _profile);
    }

    public bool TryConsumeAttackIntent(out AiAttackIntent intent)
    {
        if (_pendingAttack is null)
        {
            intent = default;
            return false;
        }

        intent = _pendingAttack.Value;
        _pendingAttack = null;
        return true;
    }

    private PerceivedTarget? ResolveTarget(
        EnemyEntity entity,
        AiUpdateContext context,
        IReadOnlyList<Entity> nearbyEntities)
    {
        var remembered = ResolveRememberedTarget(entity, context);
        if (remembered is { IsVisible: true })
        {
            return remembered;
        }

        var candidate = _distance.FindNearestHostile(entity, nearbyEntities, _profile.DetectionRange);
        if (context.Player is { IsActive: true } player &&
            entity.GetDispositionToward(player) == EntityDisposition.Hostile &&
            Vector2.DistanceSquared(entity.Body.Center, player.Body.Center) <=
            _profile.DetectionRange * _profile.DetectionRange &&
            (candidate is null ||
             Vector2.DistanceSquared(entity.Body.Center, player.Body.Center) <
             Vector2.DistanceSquared(entity.Body.Center, DistanceSensor.GetCenter(candidate))))
        {
            candidate = player;
        }

        if (candidate is not null && CanSee(entity, candidate, context))
        {
            _memory.Observe(candidate, _profile.PerceptionMemorySeconds);
            return new PerceivedTarget(candidate.Id, DistanceSensor.GetCenter(candidate), true);
        }

        return remembered;
    }

    private PerceivedTarget? ResolveRememberedTarget(EnemyEntity entity, AiUpdateContext context)
    {
        if (_memory.TargetEntityId is int rememberedId &&
            context.FindEntity(rememberedId) is { } remembered &&
            entity.GetDispositionToward(remembered) == EntityDisposition.Hostile &&
            Vector2.DistanceSquared(entity.Body.Center, DistanceSensor.GetCenter(remembered)) <=
            _profile.LoseTargetRange * _profile.LoseTargetRange &&
            CanSee(entity, remembered, context))
        {
            _memory.Observe(remembered, _profile.PerceptionMemorySeconds);
            return new PerceivedTarget(remembered.Id, DistanceSensor.GetCenter(remembered), true);
        }

        if (_memory.IsActive && _memory.TargetEntityId is int targetId)
        {
            return new PerceivedTarget(targetId, _memory.LastKnownPosition, false);
        }

        _memory.Clear();
        return null;
    }

    private bool TryResolveFlock(
        EnemyEntity entity,
        IReadOnlyList<Entity> nearbyEntities,
        out Vector2 direction)
    {
        direction = default;
        if (_profile.FlockRadius <= 0 || _profile.FlockWeight <= 0)
        {
            return false;
        }

        var radiusSquared = _profile.FlockRadius * _profile.FlockRadius;
        var center = Vector2.Zero;
        for (var index = 0; index < nearbyEntities.Count; index++)
        {
            if (nearbyEntities[index] is not EnemyEntity { IsActive: true } ally ||
                ally.Id == entity.Id ||
                ally.Faction != entity.Faction ||
                !string.Equals(ally.DefinitionId, entity.DefinitionId, StringComparison.OrdinalIgnoreCase) ||
                Vector2.DistanceSquared(entity.Body.Center, ally.Body.Center) > radiusSquared)
            {
                continue;
            }

            center += ally.Body.Center;
            _nearbyAllies++;
        }

        if (_nearbyAllies == 0 || _nearbyAllies + 1 < _profile.MinFlockSize)
        {
            return false;
        }

        center /= _nearbyAllies;
        direction = Vector2.Lerp(
            new Vector2(_patrolDirection, 0),
            center - entity.Body.Center,
            Math.Clamp(_profile.FlockWeight, 0f, 1f));
        return direction != Vector2.Zero;
    }

    private bool CanSee(EnemyEntity entity, Entity target, AiUpdateContext context)
    {
        return !_profile.RequiresLineOfSight ||
               _lineOfSight.HasLineOfSight(context.World, entity.Body.Center, DistanceSensor.GetCenter(target));
    }

    private void Patrol(EnemyEntity entity, AiUpdateContext context, float deltaSeconds)
    {
        SetState(AiState.Patrol);
        _decisionRemaining -= deltaSeconds;
        var offsetFromHome = entity.Body.Position.X - _home!.Value.X;
        if (Math.Abs(offsetFromHome) >= _profile.PatrolRadius)
        {
            _patrolDirection = offsetFromHome > 0 ? -1 : 1;
            _decisionRemaining = _profile.DecisionInterval;
        }
        else if (_decisionRemaining <= 0)
        {
            _decisionRemaining = _profile.DecisionInterval;
            _decisionCount++;
            _patrolDirection = _decisions.NextUnit(entity.Id) < 0.5f ? -1 : 1;
        }

        _steering.Move(
            entity,
            context,
            new Vector2(_patrolDirection, 0.15f),
            ResolveMoveSpeed(context.IsNight),
            _profile);
        if (entity.MovementMode == EntityMovementMode.Ground && Math.Sign(entity.Body.Velocity.X) != 0)
        {
            _patrolDirection = Math.Sign(entity.Body.Velocity.X);
        }
    }

    private void Inactive(EnemyEntity entity, AiUpdateContext context)
    {
        if (ShouldReturnHome(entity))
        {
            ReturnHome(entity, context);
            return;
        }

        if (_profile.PerchWhenInactive && entity.MovementMode == EntityMovementMode.Flying)
        {
            SetState(AiState.Perch);
            var direction = entity.Body.OnGround ? Vector2.Zero : Vector2.UnitY;
            _steering.Move(entity, context, direction, ResolveMoveSpeed(context.IsNight) * 0.2f, _profile);
            return;
        }

        SetState(AiState.Idle);
        _steering.Move(entity, context, Vector2.Zero, 0, _profile);
    }

    private bool ShouldReturnHome(EnemyEntity entity)
    {
        var radius = _profile.ReturnHomeDistance > 0
            ? _profile.ReturnHomeDistance
            : _profile.PatrolRadius;
        return radius > 0 &&
               Vector2.DistanceSquared(entity.Body.Position, _home!.Value) > radius * radius;
    }

    private void ReturnHome(EnemyEntity entity, AiUpdateContext context)
    {
        SetState(AiState.ReturnHome);
        _steering.Move(
            entity,
            context,
            _home!.Value - entity.Body.Position,
            ResolveMoveSpeed(context.IsNight),
            _profile);
    }

    private bool IsActivePeriod(bool isNight)
    {
        return _profile.ActivityPeriod == AiActivityPeriod.Any ||
               (_profile.ActivityPeriod == AiActivityPeriod.Night) == isNight;
    }

    private float ResolveMoveSpeed(bool isNight)
    {
        return _profile.MoveSpeed *
               (isNight ? _profile.NightMoveSpeedMultiplier : _profile.DayMoveSpeedMultiplier);
    }

    private void SetState(AiState state)
    {
        if (CurrentState != state)
        {
            CurrentState = state;
            _stateTransitions++;
        }
    }

    private readonly record struct PerceivedTarget(int EntityId, Vector2 Position, bool IsVisible);
}
