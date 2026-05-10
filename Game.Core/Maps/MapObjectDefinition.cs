using Game.Core.World;

namespace Game.Core.Maps;

public sealed record MapObjectDefinition
{
    public required string Id { get; init; }

    public MapObjectKind Kind { get; init; }

    public int TileX { get; init; }

    public int TileY { get; init; }

    public int Width { get; init; } = 1;

    public int Height { get; init; } = 1;

    public bool BlocksMovement { get; init; }

    public bool IsInteractable { get; init; }

    public string? InteractionId { get; init; }

    public string? TargetMapId { get; init; }

    public string? TargetSpawnId { get; init; }

    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public RectI Bounds => new(TileX, TileY, Width, Height);
}
