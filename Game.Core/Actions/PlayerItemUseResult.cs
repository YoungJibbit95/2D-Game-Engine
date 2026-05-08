using Game.Core.Combat;
using Game.Core.Interaction;

namespace Game.Core.Actions;

public readonly record struct PlayerItemUseResult(
    PlayerItemUseKind Kind,
    MiningResult Mining,
    bool PlacedTile,
    MeleeAttackResult Melee)
{
    public static PlayerItemUseResult None { get; } = new(
        PlayerItemUseKind.None,
        MiningResult.None,
        false,
        MeleeAttackResult.None);
}
