namespace Game.Core.Saving;

public sealed record SimulationSaveData
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required int Day { get; init; }

    public required double TimeOfDaySeconds { get; init; }

    public required double DayLengthSeconds { get; init; }
}
