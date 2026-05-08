namespace Game.Core.World.Generation;

public sealed class FastNoiseLiteNoiseService : INoiseService
{
    private readonly FastNoiseLite _noise;

    public FastNoiseLiteNoiseService(int seed, float frequency = 0.02f)
    {
        _noise = new FastNoiseLite(seed);
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _noise.SetFrequency(frequency);
    }

    public float GetNoise(float x, float y)
    {
        return _noise.GetNoise(x, y);
    }
}
