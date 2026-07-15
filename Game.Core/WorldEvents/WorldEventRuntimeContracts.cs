using Game.Core.World;

namespace Game.Core.WorldEvents;

public enum WorldEventRuntimeStatus
{
    Inactive,
    Active,
    Cooldown
}

public enum WorldEventDomainEventKind
{
    PlayerActionTriggered,
    Activated,
    PhaseChanged,
    Progressed,
    Completed,
    CooldownStarted,
    Cancelled
}

public enum WorldEventPlayerActionKind
{
    Mine,
    Build,
    Melee,
    Shoot,
    Cast,
    Consume,
    Farm
}

public enum WorldEventActivationSource
{
    Schedule,
    PlayerAction
}

public readonly record struct WorldEventExecutionContext(
    long WorldTick,
    long RegionIndex,
    string BiomeId,
    string? SubBiomeId,
    string WeatherId,
    float WeatherIntensity,
    bool IsNight,
    bool IsUnderground,
    float Daylight,
    TilePos FocusTile)
{
    public void Validate()
    {
        if (WorldTick < 0 || string.IsNullOrWhiteSpace(BiomeId) ||
            string.IsNullOrWhiteSpace(WeatherId) ||
            !float.IsFinite(WeatherIntensity) || WeatherIntensity is < 0f or > 1f ||
            !float.IsFinite(Daylight) || Daylight is < 0f or > 1f)
        {
            throw new ArgumentException("World-event execution context is invalid.");
        }
    }
}

public readonly record struct WorldEventCooldownState(string EventId, long UntilTickExclusive);

public sealed record WorldEventRuntimeSnapshot
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public long LastAdvancedTick { get; init; }

    public long RegionIndex { get; init; }

    public required string BiomeId { get; init; }

    public string? SubBiomeId { get; init; }

    public WorldEventRuntimeStatus Status { get; init; }

    public string? ActiveEventId { get; init; }

    public string? LastEventId { get; init; }

    public long StartTick { get; init; }

    public long EndTickExclusive { get; init; }

    public string? PhaseId { get; init; }

    public int PhaseIndex { get; init; } = -1;

    public float Progress { get; init; }

    public float PhaseProgress { get; init; }

    public float Intensity { get; init; }

    public WorldEventModifierSet EffectiveModifiers { get; init; } = WorldEventModifierSet.Identity;

    public IReadOnlyList<WorldEventCooldownState> Cooldowns { get; init; } =
        Array.Empty<WorldEventCooldownState>();

    public WorldEventActivationSource ActivationSource { get; init; }

    public WorldEventPlayerActionKind? TriggerAction { get; init; }

    public long TriggerSequence { get; init; }

    public static WorldEventRuntimeSnapshot Inactive(in WorldEventExecutionContext context)
    {
        context.Validate();
        return new WorldEventRuntimeSnapshot
        {
            LastAdvancedTick = context.WorldTick,
            RegionIndex = context.RegionIndex,
            BiomeId = context.BiomeId,
            SubBiomeId = context.SubBiomeId,
            Status = WorldEventRuntimeStatus.Inactive
        };
    }
}

public readonly record struct WorldEventDomainEvent(
    long Sequence,
    long WorldTick,
    long RegionIndex,
    string EventId,
    WorldEventDomainEventKind Kind,
    string? PhaseId,
    float Progress,
    long CooldownUntilTickExclusive)
{
    public long TriggerSequence { get; init; }
}

public readonly record struct WorldEventExecutionResult(
    WorldEventRuntimeSnapshot Snapshot,
    IReadOnlyList<WorldEventDomainEvent> Events);

public sealed record WorldEventRuntimeStateSnapshot
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required WorldEventRuntimeSnapshot Runtime { get; init; }

    public required WorldEventJournalSnapshot Journal { get; init; }

    public long LastProcessedPlayerActionSequence { get; init; }

    public static void Validate(WorldEventRuntimeStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != CurrentFormatVersion ||
            snapshot.LastProcessedPlayerActionSequence < 0 ||
            snapshot.Runtime.TriggerSequence > snapshot.LastProcessedPlayerActionSequence)
        {
            throw new InvalidDataException("World-event state snapshot is invalid or unsupported.");
        }

        DeterministicWorldEventExecutor.ValidateSnapshot(snapshot.Runtime);
        WorldEventJournal.Validate(snapshot.Journal);
        for (var index = 0; index < snapshot.Journal.Entries.Count; index++)
        {
            if (snapshot.Journal.Entries[index].WorldTick > snapshot.Runtime.LastAdvancedTick)
            {
                throw new InvalidDataException("World-event journal contains entries newer than its runtime snapshot.");
            }

            if (snapshot.Journal.Entries[index].TriggerSequence >
                snapshot.LastProcessedPlayerActionSequence)
            {
                throw new InvalidDataException(
                    "World-event journal contains an unprocessed player-action sequence.");
            }
        }
    }
}

public readonly record struct WorldEventPlayerActionTriggerResult(
    bool Processed,
    bool Activated,
    long Sequence,
    WorldEventPlayerActionKind Action,
    string? EventId)
{
    public static WorldEventPlayerActionTriggerResult Duplicate(
        long sequence,
        WorldEventPlayerActionKind action)
    {
        return new WorldEventPlayerActionTriggerResult(false, false, sequence, action, null);
    }
}

public interface IWorldEventExecutor
{
    WorldEventExecutionResult Advance(
        WorldEventRuntimeSnapshot previous,
        in WorldEventExecutionContext context);
}
