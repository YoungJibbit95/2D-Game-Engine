namespace Game.Core.Loot;

public sealed record LootTableDefinition
{
    public required string Id { get; init; }

    public required IReadOnlyList<LootEntryDefinition> Entries { get; init; }

    public int WeightedRolls { get; init; } = 1;

    public bool AllowDuplicateWeightedEntries { get; init; }
}
