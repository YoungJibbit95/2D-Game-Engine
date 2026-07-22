namespace Game.Core.Particles;

internal static class ParticleDeterministicRandom
{
    private const float UnitFloat = 1f / (1U << 24);

    public static ulong CreateState(ulong seed, ulong sequence)
    {
        return Mix(seed ^ Mix(sequence + 0x9E3779B97F4A7C15UL));
    }

    public static float NextSigned(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var value = Mix(state);
        var unit = (value >> 40) * UnitFloat;
        return (unit * 2f) - 1f;
    }

    private static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
