using System.Numerics;

namespace Game.Core.Projectiles;

public sealed class ProjectileFactory
{
    public ProjectileEntity Create(
        ProjectileDefinition definition,
        Vector2 position,
        Vector2 direction,
        int? ownerEntityId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (direction == Vector2.Zero)
        {
            direction = Vector2.UnitX;
        }

        direction = Vector2.Normalize(direction);

        return new ProjectileEntity(
            definition.Id,
            position,
            direction * definition.Speed,
            definition.Damage,
            definition.Gravity,
            definition.Pierce,
            definition.Lifetime,
            ownerEntityId);
    }
}
