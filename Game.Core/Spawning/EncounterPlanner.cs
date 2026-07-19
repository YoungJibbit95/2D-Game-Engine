using Game.Core.Entities;
using Game.Core.Randomness;
using Game.Core.Time;
using Game.Core.World;

namespace Game.Core.Spawning;

public sealed class EncounterPlanner
{
    private readonly Random _random;
    private readonly Dictionary<string, float> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _cooldownIds = new();

    public EncounterPlanner(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    public EncounterPlanner(DeterministicRandomStream stream)
        : this(new DeterministicRandomAdapter(stream))
    {
    }

    public void AdvanceCooldowns(float deltaSeconds)
    {
        if (deltaSeconds <= 0)
        {
            return;
        }

        for (var index = _cooldownIds.Count - 1; index >= 0; index--)
        {
            var encounterId = _cooldownIds[index];
            var remaining = _cooldowns[encounterId] - deltaSeconds;
            if (remaining > 0)
            {
                _cooldowns[encounterId] = remaining;
                continue;
            }

            _cooldowns.Remove(encounterId);
            _cooldownIds.RemoveAt(index);
        }
    }

    public float GetCooldownRemaining(string encounterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        return _cooldowns.GetValueOrDefault(encounterId);
    }

    public bool HasApplicableEncounter(
        EncounterDefinitionRegistry encounters,
        SpawnEnvironment environment,
        WorldTime time)
    {
        ArgumentNullException.ThrowIfNull(encounters);
        ArgumentNullException.ThrowIfNull(time);

        var definitions = encounters.Definitions;
        for (var index = 0; index < definitions.Count; index++)
        {
            if (MatchesEnvironment(definitions[index], environment, time))
            {
                return true;
            }
        }

        return false;
    }

    public EncounterPlan? TryPlan(
        EncounterDefinitionRegistry encounters,
        EntityManager entities,
        SpawnEnvironment environment,
        WorldTime time,
        TilePos regionTile)
    {
        ArgumentNullException.ThrowIfNull(encounters);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(time);
        if (environment.DensityMultiplier <= 0)
        {
            return null;
        }

        var selected = SelectEncounter(encounters, entities, environment, time, regionTile);
        if (selected is null)
        {
            return null;
        }

        var (activeGlobal, activeInRegion) = CountActiveMembers(selected, entities, regionTile);
        var capacity = Math.Min(
            selected.MaxActiveGlobal - activeGlobal,
            selected.MaxActiveInRegion - activeInRegion);
        if (capacity <= 0)
        {
            return null;
        }

        var roleSelectionCount = NextInclusive(selected.MinRoleSelections, selected.MaxRoleSelections);
        var availableRoles = new List<int>(selected.Roles.Count);
        for (var roleIndex = 0; roleIndex < selected.Roles.Count; roleIndex++)
        {
            availableRoles.Add(roleIndex);
        }

        var intents = new List<EncounterSpawnIntent>(Math.Min(capacity, selected.Roles.Count * 2));
        for (var selectionIndex = 0;
             selectionIndex < roleSelectionCount && availableRoles.Count > 0 && intents.Count < capacity;
             selectionIndex++)
        {
            var availableIndex = SelectRoleIndex(selected, availableRoles);
            var role = selected.Roles[availableRoles[availableIndex]];
            availableRoles.RemoveAt(availableIndex);
            var count = Math.Min(NextInclusive(role.MinCount, role.MaxCount), capacity - intents.Count);
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                intents.Add(new EncounterSpawnIntent(role.Id, role.SpawnRuleId, ordinal));
            }
        }

        return intents.Count == 0 ? null : new EncounterPlan(selected, intents.ToArray());
    }

