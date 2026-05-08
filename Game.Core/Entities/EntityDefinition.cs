namespace Game.Core.Entities;

public sealed record EntityDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string TexturePath { get; init; }

    public required int MaxHealth { get; init; }

    public float Width { get; init; } = 16;

    public float Height { get; init; } = 16;

    public string? AiBehavior { get; init; }

    public string? LootTableId { get; init; }
}
