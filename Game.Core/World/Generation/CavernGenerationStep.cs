namespace Game.Core.World.Generation;

public sealed class CavernGenerationStep : IWorldGenerationStep
{
    public string Name => "caverns";

    public int Order => 12;

    public void Apply(WorldGenerationContext context)
    {
        var world = context.World;
        var profile = context.Profile;
        if (profile.CavernRoomCount <= 0 || world.WidthTiles < 9 || world.HeightTiles < 12)
        {
            return;
        }

        var minRadiusX = Math.Max(2, Math.Min(profile.CavernMinRadiusX, profile.CavernMaxRadiusX));
        var maxRadiusX = Math.Max(minRadiusX, Math.Max(profile.CavernMinRadiusX, profile.CavernMaxRadiusX));
        var minRadiusY = Math.Max(2, Math.Min(profile.CavernMinRadiusY, profile.CavernMaxRadiusY));
        var maxRadiusY = Math.Max(minRadiusY, Math.Max(profile.CavernMinRadiusY, profile.CavernMaxRadiusY));
        maxRadiusX = Math.Min(maxRadiusX, Math.Max(2, (world.WidthTiles - 5) / 2));
        maxRadiusY = Math.Min(maxRadiusY, Math.Max(2, (world.HeightTiles - 5) / 2));
        minRadiusX = Math.Min(minRadiusX, maxRadiusX);
        minRadiusY = Math.Min(minRadiusY, maxRadiusY);

        var rooms = PlaceRooms(context, minRadiusX, maxRadiusX, minRadiusY, maxRadiusY);
        foreach (var room in rooms)
        {
            CarveRoom(context, room);
        }

        ConnectRooms(context, rooms);
    }

    private static List<CavernRoom> PlaceRooms(
        WorldGenerationContext context,
        int minRadiusX,
        int maxRadiusX,
        int minRadiusY,
        int maxRadiusY)
    {
        var world = context.World;
        var profile = context.Profile;
        var rooms = new List<CavernRoom>();
        var attempts = Math.Max(profile.CavernRoomCount * 12, profile.CavernRoomCount);

        for (var attempt = 0; attempt < attempts && rooms.Count < profile.CavernRoomCount; attempt++)
        {
            var radiusX = NextInclusive(context.Random, minRadiusX, maxRadiusX);
            var radiusY = NextInclusive(context.Random, minRadiusY, maxRadiusY);
            var minX = radiusX + 2;
            var maxX = world.WidthTiles - radiusX - 3;
            if (maxX < minX)
            {
                continue;
            }

            var centerX = NextInclusive(context.Random, minX, maxX);
            var minY = Math.Max(radiusY + 2, context.SurfaceHeights[centerX] + Math.Max(0, profile.CavernMinDepthOffset) + radiusY);
            var maxY = world.HeightTiles - radiusY - 3;
            if (maxY < minY)
            {
                continue;
            }

            var centerY = NextInclusive(context.Random, minY, maxY);
            var candidate = new CavernRoom(centerX, centerY, radiusX, radiusY, context.Random.Next());
            if (rooms.Any(room => IsTooClose(room, candidate)))
            {
                continue;
            }

            rooms.Add(candidate);
        }

        return rooms;
    }

    private static bool IsTooClose(CavernRoom first, CavernRoom second)
    {
        var dx = first.CenterX - second.CenterX;
        var dy = first.CenterY - second.CenterY;
        var requiredX = (first.RadiusX + second.RadiusX) * 0.55f;
        var requiredY = (first.RadiusY + second.RadiusY) * 0.55f;
        return dx * dx / (requiredX * requiredX) + dy * dy / (requiredY * requiredY) < 1f;
    }

    private static void CarveRoom(WorldGenerationContext context, CavernRoom room)
    {
        var world = context.World;
        var irregularity = Math.Clamp(context.Profile.CavernIrregularity, 0f, 1f);
        for (var y = room.CenterY - room.RadiusY - 1; y <= room.CenterY + room.RadiusY + 1; y++)
        {
            for (var x = room.CenterX - room.RadiusX - 1; x <= room.CenterX + room.RadiusX + 1; x++)
            {
                if (!world.IsInBounds(x, y) || y <= context.SurfaceHeights[x] + 2)
                {
                    continue;
                }

                var normalizedX = (x - room.CenterX) / (float)room.RadiusX;
                var normalizedY = (y - room.CenterY) / (float)room.RadiusY;
                var distance = normalizedX * normalizedX + normalizedY * normalizedY;
                var edgeNoise = context.Noise.GetNoise((x + room.NoiseOffset) * 2.1f, (y - room.NoiseOffset) * 2.1f);
                var edge = 1f + edgeNoise * irregularity;
                if (distance <= edge)
                {
                    world.RemoveTile(x, y);
                }
            }
        }
    }

    private static void ConnectRooms(WorldGenerationContext context, IReadOnlyCollection<CavernRoom> rooms)
    {
        var ordered = rooms.OrderBy(room => room.CenterX).ThenBy(room => room.CenterY).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            CarveConnector(context, ordered[index - 1], ordered[index]);
        }
    }

    private static void CarveConnector(WorldGenerationContext context, CavernRoom start, CavernRoom end)
    {
        var dx = end.CenterX - start.CenterX;
        var dy = end.CenterY - start.CenterY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        var steps = Math.Max(1, (int)MathF.Ceiling(distance * 1.5f));
        var wander = Math.Max(0, context.Profile.CavernConnectorWander);
        var controlX = (start.CenterX + end.CenterX) * 0.5f + NextInclusive(context.Random, -wander, wander);
        var controlY = (start.CenterY + end.CenterY) * 0.5f + NextInclusive(context.Random, -wander, wander);
        var radius = Math.Max(1, context.Profile.CavernConnectorRadius);

        for (var step = 0; step <= steps; step++)
        {
            var t = step / (float)steps;
            var inverse = 1f - t;
            var x = inverse * inverse * start.CenterX + 2f * inverse * t * controlX + t * t * end.CenterX;
            var y = inverse * inverse * start.CenterY + 2f * inverse * t * controlY + t * t * end.CenterY;
            CarveTunnelDisc(context, (int)MathF.Round(x), (int)MathF.Round(y), radius);
        }
    }

    private static void CarveTunnelDisc(WorldGenerationContext context, int centerX, int centerY, int radius)
    {
        var world = context.World;
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (!world.IsInBounds(x, y) || y <= context.SurfaceHeights[x] + 2)
                {
                    continue;
                }

                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    world.RemoveTile(x, y);
                }
            }
        }
    }

    private static int NextInclusive(Random random, int min, int max)
    {
        return min == max ? min : random.Next(min, max + 1);
    }

    private readonly record struct CavernRoom(int CenterX, int CenterY, int RadiusX, int RadiusY, int NoiseOffset);
}
