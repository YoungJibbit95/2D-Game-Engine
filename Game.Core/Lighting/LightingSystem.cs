using Game.Core.World;

namespace Game.Core.Lighting;

public sealed class LightingSystem
{
    public void Recalculate(World.World world, IEnumerable<LightSource> lightSources, byte sunlight = 255, LightingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(lightSources);
        options ??= LightingOptions.Default;

        var lightBuffer = new byte[world.WidthTiles * world.HeightTiles];

        ApplySunlight(world, lightBuffer, sunlight, options);

        foreach (var source in lightSources)
        {
            PropagateLightSource(world, lightBuffer, source, options);
        }

        ApplyToWorld(world, lightBuffer);
    }

    private static void ApplySunlight(World.World world, byte[] lightBuffer, byte sunlight, LightingOptions options)
    {
        var maxSunlight = (int)sunlight;
        for (var x = 0; x < world.WidthTiles; x++)
        {
            var light = maxSunlight;
            var skyBlocked = false;

            for (var y = 0; y < world.HeightTiles; y++)
            {
                var tile = world.GetTile(x, y);
                SetLight(lightBuffer, world.WidthTiles, x, y, (byte)Math.Clamp(light, 0, 255));

                if (tile.IsSolid)
                {
                    skyBlocked = true;
                    light = Math.Max(options.MinimumAmbientLight, light - options.SolidFalloff);
                }
                else if (skyBlocked)
                {
                    light = Math.Max(options.MinimumAmbientLight, light - options.UndergroundAirFalloff);
                }
                else if (light < maxSunlight)
                {
                    light = Math.Max(options.MinimumAmbientLight, light - options.OpenAirFalloff);
                }
            }
        }
    }

    private static void PropagateLightSource(World.World world, byte[] lightBuffer, LightSource source, LightingOptions options)
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

                var falloff = world.IsSolid(neighbor.X, neighbor.Y) ? options.PointLightSolidFalloff : options.PointLightAirFalloff;
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
