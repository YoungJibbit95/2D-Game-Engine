namespace Game.Core.World;

public readonly record struct TilePos(int X, int Y)
{
    public static TilePos Zero { get; } = new(0, 0);

    public override string ToString()
    {
        return $"TilePos({X}, {Y})";
    }
}
