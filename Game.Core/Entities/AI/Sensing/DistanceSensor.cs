using System.Numerics;

namespace Game.Core.Entities.AI.Sensing;

public sealed class DistanceSensor
{
    public Entity? FindNearestHostile(EnemyEntity observer, IReadOnlyList<Entity> entities, float range)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(entities);
        var rangeSquared = range * range;
        Entity? nearest = null;

        for (var index = 0; index < entities.Count; index++)
        {
            var candidate = entities[index];
            if (!candidate.IsActive || candidate.Id == observer.Id ||
                observer.GetDispositionToward(candidate) != EntityDisposition.Hostile)
            {
                continue;
            }

            var candidateCenter = GetCenter(candidate);
            var distanceSquared = Vector2.DistanceSquared(observer.Body.Center, candidateCenter);
            if (distanceSquared > rangeSquared ||
                (distanceSquared == rangeSquared && nearest is not null && candidate.Id >= nearest.Id))
            {
                continue;
            }

            rangeSquared = distanceSquared;
            nearest = candidate;
        }

        return nearest;
    }

    public static Vector2 GetCenter(Entity entity)
    {
        return entity switch
        {
            EnemyEntity actor => actor.Body.Center,
            PlayerEntity player => player.Body.Center,
            _ => new Vector2(entity.Bounds.X + entity.Bounds.Width * 0.5f, entity.Bounds.Y + entity.Bounds.Height * 0.5f)
        };
    }
}
