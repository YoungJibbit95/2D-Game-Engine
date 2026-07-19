namespace Game.Core.Saving;

public sealed record WorldSaveMetadata
{
    public int FormatVersion { get; init; } = 3;

    public int GenerationVersion { get; init; } = Game.Core.World.Generation.WorldGenerationVersions.Legacy;

    public string GenerationProfileId { get; init; } = string.Empty;

    public required string Name { get; init; }

    public required int Seed { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required int WidthTiles { get; init; }

    public required int HeightTiles { get; init; }

    public bool IsHorizontallyInfinite { get; init; }

    public string ChunkStorageMode { get; init; } = WorldChunkStorageMode.LooseFiles.ToString();

    public int SpawnTileX { get; init; }

    public int SpawnTileY { get; init; }
}
