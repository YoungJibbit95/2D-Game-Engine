namespace Game.Core.Randomness;

public sealed record RandomRegistrySnapshot
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required ulong SessionSeed { get; init; }

    public IReadOnlyList<RandomStreamSnapshot> Streams { get; init; } = [];
}
