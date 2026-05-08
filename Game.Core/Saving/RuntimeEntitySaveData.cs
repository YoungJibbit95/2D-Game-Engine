namespace Game.Core.Saving;

public sealed record RuntimeEntitySaveData
{
    public required RuntimeEntityKind Kind { get; init; }

    public required int RuntimeId { get; init; }

    public required string DefinitionId { get; init; }

    public required float PositionX { get; init; }

    public required float PositionY { get; init; }

    public float VelocityX { get; init; }

    public float VelocityY { get; init; }

    public int Health { get; init; }

    public float Age { get; init; }

    public int Pierce { get; init; }

    public int? OwnerEntityId { get; init; }

    public int StackCount { get; init; }
}
