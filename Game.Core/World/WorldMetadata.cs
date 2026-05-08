namespace Game.Core.World;

public sealed record WorldMetadata(
    string Name,
    int Seed,
    DateTimeOffset CreatedAtUtc,
    TilePos SpawnTile = default)
{
    public static WorldMetadata CreateDefault(int seed)
    {
        return new WorldMetadata("New World", seed, DateTimeOffset.UtcNow);
    }
}
