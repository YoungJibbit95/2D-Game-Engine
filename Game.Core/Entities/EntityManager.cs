using Game.Core.Utilities;
using Game.Core.World;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities;

public sealed class EntityManager
{
    private readonly List<Entity> _entities = new();
    private readonly SpatialGrid<Entity> _spatialGrid;
    private int _nextEntityId = 1;

    public EntityManager(int spatialCellSize = GameConstants.PixelsPerChunk)
    {
        _spatialGrid = new SpatialGrid<Entity>(spatialCellSize);
    }

    public IReadOnlyList<Entity> Entities => _entities;

    public void Add(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.Id == 0)
        {
            entity.AssignId(_nextEntityId++);
        }
        else
        {
            _nextEntityId = Math.Max(_nextEntityId, entity.Id + 1);
        }

        _entities.Add(entity);
        RebuildSpatialIndex();
    }

    public void Remove(Entity entity)
    {
        _entities.Remove(entity);
        _spatialGrid.Remove(entity);
    }

    public void UpdateAll(GameWorld world, float deltaSeconds)
    {
        foreach (var entity in _entities)
        {
            if (entity.IsActive)
            {
                entity.Update(world, deltaSeconds);
            }
        }

        _entities.RemoveAll(entity => !entity.IsActive);
        RebuildSpatialIndex();
    }

    public IReadOnlyList<Entity> Query(RectI area)
    {
        return _spatialGrid.Query(area);
    }

    private void RebuildSpatialIndex()
    {
        _spatialGrid.Clear();

        foreach (var entity in _entities)
        {
            if (entity.IsActive)
            {
                _spatialGrid.Insert(entity, entity.Bounds);
            }
        }
    }
}
