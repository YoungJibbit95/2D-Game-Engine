using Game.Core.World;
using System.Numerics;

namespace Game.Core.Entities.AI.Sensing;

public sealed class LocalPathSensor
{
    public bool HasObstacleAhead(World.World world, EnemyEntity entity, int direction)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);
        if (direction == 0)
        {
            return false;
        }

        var probeX = direction < 0 ? entity.Bounds.Left - 1 : entity.Bounds.Right;
        var tileX = PixelToTile(probeX);
        var minTileY = PixelToTile(entity.Bounds.Top + 1);
        var maxTileY = PixelToTile(entity.Bounds.Bottom - 1);
        for (var tileY = minTileY; tileY <= maxTileY; tileY++)
        {
            if (world.IsSolid(tileX, tileY))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanJumpObstacle(World.World world, EnemyEntity entity, int direction, int maxHeightTiles = 2)
    {
        if (!entity.Body.OnGround || maxHeightTiles <= 0 || !HasObstacleAhead(world, entity, direction))
        {
            return false;
        }

        var probeX = direction < 0 ? entity.Bounds.Left - 1 : entity.Bounds.Right;
        var tileX = PixelToTile(probeX);
        var footTileY = PixelToTile(entity.Bounds.Bottom - 1);
        var obstacleHeight = 0;
        for (var offset = 0; offset <= maxHeightTiles; offset++)
        {
            if (!world.IsSolid(tileX, footTileY - offset))
            {
                break;
            }

            obstacleHeight++;
        }

        if (obstacleHeight == 0 || obstacleHeight > maxHeightTiles)
        {
            return false;
        }

        var clearanceTileY = footTileY - obstacleHeight;
        return world.IsInBounds(tileX, clearanceTileY) && !world.IsSolid(tileX, clearanceTileY);
    }

    public Vector2 AvoidFlyingObstacle(World.World world, EnemyEntity entity, Vector2 desiredDirection)
    {
        if (desiredDirection == Vector2.Zero)
        {
            return desiredDirection;
        }

        var direction = Vector2.Normalize(desiredDirection);
        if (IsOpen(world, entity.Body.Center + direction * GameConstants.TileSize))
        {
            return direction;
        }

        var verticalPreference = (entity.Id & 1) == 0 ? -1f : 1f;
        var first = new Vector2(direction.X * 0.35f, verticalPreference);
        if (IsOpen(world, entity.Body.Center + Vector2.Normalize(first) * GameConstants.TileSize))
        {
            return first;
        }

        var second = new Vector2(direction.X * 0.35f, -verticalPreference);
        return IsOpen(world, entity.Body.Center + Vector2.Normalize(second) * GameConstants.TileSize)
            ? second
            : -direction;
    }

    private static bool IsOpen(World.World world, Vector2 position)
    {
        var tile = CoordinateUtils.WorldToTile(position);
        return world.IsInBounds(tile.X, tile.Y) && !world.IsSolid(tile.X, tile.Y);
    }

    private static int PixelToTile(int pixel)
    {
        return (int)MathF.Floor(pixel / (float)GameConstants.TileSize);
    }
}
