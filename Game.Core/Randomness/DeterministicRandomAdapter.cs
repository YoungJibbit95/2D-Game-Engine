namespace Game.Core.Randomness;

public sealed class DeterministicRandomAdapter : Random
{
    public DeterministicRandomAdapter(DeterministicRandomStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Stream = stream;
    }

    public DeterministicRandomStream Stream { get; }

    public override int Next()
    {
        return Stream.NextInt32();
    }

    public override int Next(int maxValue)
    {
        return Stream.NextInt32(maxValue);
    }

    public override int Next(int minValue, int maxValue)
    {
        return Stream.NextInt32(minValue, maxValue);
    }

    public override long NextInt64()
    {
        return (long)NextUInt64((ulong)long.MaxValue);
    }

    public override long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxValue);
        return (long)NextUInt64((ulong)maxValue);
    }

    public override long NextInt64(long minValue, long maxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxValue),
                maxValue,
                "The exclusive maximum must be greater than the inclusive minimum.");
        }

        var range = unchecked((ulong)maxValue - (ulong)minValue);
        return unchecked((long)(NextUInt64(range) + (ulong)minValue));
    }

    public override double NextDouble()
    {
        return Stream.NextDouble();
    }

    public override float NextSingle()
    {
        return Stream.NextSingle();
    }

    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Stream.Fill(buffer);
    }

    public override void NextBytes(Span<byte> buffer)
    {
        Stream.Fill(buffer);
    }

    protected override double Sample()
    {
        return Stream.NextDouble();
    }

    private ulong NextUInt64(ulong exclusiveMax)
    {
        var rejectionThreshold = unchecked(0UL - exclusiveMax) % exclusiveMax;
        ulong candidate;
        do
        {
            candidate = Stream.NextUInt64();
        }
        while (candidate < rejectionThreshold);

        return candidate % exclusiveMax;
    }
}
