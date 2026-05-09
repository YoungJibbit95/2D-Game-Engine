using Game.Core.World.Liquids;

namespace Game.Core.World.Simulation;

public readonly record struct WorldSimulationTickResult(
    LiquidSimulationResult Liquids,
    int LiquidRegionsProcessed,
    IReadOnlyList<RectI> RenderDirtyRegions,
    IReadOnlyList<RectI> LightDirtyRegions,
    IReadOnlyList<RectI> LiquidDirtyRegions)
{
    public static WorldSimulationTickResult None { get; } = new(
        LiquidSimulationResult.None,
        0,
        Array.Empty<RectI>(),
        Array.Empty<RectI>(),
        Array.Empty<RectI>());
}
