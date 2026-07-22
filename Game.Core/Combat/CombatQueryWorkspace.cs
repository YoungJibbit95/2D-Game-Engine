using Game.Core.Entities;

namespace Game.Core.Combat;

/// <summary>
/// Caller-owned scratch storage for projectile and contact resolution. A workspace must not
/// be shared by concurrently executing combat pipelines.
/// </summary>
public sealed class CombatQueryWorkspace
{
    private readonly List<DroppedItemEntity> _pendingDrops;
    private readonly List<ProjectileHitCandidate> _projectileHits;
    private readonly List<PlayerProjectileHitCandidate> _playerProjectileHits;

    public CombatQueryWorkspace(int initialCandidateCapacity = 32, int initialDropCapacity = 8)
    {
        if (initialDropCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDropCapacity));
        }

        Candidates = new EntityQueryWorkspace(initialCandidateCapacity);
        _pendingDrops = new List<DroppedItemEntity>(initialDropCapacity);
        _projectileHits = new List<ProjectileHitCandidate>(initialCandidateCapacity);
        _playerProjectileHits = new List<PlayerProjectileHitCandidate>(initialCandidateCapacity);
    }

    public EntityQueryWorkspace Candidates { get; }

    public int PendingDropCapacity => _pendingDrops.Capacity;

    public void EnsureCapacity(int candidateCapacity, int pendingDropCapacity)
    {
        if (pendingDropCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pendingDropCapacity));
        }

        Candidates.EnsureCapacity(candidateCapacity);
        _pendingDrops.EnsureCapacity(pendingDropCapacity);
        _projectileHits.EnsureCapacity(candidateCapacity);
        _playerProjectileHits.EnsureCapacity(candidateCapacity);
    }

    internal List<DroppedItemEntity> PendingDrops => _pendingDrops;

    internal List<ProjectileHitCandidate> ProjectileHits => _projectileHits;

    internal List<PlayerProjectileHitCandidate> PlayerProjectileHits => _playerProjectileHits;

    internal void BeginResolution()
    {
        _pendingDrops.Clear();
        _projectileHits.Clear();
        _playerProjectileHits.Clear();
    }

    internal void BeginProjectile()
    {
        _projectileHits.Clear();
    }

    internal void BeginPlayerProjectileResolution()
    {
        _playerProjectileHits.Clear();
    }
}

internal readonly record struct ProjectileHitCandidate(EnemyEntity Enemy, double TimeOfImpact);

internal readonly record struct PlayerProjectileHitCandidate(
    Game.Core.Projectiles.ProjectileEntity Projectile,
    double TimeOfImpact);
