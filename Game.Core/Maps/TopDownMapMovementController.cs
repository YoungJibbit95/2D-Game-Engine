using System.Numerics;
using Game.Core.Movement;
using Game.Core.World;

namespace Game.Core.Maps;

public sealed class TopDownMapMovementController
{
    private readonly TopDownMapQueryService _queries;

    public TopDownMapMovementController(TopDownMapQueryService? queries = null)
    {
        _queries = queries ?? new TopDownMapQueryService();
    }

    public TopDownMapMovementResult Move(
        MapDefinition map,
        TopDownMapBody body,
        Vector2 direction,
        float deltaSeconds,
        TopDownMovementOptions? options = null,
        TopDownMapRuntimeState? runtimeState = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(body);

        options ??= TopDownMovementOptions.Default;
        var previous = body.Position;

        if (deltaSeconds <= 0)
        {
            body.Velocity = Vector2.Zero;
            return BuildResult(map, body, previous, blockedX: false, blockedY: false, runtimeState);
        }

        var movement = TopDownMovementMath.ResolveDirection(direction, options);
        if (movement.LengthSquared() > float.Epsilon)
        {
            body.Facing = TopDownFacingExtensions.FromVector(movement, body.Facing);
        }

        body.Velocity = movement * TopDownMovementMath.ResolveSpeed(options);

        var delta = body.Velocity * deltaSeconds;
        var blockedX = MoveAxis(map, body, delta.X, Axis.X, runtimeState);
        var blockedY = MoveAxis(map, body, delta.Y, Axis.Y, runtimeState);

        if (blockedX)
        {
            body.Velocity = new Vector2(0, body.Velocity.Y);
        }

        if (blockedY)
        {
            body.Velocity = new Vector2(body.Velocity.X, 0);
        }

        return BuildResult(map, body, previous, blockedX, blockedY, runtimeState);
    }

    private TopDownMapMovementResult BuildResult(
        MapDefinition map,
        TopDownMapBody body,
        Vector2 previous,
        bool blockedX,
        bool blockedY,
        TopDownMapRuntimeState? runtimeState)
    {
        _queries.TryResolveWarp(map, body.CenterTile(map.TileSize), runtimeState, out var warp);
        return new TopDownMapMovementResult(previous, body.Position, body.Velocity, body.Facing, blockedX, blockedY, warp);
    }

    private bool MoveAxis(
        MapDefinition map,
        TopDownMapBody body,
        float amount,
        Axis axis,
        TopDownMapRuntimeState? runtimeState)
    {
        if (MathF.Abs(amount) <= float.Epsilon)
        {
            return false;
        }

        var previous = body.Position;
        body.Position = axis == Axis.X
            ? new Vector2(body.Position.X + amount, body.Position.Y)
            : new Vector2(body.Position.X, body.Position.Y + amount);

        var blocker = FindFirstBlockingTile(map, body, runtimeState);
        if (blocker is null)
        {
            return false;
        }

        if (axis == Axis.X)
        {
            var x = amount > 0
                ? blocker.Value.X * map.TileSize - body.Size.X
                : (blocker.Value.X + 1) * map.TileSize;

            body.Position = new Vector2(x, previous.Y);
        }
        else
        {
            var y = amount > 0
                ? blocker.Value.Y * map.TileSize - body.Size.Y
                : (blocker.Value.Y + 1) * map.TileSize;

            body.Position = new Vector2(previous.X, y);
        }

        return true;
    }

    private TilePos? FindFirstBlockingTile(MapDefinition map, TopDownMapBody body, TopDownMapRuntimeState? runtimeState)
    {
        var tileBounds = body.BoundsTiles(map.TileSize);
        for (var y = tileBounds.Top; y < tileBounds.Bottom; y++)
        {
            for (var x = tileBounds.Left; x < tileBounds.Right; x++)
            {
                var tile = new TilePos(x, y);
                if (_queries.IsBlocked(map, tile, runtimeState))
                {
                    return tile;
                }
            }
        }

        return null;
    }

    private enum Axis
    {
        X,
        Y
    }
}
