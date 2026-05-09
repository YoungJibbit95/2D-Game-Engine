using Game.Core.World.Liquids;

namespace Game.Core.World.Simulation;

public sealed record WorldSimulationOptions(
    float LiquidStepIntervalSeconds = 0.1f,
    int LiquidRegionPaddingTiles = 2,
    bool SeedExistingLiquids = true,
    LiquidSimulationOptions? LiquidOptions = null)
{
    public static WorldSimulationOptions Default { get; } = new();
}
