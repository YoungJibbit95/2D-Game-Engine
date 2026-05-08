using Game.Core.Biomes;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Time;
using Game.Core.World;

namespace Game.Core.Spawning;

public sealed class SpawnScheduler
{
    private readonly Random _random;
    private readonly SpawnSystem _spawnSystem;
    private float _timeUntilNextAttempt;

    public SpawnScheduler(Random random, SpawnSystem? spawnSystem = null)
    {
        _random = random;
        _spawnSystem = spawnSystem ?? new SpawnSystem(random);
    }

    public SpawnScheduler()
        : this(new Random())
    {
    }

    public SpawnSchedulerResult Update(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        BiomeMap biomeMap,
        WorldTime time,
        TilePos playerTile,
        float deltaSeconds,
        SpawnSchedulerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(biomeMap);
        ArgumentNullException.ThrowIfNull(time);

        var resolvedOptions = options ?? SpawnSchedulerOptions.Default;
        ValidateOptions(resolvedOptions);

        var despawned = DespawnFarEnemies(entities, playerTile, resolvedOptions.DespawnDistanceTiles);
        if (deltaSeconds <= 0)
        {
            return new SpawnSchedulerResult(0, 0, despawned);
        }

        _timeUntilNextAttempt -= deltaSeconds;
        if (_timeUntilNextAttempt > 0)
        {
            return new SpawnSchedulerResult(0, 0, despawned);
        }

        _timeUntilNextAttempt += resolvedOptions.SpawnIntervalSeconds;
        if (CountActiveEnemies(entities) >= resolvedOptions.MaxTotalActiveEnemies)
        {
            return new SpawnSchedulerResult(0, 0, despawned);
        }

        var attempts = 0;
        var spawned = 0;
        for (var i = 0; i < resolvedOptions.AttemptsPerInterval; i++)
        {
            if (CountActiveEnemies(entities) >= resolvedOptions.MaxTotalActiveEnemies)
            {
                break;
            }

            if (!TryPickCandidate(world, playerTile, resolvedOptions, out var candidate))
            {
                continue;
            }

            attempts++;
            var result = _spawnSystem.TrySpawn(world, entities, content, biomeMap, time, candidate);
            if (result.Spawned)
            {
                spawned++;
            }
        }

        return new SpawnSchedulerResult(attempts, spawned, despawned);
    }

    private bool TryPickCandidate(World.World world, TilePos playerTile, SpawnSchedulerOptions options, out TilePos candidate)
    {
        var offset = _random.Next(options.MinDistanceTiles, options.MaxDistanceTiles + 1);
        if (_random.Next(0, 2) == 0)
        {
            offset = -offset;
        }

        var x = Math.Clamp(playerTile.X + offset, 0, world.WidthTiles - 1);
        var minY = Math.Max(0, playerTile.Y - options.VerticalSearchRadiusTiles);
        var maxY = Math.Min(world.HeightTiles - 2, playerTile.Y + options.VerticalSearchRadiusTiles);

        for (var y = minY; y <= maxY; y++)
        {
            if (!world.IsSolid(x, y) && world.IsSolid(x, y + 1))
            {
                candidate = new TilePos(x, y);
                return true;
            }
        }

        candidate = default;
        return false;
    }

    private static int DespawnFarEnemies(EntityManager entities, TilePos playerTile, int despawnDistanceTiles)
    {
        var maxDistanceSquared = despawnDistanceTiles * despawnDistanceTiles;
        var despawned = 0;

        foreach (var enemy in entities.Entities.OfType<EnemyEntity>().Where(enemy => enemy.IsActive).ToArray())
        {
            var enemyTile = CoordinateUtils.WorldToTile(enemy.Body.Center.X, enemy.Body.Center.Y);
            var dx = enemyTile.X - playerTile.X;
            var dy = enemyTile.Y - playerTile.Y;
            if (dx * dx + dy * dy <= maxDistanceSquared)
            {
                continue;
            }

            entities.Remove(enemy);
            despawned++;
        }

        return despawned;
    }

    private static int CountActiveEnemies(EntityManager entities)
    {
        return entities.Entities.OfType<EnemyEntity>().Count(enemy => enemy.IsActive);
    }

    private static void ValidateOptions(SpawnSchedulerOptions options)
    {
        if (options.SpawnIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn interval must be greater than zero.");
        }

        if (options.AttemptsPerInterval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Attempts per interval must be greater than zero.");
        }

        if (options.MinDistanceTiles < 0 || options.MaxDistanceTiles < options.MinDistanceTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn distance range is invalid.");
        }

        if (options.VerticalSearchRadiusTiles < 0 || options.DespawnDistanceTiles <= 0 || options.MaxTotalActiveEnemies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn scheduler distances and caps must be non-negative.");
        }
    }
}
