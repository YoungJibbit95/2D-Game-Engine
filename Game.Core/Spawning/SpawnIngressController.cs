using Game.Core.Entities;
using Game.Core.World;

namespace Game.Core.Spawning;

internal sealed class SpawnIngressController
{
    private readonly List<IngressLease> _leases = new(4);

    public int ActiveLeaseCount => _leases.Count;

    public void Track(EnemyEntity actor, SpawnActivitySource source, SpawnSchedulerOptions options)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(options);
        if (source.VisibleTileBounds.IsEmpty || options.ViewportIngressMaxSeconds <= 0)
        {
            return;
        }

        var actorTiles = ResolveTileBounds(actor);
        if (actorTiles.Intersects(source.VisibleTileBounds))
        {
            return;
        }

        var approachesFromLeft = actor.Body.Center.X / GameConstants.TileSize <
            source.VisibleTileBounds.Left;
        _leases.Add(new IngressLease(
            actor.Id,
            source.Id,
            approachesFromLeft,
            options.ViewportIngressMaxSeconds));
    }

    public void Advance(
        World.World world,
        EntityManager entities,
        IReadOnlyList<SpawnActivitySource>? activitySources,
        SpawnActivitySource singleSource,
        int sourceCount,
        float deltaSeconds,
        SpawnSchedulerOptions options)
    {
        if (_leases.Count == 0 || deltaSeconds <= 0)
        {
            return;
        }

        var distance = options.ViewportIngressSpeedTilesPerSecond *
            GameConstants.TileSize * deltaSeconds;
        for (var index = _leases.Count - 1; index >= 0; index--)
        {
            var lease = _leases[index];
            if (entities.FindActiveEntity(lease.EntityId) is not EnemyEntity actor ||
                !TryFindSource(
                    activitySources,
                    singleSource,
                    sourceCount,
                    lease.SourceId,
                    out var source) ||
                source.VisibleTileBounds.IsEmpty ||
                ResolveTileBounds(actor).Intersects(source.VisibleTileBounds))
            {
                _leases.RemoveAt(index);
                continue;
            }

            var remaining = lease.RemainingSeconds - deltaSeconds;
            if (remaining <= 0)
            {
                _leases.RemoveAt(index);
                continue;
            }

            if (distance > 0)
            {
                AdvanceActor(world, actor, source.VisibleTileBounds, lease.ApproachesFromLeft, distance);
            }

            if (ResolveTileBounds(actor).Intersects(source.VisibleTileBounds))
            {
                _leases.RemoveAt(index);
            }
            else
            {
                _leases[index] = lease with { RemainingSeconds = remaining };
            }
        }
    }

    private static void AdvanceActor(
        World.World world,
        EnemyEntity actor,
        RectI visibleTiles,
        bool approachesFromLeft,
        float distance)
    {
        var targetX = approachesFromLeft
            ? (long)visibleTiles.Left * GameConstants.TileSize - actor.Body.Size.X + 0.5f
            : (long)visibleTiles.Right * GameConstants.TileSize - 0.5f;
        var current = actor.Body.Position;
        var nextX = MoveTowards(current.X, targetX, distance);
        if (nextX == current.X || !IsBodyClear(world, nextX, current.Y, actor.Body.Size.X, actor.Body.Size.Y))
        {
            return;
        }

        actor.Body.Position = new System.Numerics.Vector2(nextX, current.Y);
    }

    private static bool IsBodyClear(World.World world, float x, float y, float width, float height)
    {
        var min = CoordinateUtils.WorldToTile(x, y);
        var max = CoordinateUtils.WorldToTile(
            x + Math.Max(0, width - 0.01f),
            y + Math.Max(0, height - 0.01f));
        for (var tileY = (long)min.Y; tileY <= max.Y; tileY++)
        {
            for (var tileX = (long)min.X; tileX <= max.X; tileX++)
            {
                if (!world.TryGetTile((int)tileX, (int)tileY, out var tile) || tile.IsSolid)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static RectI ResolveTileBounds(EnemyEntity actor)
    {
        var min = CoordinateUtils.WorldToTile(actor.Body.Position.X, actor.Body.Position.Y);
        var max = CoordinateUtils.WorldToTile(
            actor.Body.Position.X + Math.Max(0, actor.Body.Size.X - 0.01f),
            actor.Body.Position.Y + Math.Max(0, actor.Body.Size.Y - 0.01f));
        return RectI.FromInclusiveTileBounds(min.X, min.Y, max.X, max.Y);
    }

    private static bool TryFindSource(
        IReadOnlyList<SpawnActivitySource>? activitySources,
        SpawnActivitySource singleSource,
        int sourceCount,
        int sourceId,
        out SpawnActivitySource source)
    {
        for (var index = 0; index < sourceCount; index++)
        {
            var candidate = activitySources is null ? singleSource : activitySources[index];
            if (candidate.Id == sourceId)
            {
                source = candidate;
                return true;
            }
        }

        source = default;
        return false;
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        var delta = target - current;
        return Math.Abs(delta) <= maxDelta
            ? target
            : current + MathF.CopySign(maxDelta, delta);
    }

    private readonly record struct IngressLease(
        int EntityId,
        int SourceId,
        bool ApproachesFromLeft,
        float RemainingSeconds);
}
