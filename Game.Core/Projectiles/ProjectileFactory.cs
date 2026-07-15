using System.Numerics;
using Game.Core.Combat;
using Game.Core.Entities;

namespace Game.Core.Projectiles;

public sealed class ProjectileFactory
{
    public ProjectileEntity Create(
        ProjectileDefinition definition,
        Vector2 position,
        Vector2 direction,
        int? ownerEntityId = null,
        int? damageOverride = null,
        DamageType? damageTypeOverride = null,
        EntityFaction ownerFaction = EntityFaction.Friendly)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (direction == Vector2.Zero)
        {
            direction = Vector2.UnitX;
        }

        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        direction = Vector2.Normalize(direction);

        var runtimeDefinition = damageOverride is null && damageTypeOverride is null
            ? definition
            : definition with
            {
                Damage = damageOverride ?? definition.Damage,
                DamageType = damageTypeOverride ?? definition.DamageType
            };
        return new ProjectileEntity(
            runtimeDefinition,
            position,
            direction * definition.Speed,
            ownerEntityId,
            ownerFaction);
    }
}
