using Game.Core.Entities.AI.Sensing;
using System.Numerics;

namespace Game.Core.Entities.AI;

public sealed class CritterAiBehavior : IAiBehavior
{
    private readonly AiProfileDefinition _profile;
    private readonly DistanceSensor _distance = new();
    private readonly LineOfSightSensor _lineOfSight = new();
    private readonly AiSteering _steering = new();
    private readonly AiPerceptionMemory _memory = new();
    private AiDecisionSequence _decisions;
    private Vector2? _home;
    private float _decisionRemaining;
    private int _wanderDirection = 1;
    private bool _targetVisible;
    private bool _activePeriod = true;
    private int _nearbyAllies;
    private long _updateCount;
    private long _decisionCount;
    private long _stateTransitions;

    public CritterAiBehavior(AiProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
    }

    public AiState CurrentState { get; private set; } = AiState.Idle;

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
        _activePeriod = IsActivePeriod(context.IsNight);
        _targetVisible = false;
        _nearbyAllies = 0;
        var nearbyEntities = context.QueryNeighborhood(
            entity.Body.Center,
            Math.Max(_profile.DetectionRange, _profile.FlockRadius));

        if (TryResolveThreat(entity, context, nearbyEntities, out var threatPosition))
        {
            SetState(AiState.Flee);
            Flee(entity, threatPosition, context);
            return;
        }

        if (!_activePeriod)
        {
            Inactive(entity, context);
            return;
        }

        if (ShouldReturnHome(entity))
        {
            ReturnHome(entity, context);
            return;
        }

        _decisionRemaining -= elapsed;
        if (_decisionRemaining <= 0)
        {
            _decisionRemaining = _profile.DecisionInterval;
            _decisionCount++;
            if (_decisions.NextUnit(entity.Id) < _profile.IdleChance)
            {
                SetState(AiState.Idle);
            }
            else
            {
                SetState(AiState.Wander);
                _wanderDirection = _decisions.NextUnit(entity.Id) < 0.5f ? -1 : 1;
            }
        }

        if (CurrentState == AiState.Idle)
        {
            _steering.Move(entity, context, Vector2.Zero, 0, _profile);
            return;
        }

        var speed = ResolveMoveSpeed(context.IsNight);
        var vertical = entity.MovementMode == EntityMovementMode.Flying
            ? (_decisions.NextUnit(entity.Id) - 0.5f) * 0.45f
            : 0;
        var direction = new Vector2(_wanderDirection, vertical);
        if (TryResolveFlock(entity, nearbyEntities, out var flockDirection))
        {
            SetState(AiState.Flock);
            direction = Vector2.Lerp(direction, flockDirection, Math.Clamp(_profile.FlockWeight, 0f, 1f));
        }
        else if (CurrentState == AiState.Flock)
        {
            SetState(AiState.Wander);
        }

        _steering.Move(entity, context, direction, speed, _profile);
        if (entity.MovementMode == EntityMovementMode.Ground && Math.Sign(entity.Body.Velocity.X) != 0)
        {
            _wanderDirection = Math.Sign(entity.Body.Velocity.X);
        }
    }

    public bool TryConsumeAttackIntent(out AiAttackIntent intent)
    {
        intent = default;
        return false;
    }

    private bool TryResolveThreat(
        EnemyEntity entity,
        AiUpdateContext context,
        IReadOnlyList<Entity> nearbyEntities,
        out Vector2 position)
    {
        var visible = _distance.FindNearestHostile(entity, nearbyEntities, _profile.DetectionRange);
        if (visible is not null && CanSense(entity, visible, context))
        {
            _memory.Observe(visible, _profile.PerceptionMemorySeconds);
            _targetVisible = true;
            position = DistanceSensor.GetCenter(visible);
            return true;
        }

        if (_memory.IsActive)
        {
            position = _memory.LastKnownPosition;
            return true;
        }

        _memory.Clear();
        position = default;
        return false;
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
        direction = center - entity.Body.Center;
        return direction != Vector2.Zero;
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

    private bool CanSense(EnemyEntity entity, Entity threat, AiUpdateContext context)
    {
        return !_profile.RequiresLineOfSight ||
               _lineOfSight.HasLineOfSight(context.World, entity.Body.Center, DistanceSensor.GetCenter(threat));
    }

    private void Flee(EnemyEntity entity, Vector2 threatPosition, AiUpdateContext context)
    {
        var away = entity.Body.Center - threatPosition;
        if (away == Vector2.Zero)
        {
            away = Vector2.UnitX;
        }

        _steering.Move(entity, context, away, _profile.FleeSpeed, _profile);
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
}
