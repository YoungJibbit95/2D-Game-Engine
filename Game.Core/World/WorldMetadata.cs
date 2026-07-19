using Game.Core.World.Generation;

namespace Game.Core.World;

public sealed record WorldMetadata(
    string Name,
    int Seed,
    DateTimeOffset CreatedAtUtc,
    TilePos SpawnTile = default)
{
    public int GenerationVersion { get; init; } = WorldGenerationVersions.Current;

    public string GenerationProfileId { get; init; } = string.Empty;

    public static WorldMetadata CreateDefault(int seed)
    {
        return new WorldMetadata("New World", seed, DateTimeOffset.UtcNow);
    }
}
