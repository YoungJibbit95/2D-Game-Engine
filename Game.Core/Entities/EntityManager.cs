using Game.Core.World;
using Game.Core.Entities.AI;
using Game.Core.Entities.AI.Sensing;
using Game.Core.Projectiles;
using Game.Core.Physics;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class EntityManager
{
    private const int MaximumHomingQueriesPerUpdate = 256;
    private const int MaximumHomingCandidatesPerProjectile = 128;
    private const int MaximumHomingEntryTestsPerProjectile = 512;
    private const int MaximumHomingTargetsPerProjectile = MaximumHomingCandidatesPerProjectile + 1;

    private readonly List<Entity> _entities = new();
    private readonly Dictionary<int, Entity> _entitiesById = new();
    private readonly EntitySpatialIndex _spatialGrid;
    private readonly List<Entity> _aiQueryResults = new();
    private readonly HashSet<Entity> _aiQuerySeen = new();
    private readonly EntityQueryWorkspace _projectileTargetQuery = new(initialCapacity: 32);
    private readonly List<ProjectileHomingTarget> _projectileTargets = new(32);
    private readonly PhysicsWorld _physicsWorld;
    private readonly TileCollisionSettings _physicsCollisionSettings;
    private readonly EntityAiDecisionScheduler _aiDecisionScheduler;
    private PhysicsBody[] _physicsBodies = Array.Empty<PhysicsBody>();
    private IEntityPhysicsParticipant[] _physicsParticipants = Array.Empty<IEntityPhysicsParticipant>();
    private PhysicsMoveResult[] _physicsResults = Array.Empty<PhysicsMoveResult>();
    private PhysicsContact[] _physicsContacts = Array.Empty<PhysicsContact>();
    private int[] _physicsSortedBodyIndices = Array.Empty<int>();
    private PhysicsBodyPair[] _physicsBodyPairs = Array.Empty<PhysicsBodyPair>();
    private PhysicsBodyContact[] _physicsBodyContacts = Array.Empty<PhysicsBodyContact>();
    private PhysicsContinuousBodyContact[] _physicsContinuousBodyContacts = Array.Empty<PhysicsContinuousBodyContact>();
    private PhysicsContinuousContactCandidate[] _physicsContinuousContactCandidates = Array.Empty<PhysicsContinuousContactCandidate>();
    private PhysicsBodySweepState[] _physicsBodySweepStates = Array.Empty<PhysicsBodySweepState>();
    private readonly int _maximumPhysicsBodyPairs;
    private int _nextEntityId = 1;
    private int _nextHomingProjectileOrdinal;
    private int _homingProjectileEntityCount;
    private int _physicsEntityCount;

    public int HomingQueriesPreparedLastUpdate { get; private set; }

    public int HomingQueriesDeferredLastUpdate { get; private set; }

    public int HomingCandidateQueriesTruncatedLastUpdate { get; private set; }

    public PhysicsStepTelemetry PhysicsTelemetryLastUpdate { get; private set; }

    public EntityAiSchedulingTelemetry AiSchedulingTelemetryLastUpdate { get; private set; }

    public EntityAiSchedulingOptions AiSchedulingOptions => _aiDecisionScheduler.Options;

    internal EntitySpatialIndexTelemetry SpatialIndexTelemetry => _spatialGrid.Telemetry;

    public EntityManager(
        int spatialCellSize = GameConstants.PixelsPerChunk,
        TileCollisionResolver? physicsCollisionResolver = null,
        int maximumPhysicsBodies = EntityPhysicsRuntime.DefaultMaximumBodies,
        int maximumPhysicsBodyPairs = EntityPhysicsRuntime.DefaultMaximumBodyPairs,
        EntityAiSchedulingOptions? aiSchedulingOptions = null)
    {
        if (maximumPhysicsBodyPairs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPhysicsBodyPairs));
        }

        _spatialGrid = new EntitySpatialIndex(spatialCellSize);
        var collisionResolver = physicsCollisionResolver ?? new TileCollisionResolver();
        _physicsCollisionSettings = collisionResolver.Settings;
        _maximumPhysicsBodyPairs = maximumPhysicsBodyPairs;
        _physicsWorld = new PhysicsWorld(
            collisionResolver,
            EntityPhysicsRuntime.CreateSettings(maximumPhysicsBodies),
            EntityPhysicsRuntime.CreateBroadphaseSettings(
                maximumPhysicsBodies,
                maximumPhysicsBodyPairs));
        _aiDecisionScheduler = new EntityAiDecisionScheduler(maximumPhysicsBodies, aiSchedulingOptions);
    }

    public IReadOnlyList<Entity> Entities => _entities;

    public void Add(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var physicsParticipant = entity as IEntityPhysicsParticipant;
        if (physicsParticipant is not null)
        {
            if (physicsParticipant.CollisionSettings != _physicsCollisionSettings)
            {
                throw new InvalidOperationException(
                    "Physics entities and their EntityManager must use identical tile-collision settings.");
            }

            if (_physicsEntityCount >= _physicsWorld.Settings.MaximumBodiesPerStep)
            {
                throw new InvalidOperationException(
                    $"Entity physics capacity {_physicsWorld.Settings.MaximumBodiesPerStep} is exhausted. " +
                    "Increase the explicit capacity; authoritative bodies are never deferred.");
            }

            EnsurePhysicsCapacity(_physicsEntityCount + 1);
        }

        if (entity.Id == 0)
        {
            entity.AssignId(_nextEntityId++);
        }
        else
        {
            _nextEntityId = Math.Max(_nextEntityId, entity.Id + 1);
        }

        if (_entitiesById.ContainsKey(entity.Id))
        {
            throw new InvalidOperationException($"Entity id {entity.Id} is already registered.");
        }

        if (physicsParticipant is not null)
        {
            physicsParticipant.Body.DeterministicOrder = entity.Id;
        }

        var bounds = entity.Bounds;
        if (bounds.IsEmpty)
        {
            throw new ArgumentException("Entity spatial bounds must not be empty.", nameof(entity));
        }

        _spatialGrid.Insert(entity, bounds);
        try
        {
            _entitiesById.Add(entity.Id, entity);
            _entities.Add(entity);
            if (entity is EnemyEntity addedActor)
            {
                addedActor.ResetAiSchedulingState();
            }

            if (entity is ProjectileEntity projectile && IsHomingCapable(projectile))
            {
                _homingProjectileEntityCount++;
            }

            if (physicsParticipant is not null)
            {
                _physicsEntityCount++;
            }
        }
        catch
        {
            _entitiesById.Remove(entity.Id);
            _spatialGrid.Remove(entity);
            throw;
        }
    }

    public void Remove(Entity entity)
    {
        if (!_entities.Remove(entity))
        {
            return;
        }

        _entitiesById.Remove(entity.Id);
        _spatialGrid.Remove(entity);
        if (entity is ProjectileEntity projectile && IsHomingCapable(projectile))
        {
            _homingProjectileEntityCount--;
        }
        if (entity is IEntityPhysicsParticipant)
        {
            _physicsEntityCount--;
        }
        if (entity is EnemyEntity removedActor)
        {
            removedActor.ResetAiSchedulingState();
        }
    }

    public void UpdateAll(GameWorld world, float deltaSeconds)
    {
        UpdateAll(world, deltaSeconds, player: null);
    }

    public void UpdateAll(GameWorld world, float deltaSeconds, PlayerEntity? player)
    {
        UpdateAll(world, deltaSeconds, player, isNight: false, tickNumber: 0);
    }

    public void UpdateAll(
        GameWorld world,
        float deltaSeconds,
        PlayerEntity? player,
        bool isNight,
        long tickNumber)
    {
        var aiContext = new AiUpdateContext(world, _entities, player, isNight, tickNumber, this);
        var aiSchedule = _aiDecisionScheduler.Schedule(_entities, player, tickNumber);
        var homingProjectileCount = CountActiveHomingProjectiles();
        var homingQueryBudget = Math.Min(homingProjectileCount, MaximumHomingQueriesPerUpdate);
        var firstHomingOrdinal = homingProjectileCount == 0
            ? 0
            : _nextHomingProjectileOrdinal % homingProjectileCount;
        var homingOrdinal = 0;
        HomingQueriesPreparedLastUpdate = 0;
        HomingQueriesDeferredLastUpdate = homingProjectileCount - homingQueryBudget;
        HomingCandidateQueriesTruncatedLastUpdate = 0;
        var physicsBodyCount = 0;

        for (var index = 0; index < _entities.Count; index++)
        {
            var entity = _entities[index];
            if (entity is EnemyEntity { IsActive: true } actor)
            {
                if (actor.PreparePhysicsUpdate(
                        aiContext,
                        deltaSeconds,
                        aiSchedule))
                {
                    QueuePhysicsBody(actor, ref physicsBodyCount);
                }
            }
            else if (entity is DroppedItemEntity { IsActive: true } droppedItem)
            {
                droppedItem.PreparePhysicsUpdate(deltaSeconds);
                QueuePhysicsBody(droppedItem, ref physicsBodyCount);
            }
            else if (entity is ProjectileEntity { IsActive: true } projectile)
            {
                if (RequiresHoming(projectile))
                {
                    var offset = (homingOrdinal - firstHomingOrdinal + homingProjectileCount) % homingProjectileCount;
                    if (offset < homingQueryBudget)
                    {
                        PrepareProjectileHomingTargets(projectile, player);
                        HomingQueriesPreparedLastUpdate++;
                    }

                    homingOrdinal++;
                }

                if (projectile.PreparePhysicsUpdate(deltaSeconds))
                {
                    QueuePhysicsBody(projectile, ref physicsBodyCount);
                }
            }
            else if (entity.IsActive)
            {
                entity.Update(world, deltaSeconds);
            }
        }

        AiSchedulingTelemetryLastUpdate = aiSchedule.Telemetry with
        {
            PhysicsBodiesSubmitted = physicsBodyCount
        };
        StepEntityPhysics(world, deltaSeconds, physicsBodyCount);

        for (var index = _entities.Count - 1; index >= 0; index--)
        {
            if (!_entities[index].IsActive &&
                (_entities[index] is not ProjectileEntity projectile ||
                 !projectile.HasPendingTileCollisionResult))
            {
                RemoveAt(index);
            }
        }

        for (var index = 0; index < _entities.Count; index++)
        {
            _spatialGrid.Update(_entities[index], _entities[index].Bounds);
        }

        _nextHomingProjectileOrdinal = homingProjectileCount == 0
            ? 0
            : (firstHomingOrdinal + homingQueryBudget) % homingProjectileCount;
    }

    public IReadOnlyList<Entity> Query(RectI area)
    {
        return _spatialGrid.Query(area);
    }

    public void QueryInto(RectI area, List<Entity> result, HashSet<Entity> seen)
    {
        _spatialGrid.QueryInto(area, result, seen);
    }

    public void QueryInto(RectI area, EntityQueryWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _spatialGrid.QueryInto(area, workspace.Results, workspace.Seen);
        workspace.RecordQuery();
    }

    public bool QueryIntoBounded(RectI area, EntityQueryWorkspace workspace, int maximumResults)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var truncated = _spatialGrid.QueryInto(area, workspace.Results, workspace.Seen, maximumResults);
        workspace.RecordQuery(truncated);
        return truncated;
    }

    internal bool QueryNearestIntoBounded(
        RectI area,
        Vector2 origin,
        EntityQueryWorkspace workspace,
        int maximumResults,
        int maximumEntryTests,
        EntitySpatialQueryKinds kinds = EntitySpatialQueryKinds.All)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var truncated = _spatialGrid.QueryNearestInto(
            area,
            origin,
            workspace.Results,
            workspace.Scores,
            maximumResults,
            maximumEntryTests,
            kinds);
        workspace.RecordQuery(truncated);
        return truncated;
    }

    internal Entity? FindActiveEntity(int id)
    {
        return _entitiesById.TryGetValue(id, out var entity) && entity.IsActive ? entity : null;
    }

    internal IReadOnlyList<Entity> QueryAiNeighborhood(Vector2 center, float radius)
    {
        var left = Saturate(Math.Floor(center.X - radius));
        var top = Saturate(Math.Floor(center.Y - radius));
        var right = Saturate(Math.Ceiling(center.X + radius));
        var bottom = Saturate(Math.Ceiling(center.Y + radius));
        _spatialGrid.QueryInto(
            RectI.FromInclusiveTileBounds(left, top, right, bottom),
            _aiQueryResults,
            _aiQuerySeen);
        return _aiQueryResults;
    }

    internal bool IntersectsActive(RectI bounds)
    {
        _spatialGrid.QueryInto(bounds, _aiQueryResults, _aiQuerySeen);
        for (var index = 0; index < _aiQueryResults.Count; index++)
        {
            if (_aiQueryResults[index].IsActive && _aiQueryResults[index].Bounds.Intersects(bounds))
            {
                return true;
            }
        }

        return false;
    }

    internal void RemoveAt(int index)
    {
        var entity = _entities[index];
        _entities.RemoveAt(index);
        _entitiesById.Remove(entity.Id);
        _spatialGrid.Remove(entity);
        if (entity is ProjectileEntity projectile && IsHomingCapable(projectile))
        {
            _homingProjectileEntityCount--;
        }
        if (entity is IEntityPhysicsParticipant)
        {
            _physicsEntityCount--;
        }
        if (entity is EnemyEntity removedActor)
        {
            removedActor.ResetAiSchedulingState();
        }
    }

    private void QueuePhysicsBody(IEntityPhysicsParticipant participant, ref int count)
    {
        _physicsBodies[count] = participant.Body;
        _physicsParticipants[count] = participant;
        count++;
    }

    private void StepEntityPhysics(GameWorld world, float deltaSeconds, int bodyCount)
    {
        if (bodyCount == 0)
        {
            PhysicsTelemetryLastUpdate = default;
            return;
        }

        var contactsPerBody = _physicsWorld.Settings.ContactsPerBody;
        var useContinuous = false;
        for (var index = 0; index < bodyCount; index++)
        {
            useContinuous = _physicsParticipants[index] is IContinuousCollisionPhysicsParticipant continuous
                && continuous.UseContinuousCollision;
            if (useContinuous)
            {
                break;
            }
        }

        try
        {
            if (useContinuous)
            {
                PhysicsTelemetryLastUpdate = _physicsWorld.StepWithContinuousBodyCollisions(
                    world,
                    _physicsBodies.AsSpan(0, bodyCount),
                    deltaSeconds,
                    _physicsResults.AsSpan(0, bodyCount),
                    _physicsContacts.AsSpan(0, bodyCount * contactsPerBody),
                    _physicsSortedBodyIndices.AsSpan(0, bodyCount),
                    _physicsBodyPairs,
                    _physicsContinuousBodyContacts,
                    _physicsContinuousContactCandidates,
                    _physicsBodySweepStates);
            }
            else
            {
                PhysicsTelemetryLastUpdate = _physicsWorld.StepWithBodyCollisions(
                    world,
                    _physicsBodies.AsSpan(0, bodyCount),
                    deltaSeconds,
                    _physicsResults.AsSpan(0, bodyCount),
                    _physicsContacts.AsSpan(0, bodyCount * contactsPerBody),
                    _physicsSortedBodyIndices.AsSpan(0, bodyCount),
                    _physicsBodyPairs,
                    _physicsBodyContacts);
            }

            var contacts = _physicsContacts.AsSpan(0, bodyCount * contactsPerBody);
            for (var index = 0; index < bodyCount; index++)
            {
                ref readonly var moveResult = ref _physicsResults[index];
                _physicsParticipants[index].SynchronizePhysicsState(
                    world,
                    in moveResult,
                    ReadOnlyTileContacts(
                        contacts,
                        index,
                        contactsPerBody,
                        moveResult.ContactsWritten));
            }
        }
        finally
        {
            Array.Clear(_physicsBodies, 0, bodyCount);
            Array.Clear(_physicsParticipants, 0, bodyCount);
        }
    }

    internal static ReadOnlySpan<PhysicsContact> ReadOnlyTileContacts(
        ReadOnlySpan<PhysicsContact> contacts,
        int bodyIndex,
        int contactsPerBody,
        int contactsWritten)
    {
        if (bodyIndex < 0 ||
            contactsPerBody <= 0 ||
            contactsWritten <= 0 ||
            contacts.IsEmpty)
        {
            return ReadOnlySpan<PhysicsContact>.Empty;
        }

        var contactOffset = (long)bodyIndex * contactsPerBody;
        if (contactOffset < 0 || contactOffset >= contacts.Length)
        {
            return ReadOnlySpan<PhysicsContact>.Empty;
        }

        var offset = (int)contactOffset;
        var available = Math.Min(contactsPerBody, contacts.Length - offset);
        return contacts.Slice(
            offset,
            Math.Min(contactsWritten, available));
    }

    private void EnsurePhysicsCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= _physicsBodies.Length)
        {
            return;
        }

        var capacity = Math.Min(
            _physicsWorld.Settings.MaximumBodiesPerStep,
            Math.Max(16, Math.Max(requiredCapacity, _physicsBodies.Length * 2)));
        Array.Resize(ref _physicsBodies, capacity);
        Array.Resize(ref _physicsParticipants, capacity);
        Array.Resize(ref _physicsResults, capacity);
        Array.Resize(ref _physicsSortedBodyIndices, capacity);
        Array.Resize(
            ref _physicsContacts,
            checked(capacity * _physicsWorld.Settings.ContactsPerBody));

        var possibleBodyPairs = checked((long)capacity * (capacity - 1) / 2);
        var requiredBodyPairs = (int)Math.Min(_maximumPhysicsBodyPairs, possibleBodyPairs);
        if (requiredBodyPairs > _physicsBodyPairs.Length)
        {
            Array.Resize(ref _physicsBodyPairs, requiredBodyPairs);
            Array.Resize(ref _physicsBodyContacts, requiredBodyPairs);
            Array.Resize(ref _physicsContinuousBodyContacts, requiredBodyPairs);
            Array.Resize(ref _physicsContinuousContactCandidates, requiredBodyPairs);
        }

        if (capacity > _physicsBodySweepStates.Length)
        {
            Array.Resize(ref _physicsBodySweepStates, capacity);
        }
    }

    private void PrepareProjectileHomingTargets(ProjectileEntity projectile, PlayerEntity? player)
    {
        var range = projectile.Definition.HomingRange;

        var center = projectile.Position;
        var left = Saturate(Math.Floor(center.X - range));
        var top = Saturate(Math.Floor(center.Y - range));
        var right = Saturate(Math.Ceiling(center.X + range));
        var bottom = Saturate(Math.Ceiling(center.Y + range));
        if (QueryNearestIntoBounded(
                RectI.FromInclusiveTileBounds(left, top, right, bottom),
                center,
                _projectileTargetQuery,
                MaximumHomingCandidatesPerProjectile,
                MaximumHomingEntryTestsPerProjectile,
                EntitySpatialQueryKinds.Player | EntitySpatialQueryKinds.Enemy))
        {
            HomingCandidateQueriesTruncatedLastUpdate++;
        }

        _projectileTargets.Clear();
        AddProjectileTarget(player, projectile);
        for (var index = 0; index < _projectileTargetQuery.Count; index++)
        {
            AddProjectileTarget(_projectileTargetQuery[index], projectile);
        }

        projectile.SetHomingTargetsForNextUpdate(_projectileTargets);
    }

    private void AddProjectileTarget(Entity? candidate, ProjectileEntity projectile)
    {
        if (_projectileTargets.Count >= MaximumHomingTargetsPerProjectile ||
            candidate is not (PlayerEntity or EnemyEntity) ||
            !candidate.IsActive ||
            candidate.Id == projectile.OwnerEntityId)
        {
            return;
        }

        _projectileTargets.Add(new ProjectileHomingTarget(
            candidate.Id,
            EntityFactionResolver.GetFaction(candidate),
            DistanceSensor.GetCenter(candidate)));
    }

    private int CountActiveHomingProjectiles()
    {
        if (_homingProjectileEntityCount == 0)
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; index < _entities.Count; index++)
        {
            if (_entities[index] is ProjectileEntity projectile && RequiresHoming(projectile))
            {
                count++;
            }
        }

        return count;
    }

    private static bool RequiresHoming(ProjectileEntity projectile)
    {
        return projectile.IsActive && IsHomingCapable(projectile);
    }

    private static bool IsHomingCapable(ProjectileEntity projectile)
    {
        return projectile.Definition.HomingRange > 0 &&
               projectile.Definition.HomingTurnRateRadiansPerSecond > 0;
    }

    private static int Saturate(double value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}
