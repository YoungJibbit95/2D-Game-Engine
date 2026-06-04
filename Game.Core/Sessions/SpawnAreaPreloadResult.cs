namespace Game.Core.Sessions;

public sealed record SpawnAreaPreloadResult(int LoadedFromSave, int Generated)
{
    public static SpawnAreaPreloadResult Empty { get; } = new(0, 0);
}
