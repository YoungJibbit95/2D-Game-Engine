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
            CreateAiBehavior(definition.AiBehavior),
            _collisionResolver,
            definition.LootTableId);
    }

    private static IAiBehavior CreateAiBehavior(string? aiBehavior)
    {
        return aiBehavior?.ToLowerInvariant() switch
        {
            "slime" => new SlimeAiBehavior(),
            _ => NullAiBehavior.Instance
        };
    }
}
