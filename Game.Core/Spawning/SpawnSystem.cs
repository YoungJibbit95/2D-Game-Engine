using Game.Core.Biomes;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.Time;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Spawning;

public sealed class SpawnSystem
{
    private readonly Random _random;
    private readonly EntityFactory _entityFactory;

    public SpawnSystem(Random random, EntityFactory entityFactory)
    {
        _random = random;
        _entityFactory = entityFactory;
    }

    public SpawnSystem(Random random)
        : this(random, new EntityFactory(new TileCollisionResolver()))
    {
    }

    public SpawnAttemptResult TrySpawn(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        BiomeMap biomeMap,
        WorldTime time,
        TilePos candidateTile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(biomeMap);
        ArgumentNullException.ThrowIfNull(time);

        if (!IsValidSpawnTile(world, candidateTile))
        {
            return SpawnAttemptResult.None;
        }

        var biomeId = biomeMap.GetBiomeAt(candidateTile.X, candidateTile.Y);
        foreach (var rule in content.SpawnRules.Definitions)
        {
            if (!CanRuleSpawn(rule, entities, biomeId, time, candidateTile))
            {
                continue;
            }

            if (_random.NextSingle() > rule.Chance)
            {
                continue;
            }

            var definition = content.Entities.GetById(rule.EntityId);
            var entity = _entityFactory.CreateEnemy(
                definition,
                new Vector2(candidateTile.X * GameConstants.TileSize, candidateTile.Y * GameConstants.TileSize));
            entities.Add(entity);
            return new SpawnAttemptResult(true, rule.Id, entity);
        }

        return SpawnAttemptResult.None;
    }

    private static bool CanRuleSpawn(
        SpawnRuleDefinition rule,
        EntityManager entities,
        string biomeId,
        WorldTime time,
        TilePos candidateTile)
    {
        if (!string.IsNullOrWhiteSpace(rule.BiomeId) &&
            !string.Equals(rule.BiomeId, biomeId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.RequiresNight is not null && rule.RequiresNight.Value != time.IsNight)
        {
            return false;
        }

        if (rule.MinTileY is not null && candidateTile.Y < rule.MinTileY)
        {
            return false;
        }

        if (rule.MaxTileY is not null && candidateTile.Y > rule.MaxTileY)
        {
            return false;
        }

        var activeCount = entities.Entities
            .OfType<EnemyEntity>()
            .Count(entity => entity.IsActive && string.Equals(entity.DefinitionId, rule.EntityId, StringComparison.OrdinalIgnoreCase));

        return activeCount < rule.MaxActive;
    }

    private static bool IsValidSpawnTile(World.World world, TilePos candidateTile)
    {
        return world.IsInBounds(candidateTile.X, candidateTile.Y) &&
               world.IsInBounds(candidateTile.X, candidateTile.Y + 1) &&
               !world.IsSolid(candidateTile.X, candidateTile.Y) &&
               world.IsSolid(candidateTile.X, candidateTile.Y + 1);
    }
}