    public bool CanSpawnAt(
        EncounterDefinition definition,
        EntityManager entities,
        TilePos candidateTile)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(entities);
        var (activeGlobal, activeInRegion) = CountActiveMembers(definition, entities, candidateTile);
        return activeGlobal < definition.MaxActiveGlobal && activeInRegion < definition.MaxActiveInRegion;
    }

    public void CommitSpawned(EncounterPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var definition = plan.Definition;
        if (definition.CooldownSeconds <= 0)
        {
            return;
        }

        if (!_cooldowns.ContainsKey(definition.Id))
        {
            _cooldownIds.Add(definition.Id);
        }

        _cooldowns[definition.Id] = definition.CooldownSeconds;
    }

    private EncounterDefinition? SelectEncounter(
        EncounterDefinitionRegistry encounters,
        EntityManager entities,
        SpawnEnvironment environment,
        WorldTime time,
        TilePos regionTile)
    {
        var definitions = encounters.Definitions;
        var totalWeight = 0d;
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (IsAvailable(definition, entities, environment, time, regionTile))
            {
                totalWeight += definition.Weight;
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        var roll = _random.NextDouble() * totalWeight;
        EncounterDefinition? lastAvailable = null;
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (!IsAvailable(definition, entities, environment, time, regionTile))
            {
                continue;
            }

            lastAvailable = definition;
            roll -= definition.Weight;
            if (roll <= 0)
            {
                return definition;
            }
        }

        return lastAvailable;
    }

    private bool IsAvailable(
        EncounterDefinition definition,
        EntityManager entities,
        SpawnEnvironment environment,
        WorldTime time,
        TilePos regionTile)
    {
        if (_cooldowns.ContainsKey(definition.Id) || !MatchesEnvironment(definition, environment, time))
        {
            return false;
        }

        var (activeGlobal, activeInRegion) = CountActiveMembers(definition, entities, regionTile);
        return activeGlobal < definition.MaxActiveGlobal && activeInRegion < definition.MaxActiveInRegion;
    }

    private static bool MatchesEnvironment(
        EncounterDefinition definition,
        SpawnEnvironment environment,
        WorldTime time)
    {
        return MatchesTime(definition.Time, time.IsNight) &&
               MatchesId(definition.BiomeIds, environment.BiomeId) &&
               MatchesId(definition.VerticalLayerIds, environment.VerticalLayerId);
    }

    private static bool MatchesTime(SpawnTimeCondition condition, bool isNight)
    {
        return condition == SpawnTimeCondition.Any ||
               condition == SpawnTimeCondition.Day && !isNight ||
               condition == SpawnTimeCondition.Night && isNight;
    }

    private static bool MatchesId(IReadOnlyList<string> allowedIds, string? actualId)
    {
        if (allowedIds.Count == 0)
        {
            return true;
        }

        for (var index = 0; index < allowedIds.Count; index++)
        {
            if (string.Equals(allowedIds[index], actualId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static (int Global, int Region) CountActiveMembers(
        EncounterDefinition definition,
        EntityManager entities,
        TilePos regionTile)
    {
        var targetRegion = SpawnRegionKey.FromTile(regionTile, definition.PopulationRegionSizeTiles);
        var global = 0;
        var region = 0;
        for (var entityIndex = 0; entityIndex < entities.Entities.Count; entityIndex++)
        {
            if (entities.Entities[entityIndex] is not EnemyEntity { IsActive: true } enemy ||
                !ContainsSpawnRule(definition, enemy.SpawnRuleId))
            {
                continue;
            }

            global++;
            var entityTile = CoordinateUtils.WorldToTile(enemy.Body.Center.X, enemy.Body.Center.Y);
            if (SpawnRegionKey.FromTile(entityTile, definition.PopulationRegionSizeTiles) == targetRegion)
            {
                region++;
            }
        }

        return (global, region);
    }

    private static bool ContainsSpawnRule(EncounterDefinition definition, string? spawnRuleId)
    {
        if (string.IsNullOrWhiteSpace(spawnRuleId))
        {
            return false;
        }

        for (var roleIndex = 0; roleIndex < definition.Roles.Count; roleIndex++)
        {
            if (string.Equals(
                    definition.Roles[roleIndex].SpawnRuleId,
                    spawnRuleId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private int SelectRoleIndex(EncounterDefinition definition, IReadOnlyList<int> availableRoles)
    {
        var totalWeight = 0d;
        for (var index = 0; index < availableRoles.Count; index++)
        {
            totalWeight += definition.Roles[availableRoles[index]].Weight;
        }

        var roll = _random.NextDouble() * totalWeight;
        for (var index = 0; index < availableRoles.Count; index++)
        {
            roll -= definition.Roles[availableRoles[index]].Weight;
            if (roll <= 0)
            {
                return index;
            }
        }

        return availableRoles.Count - 1;
    }

    private int NextInclusive(int minimum, int maximum)
    {
        return minimum == maximum ? minimum : _random.Next(minimum, maximum + 1);
    }
}
