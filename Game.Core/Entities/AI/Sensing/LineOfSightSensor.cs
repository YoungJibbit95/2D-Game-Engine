using Game.Core.World;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Entities.AI.Sensing;

public sealed class LineOfSightSensor
{
    public bool HasLineOfSight(GameWorld world, Vector2 from, Vector2 to)
    {
        ArgumentNullException.ThrowIfNull(world);
        var start = CoordinateUtils.WorldToTile(from);
        var end = CoordinateUtils.WorldToTile(to);
        var x = start.X;
        var y = start.Y;
        var dx = Math.Abs(end.X - start.X);
        var dy = Math.Abs(end.Y - start.Y);
        var stepX = start.X < end.X ? 1 : -1;
        var stepY = start.Y < end.Y ? 1 : -1;
        var error = dx - dy;

        while (x != end.X || y != end.Y)
        {
            var twiceError = error * 2;
            if (twiceError > -dy)
            {
                error -= dy;
                x += stepX;
            }

            if (twiceError < dx)
            {
                error += dx;
                y += stepY;
            }

            if ((x != end.X || y != end.Y) && world.IsSolid(x, y))
            {
                return false;
            }
        }

        return true;
    }
}
