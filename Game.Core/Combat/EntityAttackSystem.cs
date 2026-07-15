using Game.Core.Entities;
using Game.Core.Entities.AI.Sensing;
using System.Numerics;

namespace Game.Core.Combat;

public sealed class EntityAttackSystem
{
    public EntityAttackResolution ResolvePendingAttacks(
        EntityManager entities,
        PlayerEntity? primaryPlayer = null,
        float playerInvulnerabilitySeconds = 0.65f)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (playerInvulnerabilitySeconds < 0 || !float.IsFinite(playerInvulnerabilitySeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(playerInvulnerabilitySeconds));
        }

        var intentsConsumed = 0;
        var hitsApplied = 0;
        var damageApplied = 0;
        var deaths = 0;
        for (var index = 0; index < entities.Entities.Count; index++)
        {
            if (entities.Entities[index] is not EnemyEntity { IsActive: true } attacker ||
                !attacker.TryConsumeAttackIntent(out var intent))
            {
                continue;
            }

            intentsConsumed++;
            var target = FindTarget(entities.Entities, primaryPlayer, intent.TargetEntityId);
            if (!IsValidIntent(attacker, target, intent))
            {
                continue;
            }

            var targetCenter = DistanceSensor.GetCenter(target!);
            var direction = targetCenter - attacker.Body.Center;
            if (direction == Vector2.Zero)
            {
                direction = Vector2.UnitX;
            }

            var damage = new DamageInfo(
                intent.Damage,
                DamageType.Melee,
                attacker.Id,
                direction,
                intent.Knockback);
            var applied = target switch
            {
                PlayerEntity player => player.ApplyDamage(damage, playerInvulnerabilitySeconds),
                EnemyEntity enemy => enemy.ApplyDamage(damage),
                _ => false
            };

            if (!applied)
            {
                continue;
            }

            hitsApplied++;
            damageApplied += intent.Damage;
            if (IsDead(target!))
            {
                deaths++;
            }
        }

        return new EntityAttackResolution(intentsConsumed, hitsApplied, damageApplied, deaths);
    }

    private static Entity? FindTarget(IReadOnlyList<Entity> entities, PlayerEntity? player, int id)
    {
        if (player is { IsActive: true } && player.Id == id)
        {
            return player;
        }

        for (var index = 0; index < entities.Count; index++)
        {
            if (entities[index].Id == id)
            {
                return entities[index];
            }
        }

        return null;
    }

    private static bool IsValidIntent(EnemyEntity attacker, Entity? target, Entities.AI.AiAttackIntent intent)
    {
        if (target is not { IsActive: true } ||
            intent.AttackerEntityId != attacker.Id ||
            intent.Damage <= 0 ||
            intent.Range < 0 ||
            attacker.GetDispositionToward(target) != EntityDisposition.Hostile)
        {
            return false;
        }

        var distanceSquared = Vector2.DistanceSquared(attacker.Body.Center, DistanceSensor.GetCenter(target));
        return distanceSquared <= intent.Range * intent.Range;
    }

    private static bool IsDead(Entity entity)
    {
        return entity switch
        {
            PlayerEntity player => player.HealthComponent.IsDead,
            EnemyEntity enemy => enemy.Health.IsDead,
            _ => false
        };
    }
}
