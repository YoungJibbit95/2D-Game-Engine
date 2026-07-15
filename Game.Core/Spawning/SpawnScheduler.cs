using Game.Core.Biomes;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Randomness;
using Game.Core.Time;
using Game.Core.World;
using AI = Game.Core.Entities.AI;

namespace Game.Core.Spawning;

public sealed class SpawnScheduler
{
    private readonly Random _random;
    private readonly SpawnSystem _spawnSystem;
    private float _timeUntilNextAttempt;
    private ulong _candidateSequence;

    public SpawnScheduler(Random random, SpawnSystem? spawnSystem = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
        _spawnSystem = spawnSystem ?? new SpawnSystem(random);
    }

    public SpawnScheduler(DeterministicRandomStream candidateStream, SpawnSystem spawnSystem)
        : this(new DeterministicRandomAdapter(candidateStream), spawnSystem)
    {
    }

    public SpawnScheduler(
        DeterministicRandomStream candidateStream,
        DeterministicRandomStream ruleStream)
        : this(candidateStream, new SpawnSystem(ruleStream))
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
        ArgumentNullException.ThrowIfNull(biomeMap);
        var source = SpawnActivitySource.ForPlayer(
            0,
            playerTile,
            default,
            SpawnEnvironment.Default);
        return UpdateCore(
            world,
            entities,
            content,
            biomeMap,
            null,
            time,
            null,
            source,
            1,
            false,
            deltaSeconds,
            options);
    }

    public SpawnSchedulerResult Update(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        string biomeId,
        WorldTime time,
        TilePos playerTile,
        float deltaSeconds,
        SpawnSchedulerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(biomeId);
        var source = SpawnActivitySource.ForPlayer(
            0,
            playerTile,
            default,
            new SpawnEnvironment(BiomeId: biomeId));
        return UpdateCore(
            world,
            entities,
            content,
            null,
            biomeId,
            time,
            null,
            source,
            1,
            false,
            deltaSeconds,
            options);
    }

    public SpawnSchedulerResult Update(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        BiomeMap biomeMap,
        WorldTime time,
        IReadOnlyList<SpawnActivitySource> activitySources,
        float deltaSeconds,
        SpawnSchedulerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(biomeMap);
        ArgumentNullException.ThrowIfNull(activitySources);
        return UpdateCore(
            world,
            entities,
            content,
            biomeMap,
            null,
            time,
            activitySources,
            default,
            activitySources.Count,
            true,
            deltaSeconds,
            options);
    }

    private SpawnSchedulerResult UpdateCore(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        BiomeMap? biomeMap,
        string? biomeId,
        WorldTime time,
        IReadOnlyList<SpawnActivitySource>? activitySources,
        SpawnActivitySource singleSource,
        int sourceCount,
        bool useImplicitVisibleBounds,
        float deltaSeconds,
        SpawnSchedulerOptions? options)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(time);

        var resolvedOptions = options ?? SpawnSchedulerOptions.Default;
        ValidateOptions(resolvedOptions);
        ValidateSources(activitySources, singleSource, sourceCount);
        _spawnSystem.AdvanceCooldowns(deltaSeconds);
        if (sourceCount == 0)
        {
            return SpawnSchedulerResult.None;
        }

        var despawned = DespawnFarEnemies(
            entities,
            activitySources,
            singleSource,
            sourceCount,
            resolvedOptions.DespawnDistanceTiles);
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
        var activeEnemies = CountActiveEnemies(entities);
        if (activeEnemies >= resolvedOptions.MaxTotalActiveEnemies)
        {
            return new SpawnSchedulerResult(0, 0, despawned);
        }

        var attempts = 0;
        var spawned = 0;
        for (var attempt = 0; attempt < resolvedOptions.AttemptsPerInterval; attempt++)
        {
            if (activeEnemies >= resolvedOptions.MaxTotalActiveEnemies)
            {
                break;
            }

            var target = PickTarget(
                world,
                activitySources,
                singleSource,
                sourceCount,
                resolvedOptions,
                out var sourceIndex);
            attempts++;
            var result = _spawnSystem.TrySpawn(
                world,
                entities,
                content,
                time,
                new SpawnAttemptContext(
                    target,
                    biomeMap,
                    biomeId,
                    activitySources,
                    singleSource,
                    sourceIndex,
                    sourceCount,
                    useImplicitVisibleBounds,
                    resolvedOptions));
            if (!result.Spawned)
            {
                continue;
            }

            spawned++;
            activeEnemies++;
        }

        return new SpawnSchedulerResult(attempts, spawned, despawned);
    }

    private TilePos PickTarget(
        World.World world,
        IReadOnlyList<SpawnActivitySource>? activitySources,
        SpawnActivitySource singleSource,
        int sourceCount,
        SpawnSchedulerOptions options,
        out int sourceIndex)
    {
        var sequence = _candidateSequence++;
        sourceIndex = (int)(sequence % (ulong)sourceCount);
        var source = GetSource(activitySources, singleSource, sourceIndex);
        var sector = (int)((sequence / (ulong)sourceCount) % (ulong)options.SectorCount);
        var angle = (sector + _random.NextDouble()) * (Math.Tau / options.SectorCount);
        var distance = _random.NextInt64(options.MinDistanceTiles, (long)options.MaxDistanceTiles + 1);
        var verticalRange = Math.Min(distance, options.VerticalSearchRadiusTiles);
        var verticalOffset = (long)Math.Round(Math.Sin(angle) * verticalRange);
        var horizontalMagnitude = Math.Sqrt(Math.Max(0, distance * distance - verticalOffset * verticalOffset));
        var horizontalOffset = (long)Math.Round(Math.CopySign(horizontalMagnitude, Math.Cos(angle)));
        var x = Saturate((long)source.CenterTile.X + horizontalOffset);
        var y = Saturate((long)source.CenterTile.Y + verticalOffset);
        if (!world.IsHorizontallyInfinite)
        {
            x = Math.Clamp(x, 0, world.WidthTiles - 1);
        }

        y = Math.Clamp(y, 0, world.HeightTiles - 1);
        return new TilePos(x, y);
    }

    private static int DespawnFarEnemies(
        EntityManager entities,
        IReadOnlyList<SpawnActivitySource>? activitySources,
        SpawnActivitySource singleSource,
        int sourceCount,
        int despawnDistanceTiles)
    {
        var maxDistanceSquared = (double)despawnDistanceTiles * despawnDistanceTiles;
        var despawned = 0;
        for (var entityIndex = entities.Entities.Count - 1; entityIndex >= 0; entityIndex--)
        {
            if (entities.Entities[entityIndex] is not EnemyEntity { IsActive: true } enemy ||
                IsProtectedOrEngaged(enemy))
            {
                continue;
            }

            var enemyTile = CoordinateUtils.WorldToTile(enemy.Body.Center.X, enemy.Body.Center.Y);
            var nearActivity = false;
            for (var sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
            {
                var source = GetSource(activitySources, singleSource, sourceIndex);
                var dx = (double)enemyTile.X - source.CenterTile.X;
                var dy = (double)enemyTile.Y - source.CenterTile.Y;
                if (dx * dx + dy * dy <= maxDistanceSquared)
                {
                    nearActivity = true;
                    break;
                }
            }

            if (nearActivity)
            {
                continue;
            }

            entities.Remove(enemy);
            despawned++;
        }

        return despawned;
    }

    private static bool IsProtectedOrEngaged(EnemyEntity enemy)
    {
        if (enemy.DespawnPolicy.Mode == EntityDespawnMode.Never ||
            enemy.DespawnProtectionRemaining > 0 ||
            enemy.TargetEntityId is not null)
        {
            return true;
        }

        return enemy.DespawnPolicy.Mode == EntityDespawnMode.WhenIdle &&
               enemy.AiState is not (Game.Core.Entities.AI.AiState.Idle or
                   Game.Core.Entities.AI.AiState.Wander or
                   Game.Core.Entities.AI.AiState.Patrol or
                   Game.Core.Entities.AI.AiState.Flock or
                   Game.Core.Entities.AI.AiState.Perch);
    }

    private static int CountActiveEnemies(EntityManager entities)
    {
        var count = 0;
        for (var index = 0; index < entities.Entities.Count; index++)
        {
            if (entities.Entities[index] is EnemyEntity { IsActive: true })
            {
                count++;
            }
        }

        return count;
    }

    private static SpawnActivitySource GetSource(
        IReadOnlyList<SpawnActivitySource>? activitySources,
        SpawnActivitySource singleSource,
        int index)
    {
        return activitySources is null ? singleSource : activitySources[index];
    }

    private static void ValidateSources(
        IReadOnlyList<SpawnActivitySource>? activitySources,
        SpawnActivitySource singleSource,
        int sourceCount)
    {
        for (var index = 0; index < sourceCount; index++)
        {
            var source = GetSource(activitySources, singleSource, index);
            if (!float.IsFinite(source.Environment.DensityMultiplier) ||
                source.Environment.DensityMultiplier < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(activitySources),
                    $"Activity source {source.Id} has an invalid density multiplier.");
            }
        }
    }

    private static void ValidateOptions(SpawnSchedulerOptions options)
    {
        if (!float.IsFinite(options.SpawnIntervalSeconds) || options.SpawnIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn interval must be greater than zero.");
        }

        if (options.AttemptsPerInterval <= 0 || options.SectorCount < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn attempts and sector count are invalid.");
        }

        if (options.MinDistanceTiles < 0 || options.MaxDistanceTiles < options.MinDistanceTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn distance range is invalid.");
        }

        if (options.VerticalSearchRadiusTiles < 0 ||
            options.PlacementSearchRadiusTiles < 0 ||
            options.OnScreenHalfWidthTiles < 0 ||
            options.OnScreenHalfHeightTiles < 0 ||
            options.OnScreenExclusionPaddingTiles < 0 ||
            options.DespawnDistanceTiles <= 0 ||
            options.MaxTotalActiveEnemies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Spawn scheduler distances and caps are invalid.");
        }
    }

    private static int Saturate(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }
}
