namespace Game.Core.Loot;

public sealed record LootTableDefinition
{
    public required string Id { get; init; }

    public required IReadOnlyList<LootEntryDefinition> Entries { get; init; }
}
