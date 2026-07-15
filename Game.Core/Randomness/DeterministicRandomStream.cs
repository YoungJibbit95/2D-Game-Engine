using System.Numerics;

namespace Game.Core.Randomness;

public sealed class DeterministicRandomStream
{
    private ulong _state0;
    private ulong _state1;
    private ulong _state2;
    private ulong _state3;

    internal DeterministicRandomStream(RandomStreamSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        Name = snapshot.Name;
        Restore(snapshot);
    }

    public string Name { get; }

    public ulong DrawCount { get; private set; }

    public ulong NextUInt64()
    {
        unchecked
        {
            var result = BitOperations.RotateLeft(_state1 * 5, 7) * 9;
            var temporary = _state1 << 17;

            _state2 ^= _state0;
            _state3 ^= _state1;
            _state1 ^= _state2;
            _state0 ^= _state3;

            _state2 ^= temporary;
            _state3 = BitOperations.RotateLeft(_state3, 45);
            DrawCount++;
            return result;
        }
    }

    public int NextInt32()
    {
        return NextInt32(int.MaxValue);
    }

    public int NextInt32(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        return (int)NextUInt32((uint)exclusiveMax);
    }

    public int NextInt32(int inclusiveMin, int exclusiveMax)
    {
        if (inclusiveMin >= exclusiveMax)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exclusiveMax),
                exclusiveMax,
                "The exclusive maximum must be greater than the inclusive minimum.");
        }

        var range = (uint)((long)exclusiveMax - inclusiveMin);
        return (int)(inclusiveMin + (long)NextUInt32(range));
    }

    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    public float NextSingle()
    {
        return (NextUInt64() >> 40) * (1.0f / (1U << 24));
    }

    public bool NextBoolean()
    {
        return (NextUInt64() & (1UL << 63)) != 0;
    }

    public void Fill(Span<byte> destination)
    {
        var offset = 0;
        while (destination.Length - offset >= sizeof(ulong))
        {
            var value = NextUInt64();
            for (var byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            {
                destination[offset + byteIndex] = (byte)(value >> (byteIndex * 8));
            }

            offset += sizeof(ulong);
        }

        if (offset == destination.Length)
        {
            return;
        }

        var remainder = NextUInt64();
        for (var byteIndex = 0; offset < destination.Length; byteIndex++, offset++)
        {
            destination[offset] = (byte)(remainder >> (byteIndex * 8));
        }
    }

    internal RandomStreamSnapshot ExportSnapshot()
    {
        return new RandomStreamSnapshot
        {
            Name = Name,
            State0 = _state0,
            State1 = _state1,
            State2 = _state2,
            State3 = _state3,
            DrawCount = DrawCount
        };
    }

    internal void Restore(RandomStreamSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        if (!string.Equals(Name, snapshot.Name, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Cannot restore random stream '{snapshot.Name}' into stream '{Name}'.");
        }

        _state0 = snapshot.State0;
        _state1 = snapshot.State1;
        _state2 = snapshot.State2;
        _state3 = snapshot.State3;
        DrawCount = snapshot.DrawCount;
    }

    internal static void ValidateSnapshot(RandomStreamSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.State0 == 0 &&
            snapshot.State1 == 0 &&
            snapshot.State2 == 0 &&
            snapshot.State3 == 0)
        {
            throw new InvalidDataException($"Random stream '{snapshot.Name}' has an invalid all-zero state.");
        }
    }

    private uint NextUInt32(uint exclusiveMax)
    {
        var rejectionThreshold = unchecked(0U - exclusiveMax) % exclusiveMax;
        uint candidate;
        do
        {
            candidate = (uint)(NextUInt64() >> 32);
        }
        while (candidate < rejectionThreshold);

        return candidate % exclusiveMax;
    }
}
