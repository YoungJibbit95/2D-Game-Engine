namespace Game.Core.Interaction;

public sealed record MiningTuning
{
    public static MiningTuning Default { get; } = new();

    public float BaseSpeedMultiplier { get; init; } = 1.6f;

    public float ToolPowerForDoubleSpeed { get; init; } = 100f;

    public float MinimumHardness { get; init; } = 0.05f;

    public void Validate()
    {
        if (!float.IsFinite(BaseSpeedMultiplier) || BaseSpeedMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseSpeedMultiplier));
        }

        if (!float.IsFinite(ToolPowerForDoubleSpeed) || ToolPowerForDoubleSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ToolPowerForDoubleSpeed));
        }

        if (!float.IsFinite(MinimumHardness) || MinimumHardness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumHardness));
        }
    }
}
