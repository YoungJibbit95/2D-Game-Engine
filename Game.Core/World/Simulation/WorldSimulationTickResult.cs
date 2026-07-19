using Game.Core.World.Liquids;

namespace Game.Core.World.Simulation;

/// <summary>
/// Ephemeral fixed-tick result. <see cref="LiquidSimulationResult.ChangedRegions"/>
/// is workspace-owned and must be consumed or copied before the next liquid step.
/// Render, light and pending-liquid region lists follow the same tick-local contract.
/// </summary>
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
