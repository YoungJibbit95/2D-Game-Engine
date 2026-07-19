using System.Collections;
using System.Numerics;
using Game.Core.Combat;
using Game.Core.Entities;
using Game.Core.Entities.AI;
using Game.Core.Inventory;
using Game.Core.World;

namespace Game.Core.Runtime;

internal sealed class EntityFrameSnapshotBuilder
{
    private EntitySnapshotIdentity[] _identities = Array.Empty<EntitySnapshotIdentity>();
    private Vector2[] _positions = Array.Empty<Vector2>();
    private Vector2[] _velocities = Array.Empty<Vector2>();
    private EntitySnapshotGeometry[] _geometry = Array.Empty<EntitySnapshotGeometry>();
    private int[] _health = Array.Empty<int>();
    private bool[] _damageFlashing = Array.Empty<bool>();
    private AiState?[] _aiStates = Array.Empty<AiState?>();
    private int?[] _aiTargets = Array.Empty<int?>();
    private AiTelemetrySnapshot[] _aiTelemetry = Array.Empty<AiTelemetrySnapshot>();
    private EntityFrameSnapshotSequence? _currentSequence;
    private ImmutableSnapshotList<EntityFrameSnapshot> _currentSnapshot =
        ImmutableSnapshotList<EntityFrameSnapshot>.Empty;
    private int _count;
    private int _previousScratchCount;

    public void Begin(int capacityHint)
    {
        if (capacityHint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityHint));
        }

        EnsureCapacity(capacityHint);
        if (capacityHint < _previousScratchCount)
        {
            Array.Clear(_identities, capacityHint, _previousScratchCount - capacityHint);
        }

        _count = 0;
        _previousScratchCount = capacityHint;
    }

    public void Add(in EntityFrameSnapshot snapshot)
    {
        EnsureCapacity(_count + 1);
        var index = _count++;
        _identities[index] = new EntitySnapshotIdentity(
            snapshot.Id,
            snapshot.Kind,
            snapshot.ContentId,
            snapshot.IsActive,
            snapshot.MaxHealth,
            snapshot.ItemStack,
            snapshot.DamageType,
            snapshot.Faction);
        _positions[index] = snapshot.Position;
        _velocities[index] = snapshot.Velocity;
        _geometry[index] = EntitySnapshotGeometry.Capture(snapshot.Position, snapshot.Bounds);
        _health[index] = snapshot.Health;
        _damageFlashing[index] = snapshot.IsDamageFlashing;
        _aiStates[index] = snapshot.AiState;
        _aiTargets[index] = snapshot.AiTargetEntityId;
        _aiTelemetry[index] = snapshot.AiTelemetry;
    }

    public ImmutableSnapshotList<EntityFrameSnapshot> Complete()
    {
        if (_count == 0)
        {
            _currentSequence = null;
            _currentSnapshot = ImmutableSnapshotList<EntityFrameSnapshot>.Empty;
            return _currentSnapshot;
        }

        var previous = _currentSequence;
        var identities = RuntimeSnapshotColumn<EntitySnapshotIdentity>.Capture(
            _identities,
            _count,
            previous?.Identities ?? default);
        var positions = RuntimeSnapshotColumn<Vector2>.Capture(
            _positions,
            _count,
            previous?.Positions ?? default);
        var velocities = RuntimeSnapshotColumn<Vector2>.Capture(
            _velocities,
            _count,
            previous?.Velocities ?? default);
        var geometry = RuntimeSnapshotColumn<EntitySnapshotGeometry>.Capture(
            _geometry,
            _count,
            previous?.Geometry ?? default);
        var health = RuntimeSnapshotColumn<int>.Capture(
            _health,
            _count,
            previous?.Health ?? default);
        var damageFlashing = RuntimeSnapshotColumn<bool>.Capture(
            _damageFlashing,
            _count,
            previous?.DamageFlashing ?? default);
        var aiStates = RuntimeSnapshotColumn<AiState?>.Capture(
            _aiStates,
            _count,
            previous?.AiStates ?? default);
        var aiTargets = RuntimeSnapshotColumn<int?>.Capture(
            _aiTargets,
            _count,
            previous?.AiTargets ?? default);
        var aiTelemetry = RuntimeSnapshotColumn<AiTelemetrySnapshot>.Capture(
            _aiTelemetry,
            _count,
            previous?.AiTelemetry ?? default);

        if (previous is not null &&
            identities.SharesStorageWith(previous.Identities) &&
            positions.SharesStorageWith(previous.Positions) &&
            velocities.SharesStorageWith(previous.Velocities) &&
            geometry.SharesStorageWith(previous.Geometry) &&
            health.SharesStorageWith(previous.Health) &&
            damageFlashing.SharesStorageWith(previous.DamageFlashing) &&
            aiStates.SharesStorageWith(previous.AiStates) &&
            aiTargets.SharesStorageWith(previous.AiTargets) &&
            aiTelemetry.SharesStorageWith(previous.AiTelemetry))
        {
            return _currentSnapshot;
        }

        var sequence = new EntityFrameSnapshotSequence(
            identities,
            positions,
            velocities,
            geometry,
            health,
            damageFlashing,
            aiStates,
            aiTargets,
            aiTelemetry);
        _currentSequence = sequence;
        _currentSnapshot = ImmutableSnapshotList<EntityFrameSnapshot>.FromOwned(sequence);
        return _currentSnapshot;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _identities.Length)
        {
            return;
        }

        var capacity = Math.Max(required, Math.Max(16, _identities.Length * 2));
        Array.Resize(ref _identities, capacity);
        Array.Resize(ref _positions, capacity);
        Array.Resize(ref _velocities, capacity);
        Array.Resize(ref _geometry, capacity);
        Array.Resize(ref _health, capacity);
        Array.Resize(ref _damageFlashing, capacity);
        Array.Resize(ref _aiStates, capacity);
        Array.Resize(ref _aiTargets, capacity);
        Array.Resize(ref _aiTelemetry, capacity);
    }
}

