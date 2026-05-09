using Game.Core.Entities;

namespace Game.Core.World.Queries;

public sealed class AreaQueryService
{
    public IReadOnlyList<Entity> QueryEntities(EntityManager entities, AreaQueryShape shape)
    {
        ArgumentNullException.ThrowIfNull(entities);

        if (shape.Bounds.IsEmpty)
        {
            return Array.Empty<Entity>();
        }

        return entities
            .Query(shape.Bounds)
            .Where(entity => entity.IsActive && shape.Intersects(entity.Bounds))
            .ToArray();
    }
}
