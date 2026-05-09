namespace Game.Core.World.Generation;

public sealed record OreGenerationDefinition
{
    public ushort TileId { get; init; }

    public int VeinCount { get; init; }

    public int MinDepthOffset { get; init; } = 8;

    public int MaxDepthOffset { get; init; }

    public int MinLength { get; init; } = 5;

    public int MaxLength { get; init; } = 12;

    public int Radius { get; init; } = 2;

    public ushort ReplaceTileId { get; init; } = KnownTileIds.Stone;

    public bool CanGenerate => TileId != 0 && VeinCount > 0;
}
