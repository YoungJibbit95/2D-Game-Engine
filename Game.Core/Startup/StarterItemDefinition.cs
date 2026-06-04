namespace Game.Core.Startup;

public sealed record StarterItemDefinition
{
    public required string ItemId { get; init; }

    public int Count { get; init; } = 1;

    public StarterInventoryTarget Target { get; init; }

    public int? Slot { get; init; }

    public int SortOrder { get; init; }
}
