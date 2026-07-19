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
    private readonly EncounterPlanner _encounterPlanner;
    private readonly SpawnIngressController _ingressController = new();
    private float _timeUntilNextAttempt;
    private ulong _candidateSequence;
    private int _warmStartCyclesRemaining = -1;

    public SpawnScheduler(
        Random random,
        SpawnSystem? spawnSystem = null,
        EncounterPlanner? encounterPlanner = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
        _spawnSystem = spawnSystem ?? new SpawnSystem(random);
        _encounterPlanner = encounterPlanner ?? new EncounterPlanner(random);
    }

    public SpawnScheduler(DeterministicRandomStream candidateStream, SpawnSystem spawnSystem)
        : this(
            new DeterministicRandomAdapter(candidateStream),
            spawnSystem,
            new EncounterPlanner(candidateStream))
    {
    }

    public SpawnScheduler(
        DeterministicRandomStream candidateStream,
        DeterministicRandomStream ruleStream)
        : this(candidateStream, ruleStream, candidateStream)
    {
    }

    public SpawnScheduler(
        DeterministicRandomStream candidateStream,
        DeterministicRandomStream ruleStream,
        DeterministicRandomStream encounterStream)
        : this(
            new DeterministicRandomAdapter(candidateStream),
            new SpawnSystem(ruleStream),
            new EncounterPlanner(encounterStream))
    {
    }

    public float GetEncounterCooldownRemaining(string encounterId)
    {
        return _encounterPlanner.GetCooldownRemaining(encounterId);
    }

    public int ActiveIngressCount => _ingressController.ActiveLeaseCount;

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
        _encounterPlanner.AdvanceCooldowns(deltaSeconds);
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
        _ingressController.Advance(
            world,
            entities,
            activitySources,
            singleSource,
            sourceCount,
            deltaSeconds,
            resolvedOptions);
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

        if (_warmStartCyclesRemaining < 0)
        {
            _warmStartCyclesRemaining = resolvedOptions.WarmStartAttemptCycles;
        }

        var useWarmStart = resolvedOptions.EnablePopulationWarmStart &&
                           resolvedOptions.SpawnIntervalSeconds < float.MaxValue &&
                           _warmStartCyclesRemaining > 0 &&
                           activeEnemies < resolvedOptions.WarmStartTargetPopulation;
        if (useWarmStart)
        {
            var nextInterval = Math.Min(
                resolvedOptions.SpawnIntervalSeconds,
                resolvedOptions.WarmStartIntervalSeconds);
            _timeUntilNextAttempt -= resolvedOptions.SpawnIntervalSeconds - nextInterval;
            _warmStartCyclesRemaining--;
        }

        var attempts = 0;
        var spawned = 0;
        if (useWarmStart)
        {
            TrySpawnWarmStart(
                world,
                entities,
                content,
                biomeMap,
                biomeId,
                time,
                activitySources,
                singleSource,
                sourceCount,
                useImplicitVisibleBounds,
                resolvedOptions,
                ref activeEnemies,
                ref attempts,
                ref spawned);
        }

        TrySpawnEncounter(
            world,
            entities,
            content,
            biomeMap,
            biomeId,
            time,
            activitySources,
            singleSource,
            sourceCount,
            useImplicitVisibleBounds,
            resolvedOptions,
            resolvedOptions.AttemptsPerInterval - attempts,
            ref activeEnemies,
            out var encounterAttempts,
            out var encounterSpawned);
        attempts += encounterAttempts;
        spawned += encounterSpawned;

        for (var attempt = attempts; attempt < resolvedOptions.AttemptsPerInterval; attempt++)
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

    private void TrySpawnWarmStart(
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
        SpawnSchedulerOptions options,
        ref int activeEnemies,
        ref int attempts,
        ref int spawned)
    {
        if (attempts >= options.AttemptsPerInterval ||
            activeEnemies >= options.MaxTotalActiveEnemies)
        {
            return;
        }

        var target = PickTarget(
            world,
            activitySources,
            singleSource,
            sourceCount,
            options,
            out var sourceIndex);
        attempts++;
        var result = _spawnSystem.TrySpawnWarmStart(
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
                options));
        if (!result.Spawned)
        {
            return;
        }

        var source = GetSource(activitySources, singleSource, sourceIndex);
        _ingressController.Track(result.Entity!, source, options);
        spawned++;
        activeEnemies++;
    }

    private void TrySpawnEncounter(
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
        SpawnSchedulerOptions options,
        int attemptBudget,
        ref int activeEnemies,
        out int attempts,
        out int spawned)
    {
        attempts = 0;
        spawned = 0;
        if (attemptBudget <= 0 || content.Encounters.Definitions.Count == 0)
        {
            return;
        }

        var firstSourceIndex = (int)(_candidateSequence % (ulong)sourceCount);
        for (var sourceOffset = 0; sourceOffset < sourceCount; sourceOffset++)
        {
            var sourceIndex = (firstSourceIndex + sourceOffset) % sourceCount;
            var source = GetSource(activitySources, singleSource, sourceIndex);
            var environment = ResolveEncounterEnvironment(world, biomeMap, biomeId, source);
            if (!_encounterPlanner.HasApplicableEncounter(content.Encounters, environment, time))
            {
                continue;
            }

            var plan = _encounterPlanner.TryPlan(
                content.Encounters,
                entities,
                environment,
                time,
                source.CenterTile);
            if (plan is null || !TryResolveEncounterOptions(options, plan.Definition, out var encounterOptions))
            {
                continue;
            }

            var intentCount = Math.Min(plan.Spawns.Count, attemptBudget);
            for (var intentIndex = 0;
                 intentIndex < intentCount && activeEnemies < options.MaxTotalActiveEnemies;
                 intentIndex++)
            {
                var target = PickTargetForSource(world, source, encounterOptions);
                attempts++;
                if (!_encounterPlanner.CanSpawnAt(plan.Definition, entities, target))
                {
                    continue;
                }

                var result = _spawnSystem.TrySpawnRule(
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
                        encounterOptions),
                    plan.Spawns[intentIndex].SpawnRuleId);
                if (!result.Spawned)
                {
                    continue;
                }

                result.Entity!.AssignSpawnEncounter(plan.Definition.Id);
                spawned++;
                activeEnemies++;
            }

            if (spawned > 0)
            {
                _encounterPlanner.CommitSpawned(plan);
            }

            return;
        }
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
        var sourceSequence = sequence / (ulong)sourceCount;
        return PickTargetForSource(world, source, sourceSequence, options);
    }

    private TilePos PickTargetForSource(
        World.World world,
        SpawnActivitySource source,
        SpawnSchedulerOptions options)
    {
        return PickTargetForSource(world, source, _candidateSequence++, options);
    }

    private TilePos PickTargetForSource(
        World.World world,
        SpawnActivitySource source,
        ulong sourceSequence,
        SpawnSchedulerOptions options)
    {
        if (ShouldPreferViewportIngress(source, sourceSequence, options) &&
            TryPickViewportIngressTarget(world, source, sourceSequence, options, out var ingressTarget))
        {
            return ingressTarget;
        }

        return PickRadialTarget(world, source, sourceSequence, options);
    }

    private static SpawnEnvironment ResolveEncounterEnvironment(
        World.World world,
        BiomeMap? biomeMap,
        string? explicitBiomeId,
        SpawnActivitySource source)
    {
        var environment = source.Environment;
        var resolvedBiomeId = !string.IsNullOrWhiteSpace(environment.BiomeId)
            ? environment.BiomeId
            : !string.IsNullOrWhiteSpace(explicitBiomeId)
                ? explicitBiomeId
                : biomeMap?.GetBiomeAt(source.CenterTile.X, source.CenterTile.Y);
        var layerId = !string.IsNullOrWhiteSpace(environment.VerticalLayerId)
            ? environment.VerticalLayerId
            : source.CenterTile.Y <= world.HeightTiles * 0.35f
                ? "surface"
                : source.CenterTile.Y >= world.HeightTiles * 0.55f
                    ? "cavern"
                    : "underground";
        return environment with
        {
            BiomeId = resolvedBiomeId,
            VerticalLayerId = layerId,
            IsSpecified = true
        };
    }

    private static bool TryResolveEncounterOptions(
        SpawnSchedulerOptions options,
        EncounterDefinition encounter,
        out SpawnSchedulerOptions resolved)
    {
        var minimumDistance = Math.Max(options.MinDistanceTiles, encounter.MinDistanceTiles);
        var maximumDistance = Math.Min(options.MaxDistanceTiles, encounter.MaxDistanceTiles);
        if (minimumDistance > maximumDistance)
        {
            resolved = options;
            return false;
        }

        resolved = options with
        {
            MinDistanceTiles = minimumDistance,
            MaxDistanceTiles = maximumDistance
        };
        return true;
    }

    private TilePos PickRadialTarget(
        World.World world,
        SpawnActivitySource source,
        ulong sourceSequence,
        SpawnSchedulerOptions options)
    {
        var sector = (int)(sourceSequence % (ulong)options.SectorCount);
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

    private static bool ShouldPreferViewportIngress(
        SpawnActivitySource source,
        ulong sourceSequence,
        SpawnSchedulerOptions options)
    {
        return options.PreferViewportIngress &&
               !source.VisibleTileBounds.IsEmpty &&
               options.ViewportIngressAttemptsPerCycle > 0 &&
               sourceSequence % (ulong)options.ViewportIngressAttemptCycle <
               (ulong)options.ViewportIngressAttemptsPerCycle;
    }

    private bool TryPickViewportIngressTarget(
        World.World world,
        SpawnActivitySource source,
        ulong sourceSequence,
        SpawnSchedulerOptions options,
        out TilePos target)
    {
        var spawnOnLeft = (sourceSequence & 1UL) == 0;
        var visible = source.VisibleTileBounds;
        var outsideX = spawnOnLeft
            ? (long)visible.Left - options.OnScreenExclusionPaddingTiles - 1
            : (long)visible.Right + options.OnScreenExclusionPaddingTiles;
        var distanceToOutside = Math.Abs(outsideX - source.CenterTile.X);
        var minimumDistance = Math.Max(
            (long)options.MinDistanceTiles + 2,
            distanceToOutside);
        var maximumDistance = Math.Min(
            options.MaxDistanceTiles,
            minimumDistance + options.ViewportIngressBandTiles);
        if (minimumDistance > maximumDistance)
        {
            target = default;
            return false;
        }

        var distance = _random.NextInt64(minimumDistance, maximumDistance + 1);
        var x = spawnOnLeft
            ? Saturate((long)source.CenterTile.X - distance)
            : Saturate((long)source.CenterTile.X + distance);
        if (!world.IsHorizontallyInfinite)
        {
            x = Math.Clamp(x, 0, world.WidthTiles - 1);
        }

        target = new TilePos(x, Math.Clamp(source.CenterTile.Y, 0, world.HeightTiles - 1));
        return true;
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

            entities.RemoveAt(entityIndex);
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
            options.WarmStartTargetPopulation < 0 ||
            options.WarmStartAttemptCycles < 0 ||
            !float.IsFinite(options.WarmStartIntervalSeconds) ||
            options.WarmStartIntervalSeconds <= 0 ||
            options.ViewportIngressBandTiles < 0 ||
            options.ViewportIngressAttemptCycle <= 0 ||
            options.ViewportIngressAttemptsPerCycle < 0 ||
            options.ViewportIngressAttemptsPerCycle > options.ViewportIngressAttemptCycle ||
            !float.IsFinite(options.ViewportIngressSpeedTilesPerSecond) ||
            options.ViewportIngressSpeedTilesPerSecond < 0 ||
            !float.IsFinite(options.ViewportIngressMaxSeconds) ||
            options.ViewportIngressMaxSeconds < 0 ||
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
