using Game.Core.World.Generation;

namespace Game.Core.WorldEvents;

public sealed class DeterministicWorldEventExecutor : IWorldEventExecutor
{
    private const ulong ScheduleSalt = 0xD1B54A32D192ED03UL;
    private const ulong PlayerActionSalt = 0x94D049BB133111EBUL;

    private readonly int _seed;
    private readonly int _windowTicks;
    private readonly WorldEventDefinitionRegistry _registry;

    public DeterministicWorldEventExecutor(
        int seed,
        WorldEventDefinitionRegistry registry,
        int windowTicks = 3_600)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (windowTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowTicks));
        }

        foreach (var definition in registry.Definitions)
        {
            if (definition.MaxDurationTicks > windowTicks)
            {
                throw new ArgumentException(
                    $"World event '{definition.Id}' duration exceeds the scheduling window.",
                    nameof(registry));
            }
        }

        _seed = seed;
        _registry = registry;
        _windowTicks = windowTicks;
    }

    public WorldEventExecutionResult Advance(
        WorldEventRuntimeSnapshot previous,
        in WorldEventExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(previous);
        context.Validate();
        ValidateSnapshot(previous);
        if (context.WorldTick < previous.LastAdvancedTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(context),
                "World-event runtime cannot advance backwards.");
        }

        var events = new List<WorldEventDomainEvent>(4);
        var nextSequence = 0L;
        var snapshot = previous;
        if (previous.RegionIndex != context.RegionIndex)
        {
            if (previous.Status == WorldEventRuntimeStatus.Active && previous.ActiveEventId is not null)
            {
                events.Add(CreateEvent(
                    ref nextSequence,
                    context.WorldTick,
                    previous.RegionIndex,
                    previous.ActiveEventId,
                    WorldEventDomainEventKind.Cancelled,
                    previous.PhaseId,
                    previous.Progress));
            }

            snapshot = WorldEventRuntimeSnapshot.Inactive(context);
        }

        if (snapshot.Status == WorldEventRuntimeStatus.Active && snapshot.ActiveEventId is not null)
        {
            return AdvanceActive(snapshot, context, events, ref nextSequence);
        }

        var cooldowns = RemoveExpiredCooldowns(snapshot.Cooldowns, context.WorldTick);
        if (TrySelectScheduledEvent(context, cooldowns, out var definition, out var schedule))
        {
            var active = CreateActiveSnapshot(context, definition, schedule, cooldowns);
            events.Add(CreateEvent(
                ref nextSequence,
                context.WorldTick,
                context.RegionIndex,
                definition.Id,
                WorldEventDomainEventKind.Activated,
                active.PhaseId,
                active.Progress));
            return new WorldEventExecutionResult(active, events.ToArray());
        }

        var status = cooldowns.Count > 0
            ? WorldEventRuntimeStatus.Cooldown
            : WorldEventRuntimeStatus.Inactive;
        return new WorldEventExecutionResult(
            snapshot with
            {
                LastAdvancedTick = context.WorldTick,
                RegionIndex = context.RegionIndex,
                BiomeId = context.BiomeId,
                SubBiomeId = context.SubBiomeId,
                Status = status,
                ActiveEventId = null,
                StartTick = 0,
                EndTickExclusive = 0,
                PhaseId = null,
                PhaseIndex = -1,
                Progress = 0f,
                PhaseProgress = 0f,
                Intensity = 0f,
                EffectiveModifiers = WorldEventModifierSet.Identity,
                Cooldowns = cooldowns
            },
            events.ToArray());
    }

    public WorldEventExecutionResult TriggerPlayerAction(
        WorldEventRuntimeSnapshot previous,
        in WorldEventExecutionContext context,
        WorldEventPlayerActionKind action,
        long actionSequence)
    {
        ArgumentNullException.ThrowIfNull(previous);
        context.Validate();
        ValidateSnapshot(previous);
        if (actionSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actionSequence));
        }

        if (context.WorldTick < previous.LastAdvancedTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(context),
                "World-event player actions cannot be applied backwards.");
        }

        if (previous.Status == WorldEventRuntimeStatus.Active)
        {
            return new WorldEventExecutionResult(previous, Array.Empty<WorldEventDomainEvent>());
        }

        var cooldowns = RemoveExpiredCooldowns(previous.Cooldowns, context.WorldTick);
        foreach (var definition in _registry.Definitions)
        {
            if (!ContainsAction(definition.PlayerActionTriggers, action) ||
                !IsAllowed(definition, context) ||
                IsCoolingDown(cooldowns, definition.Id, context.WorldTick))
            {
                continue;
            }

            var salt = DeterministicCoordinateHash.Salt(definition.Id) ^ PlayerActionSalt;
            if (DeterministicCoordinateHash.Unit(
                    _seed,
                    context.RegionIndex,
                    actionSequence,
                    salt) >= definition.PlayerActionTriggerChance)
            {
                continue;
            }

            var duration = DeterministicCoordinateHash.Range(
                _seed,
                context.RegionIndex,
                actionSequence,
                salt + 1,
                definition.MinDurationTicks,
                definition.MaxDurationTicks);
            var schedule = new EventSchedule(
                context.WorldTick,
                checked(context.WorldTick + duration));
            var active = CreateActiveSnapshot(context, definition, schedule, cooldowns) with
            {
                ActivationSource = WorldEventActivationSource.PlayerAction,
                TriggerAction = action,
                TriggerSequence = actionSequence
            };
            var eventSequence = 0L;
            var triggered = CreateEvent(
                ref eventSequence,
                context.WorldTick,
                context.RegionIndex,
                definition.Id,
                WorldEventDomainEventKind.PlayerActionTriggered,
                active.PhaseId,
                active.Progress) with
            {
                Sequence = 0,
                TriggerSequence = active.TriggerSequence
            };
            var activated = CreateEvent(
                ref eventSequence,
                context.WorldTick,
                context.RegionIndex,
                definition.Id,
                WorldEventDomainEventKind.Activated,
                active.PhaseId,
                active.Progress) with
            {
                TriggerSequence = active.TriggerSequence
            };
            return new WorldEventExecutionResult(active, [triggered, activated]);
        }

        var status = cooldowns.Count > 0
            ? WorldEventRuntimeStatus.Cooldown
            : WorldEventRuntimeStatus.Inactive;
        return new WorldEventExecutionResult(
            previous with
            {
                LastAdvancedTick = context.WorldTick,
                RegionIndex = context.RegionIndex,
                BiomeId = context.BiomeId,
                SubBiomeId = context.SubBiomeId,
                Status = status,
                Cooldowns = cooldowns
            },
            Array.Empty<WorldEventDomainEvent>());
    }

    public static void ValidateSnapshot(WorldEventRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != WorldEventRuntimeSnapshot.CurrentFormatVersion ||
            snapshot.LastAdvancedTick < 0 || string.IsNullOrWhiteSpace(snapshot.BiomeId) ||
            snapshot.StartTick < 0 || snapshot.EndTickExclusive < snapshot.StartTick ||
            !float.IsFinite(snapshot.Progress) || snapshot.Progress is < 0f or > 1f ||
            !float.IsFinite(snapshot.PhaseProgress) || snapshot.PhaseProgress is < 0f or > 1f ||
            !float.IsFinite(snapshot.Intensity) || snapshot.Intensity < 0f ||
            snapshot.TriggerSequence < 0 ||
            (snapshot.Status == WorldEventRuntimeStatus.Active &&
             (string.IsNullOrWhiteSpace(snapshot.ActiveEventId) || snapshot.EndTickExclusive <= snapshot.StartTick)) ||
            (snapshot.Status != WorldEventRuntimeStatus.Active && snapshot.ActiveEventId is not null) ||
            (snapshot.Status == WorldEventRuntimeStatus.Active &&
             snapshot.ActivationSource == WorldEventActivationSource.PlayerAction &&
             (snapshot.TriggerAction is null || snapshot.TriggerSequence <= 0)) ||
            (snapshot.Status == WorldEventRuntimeStatus.Active &&
             snapshot.ActivationSource == WorldEventActivationSource.Schedule &&
             (snapshot.TriggerAction is not null || snapshot.TriggerSequence != 0)) ||
            (snapshot.Status != WorldEventRuntimeStatus.Active &&
             (snapshot.TriggerAction is not null || snapshot.TriggerSequence != 0)))
        {
            throw new InvalidDataException("World-event runtime snapshot is invalid or unsupported.");
        }

        WorldEventModifierSet.Validate(snapshot.EffectiveModifiers, "runtime-snapshot");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cooldown in snapshot.Cooldowns)
        {
            if (string.IsNullOrWhiteSpace(cooldown.EventId) || !ids.Add(cooldown.EventId) ||
                cooldown.UntilTickExclusive < 0)
            {
                throw new InvalidDataException("World-event cooldown snapshot is invalid.");
            }
        }
    }

    public WorldEventRuntimeSnapshot NormalizeForRegistry(WorldEventRuntimeSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        var cooldowns = new List<WorldEventCooldownState>(snapshot.Cooldowns.Count);
        for (var index = 0; index < snapshot.Cooldowns.Count; index++)
        {
            var cooldown = snapshot.Cooldowns[index];
            if (_registry.TryGetById(cooldown.EventId, out _))
            {
                cooldowns.Add(cooldown);
            }
        }

        var normalizedCooldowns = cooldowns.Count == 0
            ? Array.Empty<WorldEventCooldownState>()
            : cooldowns.ToArray();
        if (snapshot.Status != WorldEventRuntimeStatus.Active ||
            snapshot.ActiveEventId is null ||
            _registry.TryGetById(snapshot.ActiveEventId, out _))
        {
            return snapshot with { Cooldowns = normalizedCooldowns };
        }

        return snapshot with
        {
            Status = normalizedCooldowns.Length > 0
                ? WorldEventRuntimeStatus.Cooldown
                : WorldEventRuntimeStatus.Inactive,
            ActiveEventId = null,
            StartTick = 0,
            EndTickExclusive = 0,
            PhaseId = null,
            PhaseIndex = -1,
            Progress = 0f,
            PhaseProgress = 0f,
            Intensity = 0f,
            EffectiveModifiers = WorldEventModifierSet.Identity,
            Cooldowns = normalizedCooldowns,
            ActivationSource = WorldEventActivationSource.Schedule,
            TriggerAction = null,
            TriggerSequence = 0
        };
    }

    private WorldEventExecutionResult AdvanceActive(
        WorldEventRuntimeSnapshot previous,
        in WorldEventExecutionContext context,
        List<WorldEventDomainEvent> events,
        ref long nextSequence)
    {
        var definition = _registry.GetById(previous.ActiveEventId!);
        if (!IsAllowed(definition, context))
        {
            events.Add(CreateEvent(
                ref nextSequence,
                context.WorldTick,
                previous.RegionIndex,
                definition.Id,
                WorldEventDomainEventKind.Cancelled,
                previous.PhaseId,
                previous.Progress));
            return Complete(previous, context, definition, events, ref nextSequence, cancelled: true);
        }

        if (context.WorldTick >= previous.EndTickExclusive)
        {
            events.Add(CreateEvent(
                ref nextSequence,
                previous.EndTickExclusive,
                previous.RegionIndex,
                definition.Id,
                WorldEventDomainEventKind.Completed,
                previous.PhaseId,
                1f));
            return Complete(previous, context, definition, events, ref nextSequence, cancelled: false);
        }

        var duration = previous.EndTickExclusive - previous.StartTick;
        var progress = duration <= 0
            ? 1f
            : Math.Clamp((context.WorldTick - previous.StartTick) / (float)duration, 0f, 1f);
        ResolvePhase(definition, progress, out var phase, out var phaseIndex, out var phaseProgress);
        var phaseId = phase?.Id ?? "active";
        if (!string.Equals(previous.PhaseId, phaseId, StringComparison.OrdinalIgnoreCase))
        {
            events.Add(CreateEvent(
                ref nextSequence,
                context.WorldTick,
                context.RegionIndex,
                definition.Id,
                WorldEventDomainEventKind.PhaseChanged,
                phaseId,
                progress));
        }

        if ((int)(progress * 10f) > (int)(previous.Progress * 10f))
        {
            events.Add(CreateEvent(
                ref nextSequence,
                context.WorldTick,
                context.RegionIndex,
                definition.Id,
                WorldEventDomainEventKind.Progressed,
                phaseId,
                progress));
        }

        var phaseModifiers = phase?.Modifiers ?? WorldEventModifierSet.Identity;
        var snapshot = previous with
        {
            LastAdvancedTick = context.WorldTick,
            BiomeId = context.BiomeId,
            SubBiomeId = context.SubBiomeId,
            PhaseId = phaseId,
            PhaseIndex = phaseIndex,
            Progress = progress,
            PhaseProgress = phaseProgress,
            EffectiveModifiers = WorldEventModifierSet.Compose(
                definition.Modifiers,
                phaseModifiers,
                definition.Intensity),
            Cooldowns = RemoveExpiredCooldowns(previous.Cooldowns, context.WorldTick)
        };
        return new WorldEventExecutionResult(snapshot, events.ToArray());
    }

    private WorldEventExecutionResult Complete(
        WorldEventRuntimeSnapshot previous,
        in WorldEventExecutionContext context,
        WorldEventDefinition definition,
        List<WorldEventDomainEvent> events,
        ref long nextSequence,
        bool cancelled)
    {
        var windowEnd = previous.ActivationSource == WorldEventActivationSource.Schedule
            ? checked(((previous.StartTick / _windowTicks) + 1) * (long)_windowTicks)
            : previous.EndTickExclusive;
        var cooldownUntil = Math.Max(
            windowEnd,
            checked((cancelled ? context.WorldTick : previous.EndTickExclusive) + definition.CooldownTicks));
        var cooldowns = UpsertCooldown(previous.Cooldowns, definition.Id, cooldownUntil, context.WorldTick);
        events.Add(new WorldEventDomainEvent(
            nextSequence++,
            context.WorldTick,
            context.RegionIndex,
            definition.Id,
            WorldEventDomainEventKind.CooldownStarted,
            null,
            1f,
            cooldownUntil));
        var snapshot = previous with
        {
            LastAdvancedTick = context.WorldTick,
            BiomeId = context.BiomeId,
            SubBiomeId = context.SubBiomeId,
            Status = WorldEventRuntimeStatus.Cooldown,
            ActiveEventId = null,
            LastEventId = definition.Id,
            StartTick = 0,
            EndTickExclusive = 0,
            PhaseId = null,
            PhaseIndex = -1,
            Progress = 0f,
            PhaseProgress = 0f,
            Intensity = 0f,
            EffectiveModifiers = WorldEventModifierSet.Identity,
            Cooldowns = cooldowns,
            ActivationSource = WorldEventActivationSource.Schedule,
            TriggerAction = null,
            TriggerSequence = 0
        };
        return new WorldEventExecutionResult(snapshot, events.ToArray());
    }

    private bool TrySelectScheduledEvent(
        in WorldEventExecutionContext context,
        IReadOnlyList<WorldEventCooldownState> cooldowns,
        out WorldEventDefinition definition,
        out EventSchedule schedule)
    {
        var window = context.WorldTick / _windowTicks;
        foreach (var candidate in _registry.Definitions)
        {
            if (!IsAllowed(candidate, context) || IsCoolingDown(cooldowns, candidate.Id, context.WorldTick))
            {
                continue;
            }

            var salt = DeterministicCoordinateHash.Salt(candidate.Id) ^ ScheduleSalt;
            if (DeterministicCoordinateHash.Unit(_seed, context.RegionIndex, window, salt) >=
                candidate.ChancePerWindow)
            {
                continue;
            }

            var duration = DeterministicCoordinateHash.Range(
                _seed,
                context.RegionIndex,
                window,
                salt + 1,
                candidate.MinDurationTicks,
                candidate.MaxDurationTicks);
            var windowStart = window * (long)_windowTicks;
            var startOffset = DeterministicCoordinateHash.Range(
                _seed,
                context.RegionIndex,
                window,
                salt + 2,
                0,
                Math.Max(0, _windowTicks - duration));
            var start = windowStart + startOffset;
            var end = start + duration;
            if (context.WorldTick < start || context.WorldTick >= end)
            {
                continue;
            }

            definition = candidate;
            schedule = new EventSchedule(start, end);
            return true;
        }

        definition = null!;
        schedule = default;
        return false;
    }

    private static WorldEventRuntimeSnapshot CreateActiveSnapshot(
        in WorldEventExecutionContext context,
        WorldEventDefinition definition,
        in EventSchedule schedule,
        IReadOnlyList<WorldEventCooldownState> cooldowns)
    {
        var duration = schedule.EndTickExclusive - schedule.StartTick;
        var progress = duration <= 0
            ? 1f
            : Math.Clamp((context.WorldTick - schedule.StartTick) / (float)duration, 0f, 1f);
        ResolvePhase(definition, progress, out var phase, out var phaseIndex, out var phaseProgress);
        return new WorldEventRuntimeSnapshot
        {
            LastAdvancedTick = context.WorldTick,
            RegionIndex = context.RegionIndex,
            BiomeId = context.BiomeId,
            SubBiomeId = context.SubBiomeId,
            Status = WorldEventRuntimeStatus.Active,
            ActiveEventId = definition.Id,
            LastEventId = definition.Id,
            StartTick = schedule.StartTick,
            EndTickExclusive = schedule.EndTickExclusive,
            PhaseId = phase?.Id ?? "active",
            PhaseIndex = phaseIndex,
            Progress = progress,
            PhaseProgress = phaseProgress,
            Intensity = definition.Intensity,
            EffectiveModifiers = WorldEventModifierSet.Compose(
                definition.Modifiers,
                phase?.Modifiers ?? WorldEventModifierSet.Identity,
                definition.Intensity),
            Cooldowns = cooldowns,
            ActivationSource = WorldEventActivationSource.Schedule
        };
    }

    private static void ResolvePhase(
        WorldEventDefinition definition,
        float progress,
        out WorldEventPhaseDefinition? phase,
        out int phaseIndex,
        out float phaseProgress)
    {
        for (var index = 0; index < definition.Phases.Count; index++)
        {
            var candidate = definition.Phases[index];
            if (progress < candidate.StartProgress ||
                (progress >= candidate.EndProgress && index != definition.Phases.Count - 1))
            {
                continue;
            }

            phase = candidate;
            phaseIndex = index;
            phaseProgress = Math.Clamp(
                (progress - candidate.StartProgress) /
                (candidate.EndProgress - candidate.StartProgress),
                0f,
                1f);
            return;
        }

        phase = null;
        phaseIndex = -1;
        phaseProgress = progress;
    }

    private static bool IsAllowed(
        WorldEventDefinition definition,
        in WorldEventExecutionContext context)
    {
        return ContainsOrEmpty(definition.AllowedBiomeIds, context.BiomeId) &&
               (definition.AllowedSubBiomeIds.Count == 0 ||
                (context.SubBiomeId is not null && Contains(definition.AllowedSubBiomeIds, context.SubBiomeId))) &&
               ContainsOrEmpty(definition.AllowedWeatherIds, context.WeatherId) &&
               (!definition.RequiresNight || context.IsNight) &&
               (!definition.RequiresUnderground || context.IsUnderground) &&
               context.WeatherIntensity >= definition.MinimumWeatherIntensity;
    }

    private static bool ContainsOrEmpty(IReadOnlyList<string> values, string value)
    {
        return values.Count == 0 || Contains(values, value);
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

    private static bool ContainsAction(
        IReadOnlyList<WorldEventPlayerActionKind> values,
        WorldEventPlayerActionKind value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<WorldEventCooldownState> RemoveExpiredCooldowns(
        IReadOnlyList<WorldEventCooldownState> cooldowns,
        long tick)
    {
        if (cooldowns.Count == 0)
        {
            return Array.Empty<WorldEventCooldownState>();
        }

        var active = new List<WorldEventCooldownState>(cooldowns.Count);
        for (var index = 0; index < cooldowns.Count; index++)
        {
            if (cooldowns[index].UntilTickExclusive > tick)
            {
                active.Add(cooldowns[index]);
            }
        }

        return active.ToArray();
    }

    private static IReadOnlyList<WorldEventCooldownState> UpsertCooldown(
        IReadOnlyList<WorldEventCooldownState> cooldowns,
        string eventId,
        long untilTick,
        long currentTick)
    {
        var result = new List<WorldEventCooldownState>(cooldowns.Count + 1);
        for (var index = 0; index < cooldowns.Count; index++)
        {
            if (cooldowns[index].UntilTickExclusive > currentTick &&
                !string.Equals(cooldowns[index].EventId, eventId, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(cooldowns[index]);
            }
        }

        result.Add(new WorldEventCooldownState(eventId, untilTick));
        result.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.EventId, right.EventId));
        return result.ToArray();
    }

    private static bool IsCoolingDown(
        IReadOnlyList<WorldEventCooldownState> cooldowns,
        string eventId,
        long tick)
    {
        for (var index = 0; index < cooldowns.Count; index++)
        {
            if (cooldowns[index].UntilTickExclusive > tick &&
                string.Equals(cooldowns[index].EventId, eventId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static WorldEventDomainEvent CreateEvent(
        ref long sequence,
        long worldTick,
        long regionIndex,
        string eventId,
        WorldEventDomainEventKind kind,
        string? phaseId,
        float progress)
    {
        return new WorldEventDomainEvent(
            sequence++,
            worldTick,
            regionIndex,
            eventId,
            kind,
            phaseId,
            progress,
            0);
    }

    private readonly record struct EventSchedule(long StartTick, long EndTickExclusive);
}
