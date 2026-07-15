using Game.Core.Entities.AI.Sensing;
using System.Numerics;

namespace Game.Core.Entities.AI;

internal sealed class AiSteering
{
    private readonly LedgeSensor _ledge = new();
    private readonly TileHazardSensor _hazard = new();
    private readonly LocalPathSensor _localPath = new();

    public int ResolveGroundDirection(EnemyEntity entity, AiUpdateContext context, int desiredDirection, AiProfileDefinition profile)
    {
        var direction = desiredDirection == 0 ? 1 : Math.Sign(desiredDirection);
        if (HasHazard(entity, context, direction, profile))
        {
            var alternative = -direction;
            return HasHazard(entity, context, alternative, profile) ? 0 : alternative;
        }

        return direction;
    }

    private bool HasHazard(
        EnemyEntity entity,
        AiUpdateContext context,
        int direction,
        AiProfileDefinition profile)
    {
        return direction != 0 &&
               ((profile.AvoidLedges && entity.Body.OnGround && _ledge.HasLedgeAhead(context.World, entity, direction)) ||
                (profile.AvoidLiquid && _hazard.HasLiquidAhead(context.World, entity, direction)));
    }

    public static void Move(EnemyEntity entity, Vector2 direction, float speed)
    {
        if (entity.MovementMode == EntityMovementMode.Flying)
        {
            var normalized = direction == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(direction);
            entity.Body.Velocity = normalized * speed;
            return;
        }

        var horizontal = Math.Abs(direction.X) < 0.01f ? 0 : Math.Sign(direction.X) * speed;
        entity.Body.Velocity = new Vector2(horizontal, entity.Body.Velocity.Y);
    }

    public void Move(
        EnemyEntity entity,
        AiUpdateContext context,
        Vector2 desiredDirection,
        float speed,
        AiProfileDefinition profile)
    {
        if (entity.MovementMode == EntityMovementMode.Flying)
        {
            var direction = _localPath.AvoidFlyingObstacle(context.World, entity, desiredDirection);
            Move(entity, direction, speed);
            return;
        }

        if (Math.Abs(desiredDirection.X) < 0.01f || speed <= 0)
        {
            Move(entity, Vector2.Zero, 0);
            return;
        }

        var directionX = ResolveGroundDirection(entity, context, Math.Sign(desiredDirection.X), profile);
        if (directionX == 0)
        {
            Move(entity, Vector2.Zero, 0);
            return;
        }

        if (_localPath.HasObstacleAhead(context.World, entity, directionX))
        {
            if (_localPath.CanJumpObstacle(context.World, entity, directionX) && profile.JumpSpeed > 0)
            {
                entity.Body.Velocity = new Vector2(entity.Body.Velocity.X, -profile.JumpSpeed);
                entity.Body.OnGround = false;
            }
            else
            {
                directionX = ResolveGroundDirection(entity, context, -directionX, profile);
            }
        }

        Move(entity, new Vector2(directionX, 0), speed);
    }
}
