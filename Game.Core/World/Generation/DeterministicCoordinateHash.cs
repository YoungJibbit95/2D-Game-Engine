namespace Game.Core.World.Generation;

internal static class DeterministicCoordinateHash
{
    public static ulong Hash(int seed, long x, long y = 0, ulong salt = 0)
    {
        unchecked
        {
            var value = (ulong)(uint)seed ^ salt ^ 0x9E3779B97F4A7C15UL;
            value = Mix(value ^ (ulong)x);
            return Mix(value ^ RotateLeft((ulong)y, 32));
        }
    }

    public static double Unit(int seed, long x, long y = 0, ulong salt = 0)
    {
        return (Hash(seed, x, y, salt) >> 11) * (1.0 / (1UL << 53));
    }

    public static int Range(int seed, long x, long y, ulong salt, int minimum, int maximumInclusive)
    {
        if (maximumInclusive < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumInclusive));
        }

        var range = (ulong)((long)maximumInclusive - minimum + 1);
        return (int)((long)minimum + (long)(Hash(seed, x, y, salt) % range));
    }

    public static long LongRange(int seed, long x, long y, ulong salt, long minimum, long maximumInclusive)
    {
        if (maximumInclusive < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumInclusive));
        }

        var range = unchecked((ulong)(maximumInclusive - minimum)) + 1UL;
        var offset = range == 0UL ? Hash(seed, x, y, salt) : Hash(seed, x, y, salt) % range;
        return unchecked((long)((ulong)minimum + offset));
    }

    public static long FloorDivide(long value, int divisor)
    {
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor));
        }

        var quotient = value / divisor;
        return value % divisor < 0 ? quotient - 1 : quotient;
    }

    public static ulong Salt(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        unchecked
        {
            var hash = 14695981039346656037UL;
            foreach (var character in value)
            {
                hash ^= char.ToUpperInvariant(character);
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong RotateLeft(ulong value, int count)
    {
        return (value << count) | (value >> (64 - count));
    }
}
