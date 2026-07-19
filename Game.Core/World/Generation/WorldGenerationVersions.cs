namespace Game.Core.World.Generation;

public static class WorldGenerationVersions
{
    public const int Legacy = 1;
    public const int TerrariaTopology = 2;
    public const int Current = TerrariaTopology;

    public static int Normalize(int version)
    {
        return version <= Legacy ? Legacy : Current;
    }
}
