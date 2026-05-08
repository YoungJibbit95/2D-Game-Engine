namespace Game.Core.World.Structures;

public readonly record struct StructurePlacementResult(bool Placed, int TilesWritten)
{
    public static StructurePlacementResult Failed { get; } = new(false, 0);
}
