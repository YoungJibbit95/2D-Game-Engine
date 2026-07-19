namespace Game.Client.Rendering.Effects;

public readonly record struct ParticleAnimationSample(
    float NormalizedAge,
    float Opacity,
    float Scale,
    float Sway);

public static class ParticleAnimationPlanner
{
    public static ParticleAnimationSample Sample(
        float age,
        float lifetime,
        float phase,
        float pulse,
        float swayAmplitude)
    {
        if (!float.IsFinite(age) || !float.IsFinite(lifetime) || lifetime <= 0f)
        {
            return default;
        }

        var normalizedAge = Math.Clamp(age / lifetime, 0f, 1f);
        var fadeIn = Math.Clamp(normalizedAge * 10f, 0f, 1f);
        var fadeOut = 1f - normalizedAge * normalizedAge;
        var oscillation = MathF.Sin(phase + normalizedAge * MathF.Tau * 1.5f);
        var scale = Math.Max(0.1f, 1f + oscillation * Math.Clamp(pulse, 0f, 0.8f));
        return new ParticleAnimationSample(
            normalizedAge,
            Math.Clamp(fadeIn * fadeOut, 0f, 1f),
            scale,
            oscillation * Math.Max(0f, swayAmplitude));
    }
}
