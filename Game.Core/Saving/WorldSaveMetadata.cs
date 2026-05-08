namespace Game.Core.Saving;

public sealed record WorldSaveMetadata
{
    public int FormatVersion { get; init; } = 1;

    public required string Name { get; init; }

    public required int Seed { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required int WidthTiles { get; init; }

    public required int HeightTiles { get; init; }

    public int SpawnTileX { get; init; }

    public int SpawnTileY { get; init; }
}
