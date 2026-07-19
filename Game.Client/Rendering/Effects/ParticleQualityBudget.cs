namespace Game.Client.Rendering.Effects;

public readonly record struct ParticleQualityBudget(
    int MaximumParticles,
    int MaximumEmissionsPerCall,
    int MaximumAmbientEmissionsPerTick,
    int MaximumDrawPrimitives,
    int AmbientTickStride)
{
    public const int AbsoluteMaximumParticles = 1_536;

    public static ParticleQualityBudget ForQuality(int quality)
    {
        return quality switch
        {
            <= 0 => default,
            1 => new ParticleQualityBudget(192, 12, 4, 224, 3),
            2 => new ParticleQualityBudget(640, 28, 10, 960, 2),
            _ => new ParticleQualityBudget(AbsoluteMaximumParticles, 48, 24, 3_072, 1)
        };
    }
}