internal sealed class EntityFrameSnapshotSequence : IReadOnlyList<EntityFrameSnapshot>
{
    public EntityFrameSnapshotSequence(
        RuntimeSnapshotColumn<EntitySnapshotIdentity> identities,
        RuntimeSnapshotColumn<Vector2> positions,
        RuntimeSnapshotColumn<Vector2> velocities,
        RuntimeSnapshotColumn<EntitySnapshotGeometry> geometry,
        RuntimeSnapshotColumn<int> health,
        RuntimeSnapshotColumn<bool> damageFlashing,
        RuntimeSnapshotColumn<AiState?> aiStates,
        RuntimeSnapshotColumn<int?> aiTargets,
        RuntimeSnapshotColumn<AiTelemetrySnapshot> aiTelemetry)
    {
        Identities = identities;
        Positions = positions;
        Velocities = velocities;
        Geometry = geometry;
        Health = health;
        DamageFlashing = damageFlashing;
        AiStates = aiStates;
        AiTargets = aiTargets;
        AiTelemetry = aiTelemetry;
    }

    internal RuntimeSnapshotColumn<EntitySnapshotIdentity> Identities { get; }

    internal RuntimeSnapshotColumn<Vector2> Positions { get; }

    internal RuntimeSnapshotColumn<Vector2> Velocities { get; }

    internal RuntimeSnapshotColumn<EntitySnapshotGeometry> Geometry { get; }

    internal RuntimeSnapshotColumn<int> Health { get; }

    internal RuntimeSnapshotColumn<bool> DamageFlashing { get; }

    internal RuntimeSnapshotColumn<AiState?> AiStates { get; }

    internal RuntimeSnapshotColumn<int?> AiTargets { get; }

    internal RuntimeSnapshotColumn<AiTelemetrySnapshot> AiTelemetry { get; }

    public int Count => Identities.Count;

