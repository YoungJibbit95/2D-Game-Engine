using Game.Core.WorldEvents;

namespace Game.Core.Saving;

public enum WorldEventStateLoadSource
{
    Primary,
    BackupRecovery,
    LegacyFallback
}

public sealed record WorldEventStateLoadResult
{
    public WorldEventRuntimeStateSnapshot? State { get; init; }

    public required WorldEventStateLoadSource Source { get; init; }

    public string? Warning { get; init; }
}
