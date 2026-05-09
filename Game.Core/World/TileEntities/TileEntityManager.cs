namespace Game.Core.World.TileEntities;

public sealed class TileEntityManager
{
    private readonly Dictionary<TilePos, TileEntity> _byPosition = new();
    private readonly List<TileEntity> _entities = new();
    private int _nextRuntimeId = 1;

    public IReadOnlyList<TileEntity> Entities => _entities;

    public bool Add(TileEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_byPosition.ContainsKey(entity.Position))
        {
            return false;
        }

        if (entity.RuntimeId == 0)
        {
            entity.AssignId(_nextRuntimeId++);
        }
        else
        {
            _nextRuntimeId = Math.Max(_nextRuntimeId, entity.RuntimeId + 1);
        }

        _byPosition.Add(entity.Position, entity);
        _entities.Add(entity);
        return true;
    }

    public bool Remove(TilePos position)
    {
        if (!_byPosition.Remove(position, out var entity))
        {
            return false;
        }

        _entities.Remove(entity);
        return true;
    }

    public bool TryGet(TilePos position, out TileEntity entity)
    {
        return _byPosition.TryGetValue(position, out entity!);
    }

    public IReadOnlyList<TileEntity> Query(RectI tileRegion)
    {
        if (tileRegion.IsEmpty)
        {
            return Array.Empty<TileEntity>();
        }

        return _entities
            .Where(entity => tileRegion.Contains(entity.Position))
            .ToArray();
    }
}
