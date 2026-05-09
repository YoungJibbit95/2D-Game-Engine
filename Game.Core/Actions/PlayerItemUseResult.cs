using Game.Core.Combat;
using Game.Core.Interaction;
using Game.Core.Projectiles;

namespace Game.Core.Actions;

public readonly record struct PlayerItemUseResult(
    PlayerItemUseKind Kind,
    MiningResult Mining,
    bool PlacedTile,
    MeleeAttackResult Melee,
    ProjectileEntity? Projectile = null)
{
    public static PlayerItemUseResult None { get; } = new(
        PlayerItemUseKind.None,
        MiningResult.None,
        false,
        MeleeAttackResult.None,
        null);
}
