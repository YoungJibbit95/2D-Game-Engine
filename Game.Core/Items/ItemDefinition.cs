namespace Game.Core.Items;

public sealed record ItemDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public ItemType Type { get; init; }

    public required string TexturePath { get; init; }

    public int MaxStack { get; init; } = 1;

    public float UseTime { get; init; }

    public int Damage { get; init; }

    public int ToolPower { get; init; }

    public float Knockback { get; init; }

    public string? PlacesTileId { get; init; }
}
