using Game.Core.World;

namespace Game.Core.Entities.AI.Sensing;

public sealed class LedgeSensor
{
    public bool HasLedgeAhead(World.World world, EnemyEntity entity, int direction)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);
        var edgeX = direction < 0 ? entity.Body.Bounds.Left - 1 : entity.Body.Bounds.Right;
        var tileX = (int)MathF.Floor(edgeX / GameConstants.TileSize);
        var footY = (int)MathF.Floor(entity.Body.Bounds.Bottom / (float)GameConstants.TileSize);
        return !world.IsInBounds(tileX, footY) || !world.IsSolid(tileX, footY);
    }
}
