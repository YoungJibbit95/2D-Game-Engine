namespace Game.Core.World.Generation;

public sealed record WorldDimensionDefinition
{
    public required string Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public int MinTileY { get; init; }

    public int MaxTileYInclusive { get; init; }

    public ushort SurfaceTileId { get; init; } = KnownTileIds.Grass;

    public ushort SubsurfaceTileId { get; init; } = KnownTileIds.Dirt;

    public ushort FillTileId { get; init; } = KnownTileIds.Stone;

    public float CaveMultiplier { get; init; } = 1f;

    public float OreMultiplier { get; init; } = 1f;

    public byte AmbientLight { get; init; } = 32;

    public bool AllowsSurfaceTrees { get; init; }

    public bool Contains(int tileY)
    {
        return tileY >= MinTileY && tileY <= MaxTileYInclusive;
    }
}
