namespace Game.Core.World.TileEntities;

public abstract class TileEntity
{
    protected TileEntity(string typeId, TilePos position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        TypeId = typeId;
        Position = position;
    }

    public int RuntimeId { get; private set; }

    public string TypeId { get; }

    public TilePos Position { get; }

    public RectI TileBounds => new(Position.X, Position.Y, 1, 1);

    internal void AssignId(int id)
    {
        if (RuntimeId != 0 && RuntimeId != id)
        {
            throw new InvalidOperationException($"Tile entity already has id {RuntimeId}.");
        }

        RuntimeId = id;
    }
}
