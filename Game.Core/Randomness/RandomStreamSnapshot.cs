namespace Game.Core.Randomness;

public sealed record RandomStreamSnapshot
{
    public required string Name { get; init; }

    public required ulong State0 { get; init; }

    public required ulong State1 { get; init; }

    public required ulong State2 { get; init; }

    public required ulong State3 { get; init; }

    public required ulong DrawCount { get; init; }
}
