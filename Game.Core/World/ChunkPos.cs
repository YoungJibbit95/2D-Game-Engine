namespace Game.Core.World;

public readonly record struct ChunkPos(int X, int Y)
{
    public static ChunkPos Zero { get; } = new(0, 0);

    public override string ToString()
    {
        return $"ChunkPos({X}, {Y})";
    }
}
