using Game.Core.Data;

namespace Game.Core.Tiles;

public sealed record TileDefinition
{
    public required ushort NumericId { get; init; }

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string TexturePath { get; init; }

    public bool Solid { get; init; }

    public bool BlocksLight { get; init; }

    public float Hardness { get; init; }

    public int MiningPowerRequired { get; init; }

    public string? DropItemId { get; init; }

    public string? MergeGroup { get; init; }

    public string? CraftingStationId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public bool HasTag(string tag)
    {
        return DefinitionTags.HasTag(Tags, tag);
    }
}
