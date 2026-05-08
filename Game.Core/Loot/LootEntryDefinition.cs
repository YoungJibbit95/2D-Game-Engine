namespace Game.Core.Loot;

public sealed record LootEntryDefinition
{
    public required string ItemId { get; init; }

    public required int Min { get; init; }

    public required int Max { get; init; }

    public required float Chance { get; init; }
}
