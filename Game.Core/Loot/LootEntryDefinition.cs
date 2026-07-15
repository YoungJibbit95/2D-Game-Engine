using Game.Core.Combat;
using Game.Core.Entities;

namespace Game.Core.Loot;

public sealed record LootEntryDefinition
{
    public required string ItemId { get; init; }

    public required int Min { get; init; }

    public required int Max { get; init; }

    public float Chance { get; init; } = 1f;

    public bool Guaranteed { get; init; }

    public bool IsRare { get; init; }

    public float Weight { get; init; }

    public EntityFaction? RequiredKillerFaction { get; init; }

    public IReadOnlyList<DamageType> DamageTypes { get; init; } = Array.Empty<DamageType>();

    public bool? RequiresNight { get; init; }

    public int? MinVictimDepth { get; init; }

    public int? MaxVictimDepth { get; init; }

    public IReadOnlyList<string> RequiredVictimTags { get; init; } = Array.Empty<string>();
}
