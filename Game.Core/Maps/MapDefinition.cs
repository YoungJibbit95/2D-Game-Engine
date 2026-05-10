using Game.Core.Data;
using Game.Core.World;

namespace Game.Core.Maps;

public sealed record MapDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public int WidthTiles { get; init; }

    public int HeightTiles { get; init; }

    public int TileSize { get; init; } = GameConstants.TileSize;

    public IReadOnlyList<MapTileLayerDefinition> Layers { get; init; } = Array.Empty<MapTileLayerDefinition>();

    public IReadOnlyList<MapObjectDefinition> Objects { get; init; } = Array.Empty<MapObjectDefinition>();

    public IReadOnlyList<MapSpawnPointDefinition> SpawnPoints { get; init; } = Array.Empty<MapSpawnPointDefinition>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public RectI Bounds => new(0, 0, WidthTiles, HeightTiles);

    public bool IsInBounds(TilePos position)
    {
        return Bounds.Contains(position);
    }

    public bool TryGetSpawn(string id, out MapSpawnPointDefinition spawn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        spawn = SpawnPoints.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))!;
        return spawn is not null;
    }

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
