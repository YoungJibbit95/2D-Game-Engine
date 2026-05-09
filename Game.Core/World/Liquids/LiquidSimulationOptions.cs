namespace Game.Core.World.Liquids;

public sealed record LiquidSimulationOptions(
    byte MaxLiquid = byte.MaxValue,
    byte MinimumHorizontalDifference = 8,
    byte MaxHorizontalFlow = 64)
{
    public static LiquidSimulationOptions Default { get; } = new();
}
