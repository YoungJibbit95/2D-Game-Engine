using Game.Core.World.Generation;

namespace Game.Core.WorldEvents;

public sealed class DeterministicWorldEventSystem
{
    private readonly int _seed;
    private readonly int _windowTicks;
    private readonly WorldEventDefinition[] _definitions;
    private readonly ulong[] _definitionSalts;

    public DeterministicWorldEventSystem(
        int seed,
        IReadOnlyList<WorldEventDefinition> definitions,
        int windowTicks = 3_600)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (windowTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowTicks));
        }

        foreach (var definition in definitions)
        {
            WorldEventDefinition.Validate(definition);
            if (definition.MaxDurationTicks > windowTicks)
            {
                throw new ArgumentException(
                    $"World event '{definition.Id}' duration exceeds the scheduling window.",
                    nameof(definitions));
            }
        }

        _seed = seed;
        _windowTicks = windowTicks;
        _definitions = definitions.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        _definitionSalts = _definitions
            .Select(definition => DeterministicCoordinateHash.Salt(definition.Id) ^ 0x94D049BB133111EBUL)
            .ToArray();
    }

    public WorldEventState GetState(
        long worldTick,
        long regionIndex,
        string biomeId,
        string? subBiomeId)
    {
        if (worldTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(worldTick));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(biomeId);
        var window = worldTick / _windowTicks;
        var windowStart = window * _windowTicks;
        for (var definitionIndex = 0; definitionIndex < _definitions.Length; definitionIndex++)
        {
            var definition = _definitions[definitionIndex];
            if (!IsAllowed(definition, biomeId, subBiomeId))
            {
                continue;
            }

            var salt = _definitionSalts[definitionIndex];
            if (DeterministicCoordinateHash.Unit(_seed, regionIndex, window, salt) >=
                definition.ChancePerWindow)
            {
                continue;
            }

            var duration = DeterministicCoordinateHash.Range(
                _seed,
                regionIndex,
                window,
                salt + 1,
                definition.MinDurationTicks,
                definition.MaxDurationTicks);
            var latestStartOffset = Math.Max(0, _windowTicks - duration);
            var startOffset = DeterministicCoordinateHash.Range(
                _seed,
                regionIndex,
                window,
                salt + 2,
                0,
                latestStartOffset);
            var start = windowStart + startOffset;
            var end = start + duration;
            if (worldTick < start || worldTick >= end)
            {
                continue;
            }

            return new WorldEventState(
                true,
                definition.Id,
                regionIndex,
                start,
                end,
                (worldTick - start) / (float)duration,
                definition.Intensity);
        }

        return WorldEventState.Inactive(regionIndex, worldTick);
    }

    public WorldEventSystemSnapshot CreateSnapshot(
        long worldTick,
        long regionIndex,
        string biomeId,
        string? subBiomeId)
    {
        return new WorldEventSystemSnapshot(
            WorldEventSystemSnapshot.CurrentFormatVersion,
            worldTick,
            regionIndex,
            biomeId,
            subBiomeId,
            GetState(worldTick, regionIndex, biomeId, subBiomeId));
    }

    public WorldEventSystemSnapshot Advance(
        WorldEventSystemSnapshot snapshot,
        long worldTick,
        long regionIndex,
        string biomeId,
        string? subBiomeId)
    {
        ValidateSnapshot(snapshot);
        if (worldTick < snapshot.LastAdvancedTick)
        {
            throw new ArgumentOutOfRangeException(nameof(worldTick), "World-event snapshots cannot advance backwards.");
        }

        return CreateSnapshot(worldTick, regionIndex, biomeId, subBiomeId);
    }

    public static void ValidateSnapshot(WorldEventSystemSnapshot snapshot)
    {
        if (snapshot.FormatVersion != WorldEventSystemSnapshot.CurrentFormatVersion ||
            snapshot.LastAdvancedTick < 0 ||
            string.IsNullOrWhiteSpace(snapshot.BiomeId) ||
            snapshot.State.RegionIndex != snapshot.RegionIndex ||
            snapshot.State.StartTick < 0 ||
            snapshot.State.EndTickExclusive < snapshot.State.StartTick ||
            !float.IsFinite(snapshot.State.Progress) ||
            snapshot.State.Progress is < 0f or > 1f ||
            !float.IsFinite(snapshot.State.Intensity) ||
            snapshot.State.Intensity < 0f ||
            (snapshot.State.IsActive && string.IsNullOrWhiteSpace(snapshot.State.EventId)) ||
            (!snapshot.State.IsActive && snapshot.State.EventId is not null))
        {
            throw new InvalidDataException("World-event snapshot is invalid or uses an unsupported format.");
        }
    }

    private static bool IsAllowed(
        WorldEventDefinition definition,
        string biomeId,
        string? subBiomeId)
    {
        return (definition.AllowedBiomeIds.Count == 0 ||
                Contains(definition.AllowedBiomeIds, biomeId)) &&
            (definition.AllowedSubBiomeIds.Count == 0 ||
             (subBiomeId is not null &&
              Contains(definition.AllowedSubBiomeIds, subBiomeId)));
    }

    private static bool Contains(IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
