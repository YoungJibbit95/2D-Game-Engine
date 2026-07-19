namespace Game.Core.World.Liquids;

public readonly record struct LiquidSimulationResult(
    int ChangedTiles,
    int MovedLiquid,
    /// <summary>
    /// Reusable workspace-owned view. Consumers must read or copy it before
    /// the next step executed with the same workspace.
    /// </summary>
    IReadOnlyList<RectI> ChangedRegions)
{
    public static LiquidSimulationResult None { get; } = new(0, 0, Array.Empty<RectI>());

    public int ActiveCellsAtStart { get; init; }

    public int SeedTilesChecked { get; init; }

    public int SeededCells { get; init; }

    public int ProcessedCells { get; init; }

    public int TransferOperations { get; init; }

    public int SuccessfulTransfers { get; init; }

    public int PendingActiveCells { get; init; }

    public int PendingSeedRegions { get; init; }

    public int DroppedActivations { get; init; }

    public int DroppedSeedRegions { get; init; }

    public bool CellBudgetExhausted { get; init; }

    public bool TransferBudgetExhausted { get; init; }

    public bool SeedBudgetExhausted { get; init; }

    public bool CapacityExhausted => DroppedActivations > 0 || DroppedSeedRegions > 0;

    public bool BudgetExhausted =>
        CellBudgetExhausted ||
        TransferBudgetExhausted ||
        SeedBudgetExhausted ||
        CapacityExhausted;

    public bool HasPendingWork => PendingActiveCells > 0 || PendingSeedRegions > 0;

    public LiquidSimulationResult Add(LiquidSimulationResult other)
    {
        var tracker = new DirtyRegionTracker();
        tracker.AddRange(ChangedRegions);
        tracker.AddRange(other.ChangedRegions);
        return new LiquidSimulationResult(
            ChangedTiles + other.ChangedTiles,
            MovedLiquid + other.MovedLiquid,
            tracker.DrainMerged())
        {
            ActiveCellsAtStart = ActiveCellsAtStart + other.ActiveCellsAtStart,
            SeedTilesChecked = SeedTilesChecked + other.SeedTilesChecked,
            SeededCells = SeededCells + other.SeededCells,
            ProcessedCells = ProcessedCells + other.ProcessedCells,
            TransferOperations = TransferOperations + other.TransferOperations,
            SuccessfulTransfers = SuccessfulTransfers + other.SuccessfulTransfers,
            PendingActiveCells = other.PendingActiveCells,
            PendingSeedRegions = other.PendingSeedRegions,
            DroppedActivations = DroppedActivations + other.DroppedActivations,
            DroppedSeedRegions = DroppedSeedRegions + other.DroppedSeedRegions,
            CellBudgetExhausted = CellBudgetExhausted || other.CellBudgetExhausted,
            TransferBudgetExhausted = TransferBudgetExhausted || other.TransferBudgetExhausted,
            SeedBudgetExhausted = SeedBudgetExhausted || other.SeedBudgetExhausted
        };
    }
}
