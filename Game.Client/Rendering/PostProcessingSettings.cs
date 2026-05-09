namespace Game.Client.Rendering;

public sealed record PostProcessingSettings
{
    public bool Enabled { get; init; }

    public bool PixelSnap { get; init; } = true;

    public float BloomStrength { get; init; }

    public float VignetteStrength { get; init; }

    public float ColorGradeIntensity { get; init; }
}