    public EntityFrameSnapshot this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var identity = Identities[index];
            var position = Positions[index];
            return new EntityFrameSnapshot(
                identity.Id,
                identity.Kind,
                identity.ContentId,
                position,
                Velocities[index],
                Geometry[index].Restore(position),
                identity.IsActive,
                Health[index],
                identity.MaxHealth,
                identity.ItemStack,
                DamageFlashing[index],
                identity.DamageType)
            {
                Faction = identity.Faction,
                AiState = AiStates[index],
                AiTargetEntityId = AiTargets[index],
                AiTelemetry = AiTelemetry[index]
            };
        }
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<EntityFrameSnapshot> IEnumerable<EntityFrameSnapshot>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public struct Enumerator : IEnumerator<EntityFrameSnapshot>
    {
        private readonly EntityFrameSnapshotSequence _owner;
        private int _index;

        internal Enumerator(EntityFrameSnapshotSequence owner)
        {
            _owner = owner;
            _index = -1;
        }

        public readonly EntityFrameSnapshot Current => _owner[_index];

        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            var next = _index + 1;
            if ((uint)next < (uint)_owner.Count)
            {
                _index = next;
                return true;
            }

            _index = _owner.Count;
            return false;
        }

        public void Reset()
        {
            _index = -1;
        }

        public readonly void Dispose()
        {
        }
    }
}

internal readonly record struct EntitySnapshotIdentity(
    int Id,
    EntityFrameKind Kind,
    string ContentId,
    bool IsActive,
    int MaxHealth,
    ItemStack ItemStack,
    DamageType DamageType,
    EntityFaction Faction);

internal readonly record struct EntitySnapshotGeometry(int OffsetX, int OffsetY, int Width, int Height)
{
    public static EntitySnapshotGeometry Capture(Vector2 position, RectI bounds)
    {
        return new EntitySnapshotGeometry(
            unchecked(bounds.X - (int)position.X),
            unchecked(bounds.Y - (int)position.Y),
            bounds.Width,
            bounds.Height);
    }

    public RectI Restore(Vector2 position)
    {
        return new RectI(
            unchecked((int)position.X + OffsetX),
            unchecked((int)position.Y + OffsetY),
            Width,
            Height);
    }
}

internal readonly struct RuntimeSnapshotColumn<T>
{
    private readonly T[]? _values;
    private readonly T _uniformValue;

    private RuntimeSnapshotColumn(int count, T uniformValue, T[]? values)
    {
        Count = count;
        _uniformValue = uniformValue;
        _values = values;
    }

    public int Count { get; }

    public T this[int index] => _values is null ? _uniformValue : _values[index];

    public static RuntimeSnapshotColumn<T> Capture(
        T[] scratch,
        int count,
        RuntimeSnapshotColumn<T> previous)
    {
        if (previous.Matches(scratch, count))
        {
            return previous;
        }

        var comparer = EqualityComparer<T>.Default;
        var uniformValue = scratch[0];
        var isUniform = true;
        for (var index = 1; index < count; index++)
        {
            if (!comparer.Equals(uniformValue, scratch[index]))
            {
                isUniform = false;
                break;
            }
        }

        if (isUniform)
        {
            return new RuntimeSnapshotColumn<T>(count, uniformValue, values: null);
        }

        var values = GC.AllocateUninitializedArray<T>(count);
        Array.Copy(scratch, values, count);
        return new RuntimeSnapshotColumn<T>(count, default!, values);
    }

    public bool SharesStorageWith(RuntimeSnapshotColumn<T> other)
    {
        if (Count != other.Count || !ReferenceEquals(_values, other._values))
        {
            return false;
        }

        return _values is not null || EqualityComparer<T>.Default.Equals(_uniformValue, other._uniformValue);
    }

    private bool Matches(T[] scratch, int count)
    {
        if (Count != count || count == 0)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        if (_values is null)
        {
            for (var index = 0; index < count; index++)
            {
                if (!comparer.Equals(_uniformValue, scratch[index]))
                {
                    return false;
                }
            }

            return true;
        }

        for (var index = 0; index < count; index++)
        {
            if (!comparer.Equals(_values[index], scratch[index]))
            {
                return false;
            }
        }

        return true;
    }
}
