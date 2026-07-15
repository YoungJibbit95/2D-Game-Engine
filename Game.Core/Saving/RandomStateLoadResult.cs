using Game.Core.Randomness;

namespace Game.Core.Saving;

public enum RandomStateLoadSource
{
    Primary,
    BackupRecovery,
    LegacyFallback
}

public sealed record RandomStateLoadResult
{
    public required SessionRandomRegistry Registry { get; init; }

    public required RandomStateLoadSource Source { get; init; }

    public string? Warning { get; init; }
}
