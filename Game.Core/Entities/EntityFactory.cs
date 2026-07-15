using Game.Core.Combat;
using Game.Core.Entities.AI;
using Game.Core.Physics;
using System.Numerics;

namespace Game.Core.Entities;

public sealed class EntityFactory
{
    private readonly TileCollisionResolver _collisionResolver;

    public EntityFactory(TileCollisionResolver collisionResolver)
    {
        _collisionResolver = collisionResolver;
    }

    public EnemyEntity CreateEnemy(EntityDefinition definition, Vector2 spawnPosition, int? currentHealth = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new EnemyEntity(
            definition.Id,
            spawnPosition,
            new Vector2(definition.Width, definition.Height),
            new HealthComponent(definition.MaxHealth, currentHealth),
            CreateAiBehavior(definition),
            _collisionResolver,
            definition.LootTableId,
            definition.ContactDamage,
            definition.ContactKnockback,
            definition.AttackDamage,
            definition.AttackKnockback,
            definition.Despawn,
            definition.OnContactEffects,
            definition.Faction,
            definition.MovementMode,
            definition.Tags);
    }

    private static IAiBehavior CreateAiBehavior(EntityDefinition definition)
    {
        var profile = definition.Ai ?? CreateLegacyProfile(definition.AiBehavior);
        return profile.Kind switch
        {
            AiBehaviorKind.Slime => new SlimeAiBehavior(),
            AiBehaviorKind.Critter or AiBehaviorKind.Wander => new CritterAiBehavior(profile),
            AiBehaviorKind.Hostile => new HostileAiBehavior(profile),
            _ => NullAiBehavior.Instance
        };
    }

    private static AiProfileDefinition CreateLegacyProfile(string? aiBehavior)
    {
        return aiBehavior?.ToLowerInvariant() switch
        {
            "slime" => new AiProfileDefinition { Kind = AiBehaviorKind.Slime },
            "critter" or "wander" or "flee" => new AiProfileDefinition { Kind = AiBehaviorKind.Critter },
            "hostile" or "patrol" or "chase" => new AiProfileDefinition { Kind = AiBehaviorKind.Hostile },
            _ => new AiProfileDefinition { Kind = AiBehaviorKind.None }
        };
    }
}
