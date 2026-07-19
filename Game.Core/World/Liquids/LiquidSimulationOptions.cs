namespace Game.Core.World.Liquids;

public sealed record LiquidSimulationOptions(
    byte MaxLiquid = byte.MaxValue,
    byte MinimumHorizontalDifference = 8,
    byte MaxHorizontalFlow = 64)
{
    /// <summary>
    /// Maximum active liquid cells evaluated by one simulation step. Cells
    /// activated by a transfer remain queued for a later step.
    /// </summary>
    public int MaxCellsPerStep { get; init; } = 4_096;

    /// <summary>
    /// Maximum down/side transfer attempts performed by one step. Failed
    /// attempts count as operations so solid surroundings remain bounded.
    /// </summary>
    public int MaxTransferOperationsPerStep { get; init; } = 8_192;

    /// <summary>
    /// Maximum tiles inspected while incrementally converting compatibility
    /// regions into active cells.
    /// </summary>
    public int MaxSeedTileChecksPerStep { get; init; } = 16_384;

    public static LiquidSimulationOptions Default { get; } = new();
}
