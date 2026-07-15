using Game.Core.Biomes;
using Game.Core.Data;
using Game.Core.Entities;
using Game.Core.Physics;
using Game.Core.Randomness;
using Game.Core.Time;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Spawning;

public sealed class SpawnSystem
{
    private readonly Random _random;
    private readonly EntityFactory _entityFactory;
    private readonly Dictionary<string, float> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _cooldownRuleIds = new();

    public SpawnSystem(Random random, EntityFactory entityFactory)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(entityFactory);
        _random = random;
        _entityFactory = entityFactory;
    }

    public SpawnSystem(DeterministicRandomStream stream, EntityFactory entityFactory)
        : this(new DeterministicRandomAdapter(stream), entityFactory)
    {
    }

    public SpawnSystem(Random random)
        : this(random, new EntityFactory(new TileCollisionResolver()))
    {
    }

    public SpawnSystem(DeterministicRandomStream stream)
        : this(stream, new EntityFactory(new TileCollisionResolver()))
    {
    }

    public void AdvanceCooldowns(float deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        for (var index = _cooldownRuleIds.Count - 1; index >= 0; index--)
        {
            var ruleId = _cooldownRuleIds[index];
            var remaining = _cooldowns[ruleId] - deltaSeconds;
            if (remaining > 0)
            {
                _cooldowns[ruleId] = remaining;
                continue;
            }

            _cooldowns.Remove(ruleId);
            _cooldownRuleIds.RemoveAt(index);
        }
    }

    public float GetCooldownRemaining(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        return _cooldowns.GetValueOrDefault(ruleId);
    }

    public SpawnAttemptResult TrySpawn(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        BiomeMap biomeMap,
        WorldTime time,
        TilePos candidateTile)
    {
        ArgumentNullException.ThrowIfNull(biomeMap);
        return TrySpawn(
            world,
            entities,
            content,
            time,
            SpawnAttemptContext.Direct(candidateTile, biomeMap, null));
    }

    public SpawnAttemptResult TrySpawn(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        string biomeId,
        WorldTime time,
        TilePos candidateTile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(biomeId);
        return TrySpawn(
            world,
            entities,
            content,
            time,
            SpawnAttemptContext.Direct(candidateTile, null, biomeId));
    }

    internal SpawnAttemptResult TrySpawn(
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        WorldTime time,
        SpawnAttemptContext context)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(time);

        var definitions = content.SpawnRules.Definitions;
        var totalWeight = 0d;
        for (var ruleIndex = 0; ruleIndex < definitions.Count; ruleIndex++)
        {
            if (TryEvaluateRule(
                    definitions[ruleIndex],
                    world,
                    entities,
                    content,
                    time,
                    context,
                    out var evaluation))
            {
                totalWeight += evaluation.Weight;
            }
        }

        if (totalWeight <= 0)
        {
            return SpawnAttemptResult.None;
        }

        var roll = _random.NextDouble() * totalWeight;
        SpawnRuleDefinition? selectedRule = null;
        SpawnEvaluation selected = default;
        for (var ruleIndex = 0; ruleIndex < definitions.Count; ruleIndex++)
        {
            var rule = definitions[ruleIndex];
            if (!TryEvaluateRule(rule, world, entities, content, time, context, out var evaluation))
            {
                continue;
            }

            roll -= evaluation.Weight;
            if (roll > 0 && ruleIndex < definitions.Count - 1)
            {
                continue;
            }

            selectedRule = rule;
            selected = evaluation;
            break;
        }

        if (selectedRule is null)
        {
            return SpawnAttemptResult.None;
        }

        var chance = Math.Clamp(selectedRule.Chance * selected.DensityMultiplier, 0f, 1f);
        if (_random.NextSingle() > chance)
        {
            return SpawnAttemptResult.None;
        }

        var entity = _entityFactory.CreateEnemy(
            selected.Definition,
            CoordinateUtils.TileToWorld(selected.Tile));
        entity.AssignSpawnMetadata(
            selectedRule.Id,
            selectedRule.PopulationGroup,
            selected.Region,
            selected.Habitat);
        entities.Add(entity);
        StartCooldown(selectedRule);
        return new SpawnAttemptResult(true, selectedRule.Id, entity);
    }

    private bool TryEvaluateRule(
        SpawnRuleDefinition rule,
        World.World world,
        EntityManager entities,
        GameContentDatabase content,
        WorldTime time,
        SpawnAttemptContext context,
        out SpawnEvaluation evaluation)
    {
        evaluation = default;
        if (_cooldowns.ContainsKey(rule.Id) || !MatchesTime(rule, time))
        {
            return false;
        }

        var definition = content.Entities.GetById(rule.EntityId);
        if (!TryFindPlacement(
                rule,
                definition,
                world,
                entities,
                context,
                out var tile,
                out var habitat))
        {
            return false;
        }

        var environment = ResolveEnvironment(world, context, tile);
        if (!string.IsNullOrWhiteSpace(rule.BiomeId) &&
            !string.Equals(rule.BiomeId, environment.BiomeId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var weight = CalculateWeight(rule, environment, habitat, time.IsNight);
        if (weight <= 0)
        {
            return false;
        }

        var region = SpawnRegionKey.FromTile(tile, rule.PopulationRegionSizeTiles);
        if (!WithinPopulationCaps(rule, entities, tile, habitat, region))
        {
            return false;
        }

        evaluation = new SpawnEvaluation(
            definition,
            tile,
            habitat,
            region,
            weight,
            environment.DensityMultiplier);
        return true;
    }

    private static bool TryFindPlacement(
        SpawnRuleDefinition rule,
        EntityDefinition definition,
        World.World world,
        EntityManager entities,
        SpawnAttemptContext context,
        out TilePos tile,
        out SpawnHabitat habitat)
    {
        var widthTiles = Math.Max(1, (int)Math.Ceiling(definition.Width / GameConstants.TileSize));
        var heightTiles = Math.Max(1, (int)Math.Ceiling(definition.Height / GameConstants.TileSize));
        var radius = context.Options.PlacementSearchRadiusTiles;
        var samples = radius * 2 + 1;
        for (var sample = 0; sample < samples; sample++)
        {
            var offset = sample == 0
                ? 0
                : (sample % 2 == 1 ? 1 : -1) * ((sample + 1) / 2);
            var candidateY = (long)context.TargetTile.Y + offset;
            if (candidateY < int.MinValue || candidateY > int.MaxValue)
            {
                continue;
            }

            var candidate = new TilePos(context.TargetTile.X, (int)candidateY);
            if ((rule.MinTileY is not null && candidate.Y < rule.MinTileY) ||
                (rule.MaxTileY is not null && candidate.Y > rule.MaxTileY) ||
                !TryResolveHabitat(rule.Habitats, world, candidate, out var resolvedHabitat) ||
                !MatchesPlacement(definition, resolvedHabitat, world, candidate, widthTiles, heightTiles) ||
                !IsAllowedByActivity(context, candidate, widthTiles, heightTiles) ||
                IntersectsActiveEntity(entities, candidate, definition.Width, definition.Height))
            {
                continue;
            }

            tile = candidate;
            habitat = resolvedHabitat;
            return true;
        }

        tile = default;
        habitat = default;
        return false;
    }

    private static bool MatchesPlacement(
        EntityDefinition definition,
        SpawnHabitat habitat,
        World.World world,
        TilePos candidate,
        int widthTiles,
        int heightTiles)
    {
        if (!IsBodyClear(world, candidate, widthTiles, heightTiles))
        {
            return false;
        }

        if (definition.MovementMode == EntityMovementMode.Flying)
        {
            return habitat != SpawnHabitat.WaterEdge || HasNearbyLiquid(world, candidate);
        }

        var groundY = (long)candidate.Y + heightTiles;
        if (groundY > int.MaxValue)
        {
            return false;
        }

        for (var xOffset = 0; xOffset < widthTiles; xOffset++)
        {
            var tileX = (long)candidate.X + xOffset;
            if (tileX < int.MinValue || tileX > int.MaxValue ||
                !IsSolid(world, (int)tileX, (int)groundY))
            {
                return false;
            }
        }

        return habitat != SpawnHabitat.WaterEdge || HasNearbyLiquid(world, candidate);
    }

    private static bool IsBodyClear(
        World.World world,
        TilePos candidate,
        int widthTiles,
        int heightTiles)
    {
        for (var yOffset = 0; yOffset < heightTiles; yOffset++)
        {
            for (var xOffset = 0; xOffset < widthTiles; xOffset++)
            {
                var tileX = (long)candidate.X + xOffset;
                var tileY = (long)candidate.Y + yOffset;
                if (tileX < int.MinValue || tileX > int.MaxValue ||
                    tileY < int.MinValue || tileY > int.MaxValue ||
                    !world.TryGetTile((int)tileX, (int)tileY, out var tile) ||
                    (tile.Flags & (TileFlags.Solid | TileFlags.HasLiquid)) != 0 ||
                    tile.LiquidAmount > 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsAllowedByActivity(
        SpawnAttemptContext context,
        TilePos candidate,
        int widthTiles,
        int heightTiles)
    {
        if (context.SourceCount == 0)
        {
            return true;
        }

        var centerX = (double)candidate.X + widthTiles * 0.5d;
        var centerY = (double)candidate.Y + heightTiles * 0.5d;
        var selected = context.GetSource(context.SourceIndex);
        var selectedDistanceSquared = DistanceSquared(centerX, centerY, selected.CenterTile);
        var minDistanceSquared = (double)context.Options.MinDistanceTiles * context.Options.MinDistanceTiles;
        var maxDistanceSquared = (double)context.Options.MaxDistanceTiles * context.Options.MaxDistanceTiles;
        if (selectedDistanceSquared < minDistanceSquared || selectedDistanceSquared > maxDistanceSquared)
        {
            return false;
        }

        var bodyBounds = new RectI(candidate.X, candidate.Y, widthTiles, heightTiles);
        for (var index = 0; index < context.SourceCount; index++)
        {
            var source = context.GetSource(index);
            if (DistanceSquared(centerX, centerY, source.CenterTile) < minDistanceSquared)
            {
                return false;
            }

            var visibleBounds = source.VisibleTileBounds;
            if (visibleBounds.IsEmpty && !context.UseImplicitVisibleBounds)
            {
                continue;
            }

            if (visibleBounds.IsEmpty)
            {
                visibleBounds = CreateDefaultVisibleBounds(source.CenterTile, context.Options);
            }

            if (bodyBounds.Intersects(visibleBounds.Inflate(context.Options.OnScreenExclusionPaddingTiles)))
            {
                return false;
            }
        }

        return true;
    }

    private static RectI CreateDefaultVisibleBounds(TilePos center, SpawnSchedulerOptions options)
    {
        return RectI.FromInclusiveTileBounds(
            Saturate((long)center.X - options.OnScreenHalfWidthTiles),
            Saturate((long)center.Y - options.OnScreenHalfHeightTiles),
            Saturate((long)center.X + options.OnScreenHalfWidthTiles),
            Saturate((long)center.Y + options.OnScreenHalfHeightTiles));
    }

    private static bool IntersectsActiveEntity(
        EntityManager entities,
        TilePos candidate,
        float width,
        float height)
    {
        var bounds = new RectI(
            Saturate((long)candidate.X * GameConstants.TileSize),
            Saturate((long)candidate.Y * GameConstants.TileSize),
            Math.Max(1, (int)Math.Ceiling(width)),
            Math.Max(1, (int)Math.Ceiling(height)));
        for (var index = 0; index < entities.Entities.Count; index++)
        {
            var entity = entities.Entities[index];
            if (entity.IsActive && entity.Bounds.Intersects(bounds))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WithinPopulationCaps(
        SpawnRuleDefinition rule,
        EntityManager entities,
        TilePos candidate,
        SpawnHabitat habitat,
        SpawnRegionKey region)
    {
        var activeCount = 0;
        var activeInGroup = 0;
        var activeInRegion = 0;
        var activeInHabitat = 0;
        var activeInLocalArea = 0;
        var localRadiusSquared = (double)rule.LocalPopulationRadiusTiles * rule.LocalPopulationRadiusTiles;
        for (var index = 0; index < entities.Entities.Count; index++)
        {
            if (entities.Entities[index] is not EnemyEntity { IsActive: true } entity)
            {
                continue;
            }

            if (string.Equals(entity.DefinitionId, rule.EntityId, StringComparison.OrdinalIgnoreCase))
            {
                activeCount++;
            }

            if (string.IsNullOrWhiteSpace(rule.PopulationGroup) ||
                !string.Equals(entity.SpawnGroup, rule.PopulationGroup, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            activeInGroup++;
            if (entity.SpawnRegion == region)
            {
                activeInRegion++;
            }

            if (entity.SpawnHabitat == habitat)
            {
                activeInHabitat++;
            }

            var entityTile = CoordinateUtils.WorldToTile(entity.Body.Center.X, entity.Body.Center.Y);
            if (DistanceSquared(candidate.X, candidate.Y, entityTile) <= localRadiusSquared)
            {
                activeInLocalArea++;
            }
        }

        return activeCount < rule.MaxActive &&
               (rule.MaxActiveInGroup is null || activeInGroup < rule.MaxActiveInGroup) &&
               (rule.MaxActiveInRegion is null || activeInRegion < rule.MaxActiveInRegion) &&
               (rule.MaxActiveInHabitat is null || activeInHabitat < rule.MaxActiveInHabitat) &&
               (rule.MaxActiveInLocalArea is null || activeInLocalArea < rule.MaxActiveInLocalArea);
    }

    private static SpawnEnvironment ResolveEnvironment(
        World.World world,
        SpawnAttemptContext context,
        TilePos tile)
    {
        var sourceEnvironment = context.SourceCount == 0
            ? new SpawnEnvironment()
            : context.GetSource(context.SourceIndex).Environment;
        var biomeId = !string.IsNullOrWhiteSpace(sourceEnvironment.BiomeId)
            ? sourceEnvironment.BiomeId
            : !string.IsNullOrWhiteSpace(context.ExplicitBiomeId)
                ? context.ExplicitBiomeId
                : context.BiomeMap?.GetBiomeAt(tile.X, tile.Y) ?? SpawnEnvironment.NoneId;
        var layerId = !string.IsNullOrWhiteSpace(sourceEnvironment.VerticalLayerId)
            ? sourceEnvironment.VerticalLayerId
            : ResolveVerticalLayer(world, tile.Y);
        var weatherId = string.IsNullOrWhiteSpace(sourceEnvironment.WeatherId)
            ? SpawnEnvironment.NoneId
            : sourceEnvironment.WeatherId;
        var eventId = string.IsNullOrWhiteSpace(sourceEnvironment.WorldEventId)
            ? SpawnEnvironment.NoneId
            : sourceEnvironment.WorldEventId;
        var density = sourceEnvironment.IsSpecified ? sourceEnvironment.DensityMultiplier : 1f;
        return new SpawnEnvironment(biomeId, layerId, weatherId, eventId, density, true);
    }

    private static string ResolveVerticalLayer(World.World world, int tileY)
    {
        if (tileY <= world.HeightTiles * 0.35f)
        {
            return "surface";
        }

        return tileY >= world.HeightTiles * 0.55f ? "cavern" : "underground";
    }

    private static float CalculateWeight(
        SpawnRuleDefinition rule,
        SpawnEnvironment environment,
        SpawnHabitat habitat,
        bool isNight)
    {
        var weight = rule.Weight * (isNight ? rule.NightWeight : rule.DayWeight);
        weight *= GetDimensionWeight(rule.BiomeWeights, environment.BiomeId ?? SpawnEnvironment.NoneId);
        weight *= GetDimensionWeight(rule.VerticalLayerWeights, environment.VerticalLayerId ?? SpawnEnvironment.NoneId);
        weight *= GetDimensionWeight(rule.WeatherWeights, environment.WeatherId ?? SpawnEnvironment.NoneId);
        weight *= GetDimensionWeight(rule.WorldEventWeights, environment.WorldEventId ?? SpawnEnvironment.NoneId);
        weight *= GetDimensionWeight(rule.HabitatWeights, GetHabitatKey(habitat));
        return weight;
    }

    private static string GetHabitatKey(SpawnHabitat habitat)
    {
        return habitat switch
        {
            SpawnHabitat.Surface => "Surface",
            SpawnHabitat.Underground => "Underground",
            SpawnHabitat.Cavern => "Cavern",
            SpawnHabitat.OpenAir => "OpenAir",
            SpawnHabitat.WaterEdge => "WaterEdge",
            _ => "Any"
        };
    }

    private static float GetDimensionWeight(Dictionary<string, float> weights, string key)
    {
        if (weights.Count == 0)
        {
            return 1f;
        }

        if (weights.TryGetValue(key, out var direct))
        {
            return direct;
        }

        var fallback = 0f;
        foreach (var pair in weights)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }

            if (pair.Key == "*")
            {
                fallback = pair.Value;
            }
        }

        return fallback;
    }

    private static bool MatchesTime(SpawnRuleDefinition rule, WorldTime time)
    {
        if (rule.RequiresNight is not null && rule.RequiresNight.Value != time.IsNight)
        {
            return false;
        }

        return rule.RequiresNight is not null ||
               (rule.Time != SpawnTimeCondition.Day || !time.IsNight) &&
               (rule.Time != SpawnTimeCondition.Night || time.IsNight);
    }

    private static bool TryResolveHabitat(
        IReadOnlyList<SpawnHabitat> habitats,
        World.World world,
        TilePos candidate,
        out SpawnHabitat resolved)
    {
        if (habitats.Count == 0)
        {
            resolved = SpawnHabitat.Any;
            return true;
        }

        for (var index = 0; index < habitats.Count; index++)
        {
            var matches = habitats[index] switch
            {
                SpawnHabitat.Any => true,
                SpawnHabitat.Surface => candidate.Y <= world.HeightTiles * 0.35f,
                SpawnHabitat.Underground => candidate.Y >= world.HeightTiles * 0.25f,
                SpawnHabitat.Cavern => candidate.Y >= world.HeightTiles * 0.55f,
                SpawnHabitat.OpenAir => HasOpenAir(world, candidate),
                SpawnHabitat.WaterEdge => HasNearbyLiquid(world, candidate),
                _ => false
            };
            if (matches)
            {
                resolved = habitats[index];
                return true;
            }
        }

        resolved = default;
        return false;
    }

    private static bool HasOpenAir(World.World world, TilePos candidate)
    {
        for (var offset = 0; offset <= 2; offset++)
        {
            var tileY = (long)candidate.Y - offset;
            if (tileY < int.MinValue || tileY > int.MaxValue ||
                !world.TryGetTile(candidate.X, (int)tileY, out var tile) ||
                (tile.Flags & (TileFlags.Solid | TileFlags.HasLiquid)) != 0 ||
                tile.LiquidAmount > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasNearbyLiquid(World.World world, TilePos candidate)
    {
        for (var yOffset = -1; yOffset <= 1; yOffset++)
        {
            for (var xOffset = -1; xOffset <= 1; xOffset++)
            {
                var x = (long)candidate.X + xOffset;
                var y = (long)candidate.Y + yOffset;
                if (x >= int.MinValue && x <= int.MaxValue &&
                    y >= int.MinValue && y <= int.MaxValue &&
                    world.TryGetTile((int)x, (int)y, out var tile) &&
                    (tile.Flags & TileFlags.HasLiquid) != 0 &&
                    tile.LiquidAmount > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void StartCooldown(SpawnRuleDefinition rule)
    {
        if (rule.CooldownSeconds <= 0)
        {
            return;
        }

        if (!_cooldowns.ContainsKey(rule.Id))
        {
            _cooldownRuleIds.Add(rule.Id);
        }

        _cooldowns[rule.Id] = rule.CooldownSeconds;
    }

    private static bool IsSolid(World.World world, int tileX, int tileY)
    {
        return world.TryGetTile(tileX, tileY, out var tile) &&
               (tile.Flags & TileFlags.Solid) != 0;
    }

    private static double DistanceSquared(double x, double y, TilePos other)
    {
        var dx = x - other.X;
        var dy = y - other.Y;
        return dx * dx + dy * dy;
    }

    private static int Saturate(long value)
    {
        return (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    }

    private readonly record struct SpawnEvaluation(
        EntityDefinition Definition,
        TilePos Tile,
        SpawnHabitat Habitat,
        SpawnRegionKey Region,
        float Weight,
        float DensityMultiplier);
}
