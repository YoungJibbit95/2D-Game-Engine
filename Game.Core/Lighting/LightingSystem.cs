using Game.Core.World;

namespace Game.Core.Lighting;

public sealed class LightingSystem
{
    private const int Sunlight = 255;
    private const int AirFalloff = 6;
    private const int SolidFalloff = 80;

    public void Recalculate(World.World world, IEnumerable<LightSource> lightSources)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(lightSources);

        var lightBuffer = new byte[world.WidthTiles * world.HeightTiles];

        ApplySunlight(world, lightBuffer);

        foreach (var source in lightSources)
        {
            PropagateLightSource(world, lightBuffer, source);
        }

        ApplyToWorld(world, lightBuffer);
    }

    private static void ApplySunlight(World.World world, byte[] lightBuffer)
    {
        for (var x = 0; x < world.WidthTiles; x++)
        {
            var light = Sunlight;

            for (var y = 0; y < world.HeightTiles; y++)
            {
                var tile = world.GetTile(x, y);
                SetLight(lightBuffer, world.WidthTiles, x, y, (byte)Math.Clamp(light, 0, 255));

                if (tile.IsSolid)
                {
                    light = Math.Max(0, light - SolidFalloff);
                }
                else if (light < Sunlight)
                {
                    light = Math.Max(0, light - AirFalloff);
                }
            }
        }
    }

    private static void PropagateLightSource(World.World world, byte[] lightBuffer, LightSource source)
    {
        if (!world.IsInBounds(source.Position.X, source.Position.Y) || source.Intensity == 0 || source.Radius <= 0)
        {
            return;
        }

        var queue = new Queue<LightNode>();
        var visited = new Dictionary<TilePos, byte>();
        var start = new LightNode(source.Position, source.Intensity, 0);

        queue.Enqueue(start);
        visited[source.Position] = source.Intensity;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var current = GetLight(lightBuffer, world.WidthTiles, node.Position.X, node.Position.Y);
            if (node.Intensity > current)
            {
                SetLight(lightBuffer, world.WidthTiles, node.Position.X, node.Position.Y, node.Intensity);
            }

            if (node.Distance >= source.Radius)
            {
                continue;
            }

            foreach (var neighbor in GetNeighbors(node.Position))
            {
                if (!world.IsInBounds(neighbor.X, neighbor.Y))
                {
                    continue;
                }

                var falloff = world.IsSolid(neighbor.X, neighbor.Y) ? SolidFalloff : 28;
                if (node.Intensity <= falloff)
                {
                    continue;
                }

                var nextIntensity = (byte)(node.Intensity - falloff);
                if (visited.TryGetValue(neighbor, out var previousIntensity) && previousIntensity >= nextIntensity)
                {
                    continue;
                }

                visited[neighbor] = nextIntensity;
                queue.Enqueue(new LightNode(neighbor, nextIntensity, node.Distance + 1));
            }
        }
    }

    private static IEnumerable<TilePos> GetNeighbors(TilePos position)
    {
        yield return new TilePos(position.X - 1, position.Y);
        yield return new TilePos(position.X + 1, position.Y);
        yield return new TilePos(position.X, position.Y - 1);
        yield return new TilePos(position.X, position.Y + 1);
    }

    private static void ApplyToWorld(World.World world, byte[] lightBuffer)
    {
        for (var y = 0; y < world.HeightTiles; y++)
        {
            for (var x = 0; x < world.WidthTiles; x++)
            {
                world.SetTileLight(x, y, GetLight(lightBuffer, world.WidthTiles, x, y));
            }
        }
    }

    private static byte GetLight(byte[] buffer, int width, int x, int y)
    {
        return buffer[y * width + x];
    }

    private static void SetLight(byte[] buffer, int width, int x, int y, byte light)
    {
        buffer[y * width + x] = Math.Max(buffer[y * width + x], light);
    }

    private readonly record struct LightNode(TilePos Position, byte Intensity, int Distance);
}
