using System.Numerics;

namespace Game.Core.Maps;

public sealed class TopDownMapSession
{
    public TopDownMapSession(string currentMapId, string currentSpawnId, TopDownMapBody body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentMapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSpawnId);
        ArgumentNullException.ThrowIfNull(body);

        CurrentMapId = currentMapId;
        CurrentSpawnId = currentSpawnId;
        Body = body;
    }

    public string CurrentMapId { get; private set; }

    public string CurrentSpawnId { get; private set; }

    public TopDownMapBody Body { get; }

    public static TopDownMapSession CreateAtSpawn(
        MapRegistry maps,
        string mapId,
        string spawnId,
        Vector2? bodySize = null)
    {
        ArgumentNullException.ThrowIfNull(maps);

        var map = maps.GetById(mapId);
        if (!map.TryGetSpawn(spawnId, out var spawn))
        {
            throw new KeyNotFoundException($"Map '{map.Id}' does not define spawn '{spawnId}'.");
        }

        var body = new TopDownMapBody(ToPixelPosition(map, spawn), bodySize)
        {
            Facing = TopDownFacingExtensions.Parse(spawn.Facing)
        };

        return new TopDownMapSession(map.Id, spawn.Id, body);
    }

    public void MoveToSpawn(MapDefinition map, MapSpawnPointDefinition spawn)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(spawn);

        CurrentMapId = map.Id;
        CurrentSpawnId = spawn.Id;
        Body.Position = ToPixelPosition(map, spawn);
        Body.Velocity = Vector2.Zero;
        Body.Facing = TopDownFacingExtensions.Parse(spawn.Facing, Body.Facing);
    }

    private static Vector2 ToPixelPosition(MapDefinition map, MapSpawnPointDefinition spawn)
    {
        return new Vector2(spawn.TileX * map.TileSize, spawn.TileY * map.TileSize);
    }
}
