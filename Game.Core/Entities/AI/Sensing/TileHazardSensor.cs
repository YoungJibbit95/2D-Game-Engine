namespace Game.Core.Entities.AI.Sensing;

public sealed class TileHazardSensor
{
    public bool HasLiquidAhead(World.World world, EnemyEntity entity, int direction)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);
        var edgeX = direction < 0 ? entity.Body.Bounds.Left - 1 : entity.Body.Bounds.Right;
        var tileX = (int)MathF.Floor(edgeX / GameConstants.TileSize);
        var centerY = (int)MathF.Floor(entity.Body.Center.Y / GameConstants.TileSize);

        for (var offsetY = 0; offsetY <= 1; offsetY++)
        {
            var tileY = centerY + offsetY;
            if (world.IsInBounds(tileX, tileY) && world.GetTile(tileX, tileY).HasLiquid)
            {
                return true;
            }
        }

        return false;
    }
}
