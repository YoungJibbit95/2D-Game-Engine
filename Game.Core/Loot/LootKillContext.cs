using Game.Core.Combat;
using Game.Core.Entities;

namespace Game.Core.Loot;

public readonly record struct LootKillContext(
    int? KillerEntityId,
    EntityFaction? KillerFaction,
    DamageType? DamageType,
    bool? IsNight,
    int? VictimDepth,
    IReadOnlyList<string> VictimTags)
{
    public float QuantityMultiplier { get; init; } = 1f;

    public float RareChanceMultiplier { get; init; } = 1f;

    public static LootKillContext Empty { get; } = new(null, null, null, null, null, Array.Empty<string>());
}
