using Game.Core.World;

namespace Game.Core.Spawning;

public enum SpawnActivitySourceKind
{
    Player,
    Camera
}

public readonly record struct SpawnActivitySource(
    int Id,
    SpawnActivitySourceKind Kind,
    TilePos CenterTile,
    RectI VisibleTileBounds,
    SpawnEnvironment Environment)
{
    public static SpawnActivitySource ForPlayer(
        int id,
        TilePos centerTile,
        RectI visibleTileBounds,
        SpawnEnvironment environment = default)
    {
        return new SpawnActivitySource(
            id,
            SpawnActivitySourceKind.Player,
            centerTile,
            visibleTileBounds,
            Normalize(environment));
    }

    public static SpawnActivitySource ForCamera(
        int id,
        TilePos centerTile,
        RectI visibleTileBounds,
        SpawnEnvironment environment = default)
    {
        return new SpawnActivitySource(
            id,
            SpawnActivitySourceKind.Camera,
            centerTile,
            visibleTileBounds,
            Normalize(environment));
    }

    private static SpawnEnvironment Normalize(SpawnEnvironment environment)
    {
        return environment.IsSpecified ? environment : SpawnEnvironment.Default;
    }
}
