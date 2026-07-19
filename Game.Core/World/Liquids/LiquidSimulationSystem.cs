namespace Game.Core.World.Liquids;

/// <summary>
/// Deterministic, budgeted active-cell liquid simulation. Region overloads are
/// retained as compatibility adapters and incrementally seed the system-owned
/// workspace instead of scanning an unbounded region in one step.
/// </summary>
public sealed class LiquidSimulationSystem
{
    private readonly LiquidSimulationWorkspace _workspace;

    public LiquidSimulationSystem()
        : this(new LiquidSimulationWorkspace())
    {
    }

    public LiquidSimulationSystem(LiquidSimulationWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _workspace = workspace;
    }

    public int PendingActiveCellCount => _workspace.PendingActiveCellCount;

    public int PendingSeedRegionCount => _workspace.PendingSeedRegionCount;

    public bool HasPendingWork => _workspace.HasPendingWork;

    public bool Activate(TilePos position)
    {
        return _workspace.Activate(position);
    }

    public bool ActivateRegion(RectI region)
    {
        return _workspace.ActivateRegion(region);
    }

    public void Reset()
    {
        _workspace.Reset();
    }

    public LiquidSimulationResult Step(
        World world,
        RectI tileRegion,
        LiquidSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _workspace.Bind(world);

        if (!tileRegion.IsEmpty)
        {
            var clamped = world.ClampRegionToBounds(tileRegion);
            if (!clamped.IsEmpty)
            {
                _workspace.ActivateRegion(clamped);
            }
        }

        return StepCore(world, _workspace, options ?? LiquidSimulationOptions.Default);
    }

    public LiquidSimulationResult Step(
        World world,
        IEnumerable<RectI> tileRegions,
        LiquidSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tileRegions);
        _workspace.Bind(world);

        foreach (var region in tileRegions)
        {
            if (region.IsEmpty)
            {
                continue;
            }

            var clamped = world.ClampRegionToBounds(region);
            if (!clamped.IsEmpty)
            {
                _workspace.ActivateRegion(clamped);
            }
        }

        return StepCore(world, _workspace, options ?? LiquidSimulationOptions.Default);
    }

    public LiquidSimulationResult Step(
        World world,
        LiquidSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _workspace.Bind(world);
        return StepCore(world, _workspace, options ?? LiquidSimulationOptions.Default);
    }

    public LiquidSimulationResult Step(
        World world,
        LiquidSimulationWorkspace workspace,
        LiquidSimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(workspace);
        workspace.Bind(world);
        return StepCore(world, workspace, options ?? LiquidSimulationOptions.Default);
    }

    private static LiquidSimulationResult StepCore(
        World world,
        LiquidSimulationWorkspace workspace,
        LiquidSimulationOptions options)
    {
        ValidateOptions(options);
        workspace.BeginStep();

        var activeAtStart = workspace.PendingActiveCellCount;
        var seedTilesChecked = 0;
        var seededCells = 0;
        SeedActiveCells(
            world,
            workspace,
            options.MaxSeedTileChecksPerStep,
            ref seedTilesChecked,
            ref seededCells);

        var cellsAvailableThisStep = workspace.PendingActiveCellCount;
        var cellLimit = Math.Min(options.MaxCellsPerStep, cellsAvailableThisStep);
        var processedCells = 0;
        var transferOperations = 0;
        var successfulTransfers = 0;
        var movedLiquid = 0;

        while (processedCells < cellLimit && workspace.PendingActiveCellCount > 0)
        {
            if (transferOperations >= options.MaxTransferOperationsPerStep)
            {
                break;
            }

            var position = workspace.DequeueActive();
            processedCells++;
            ProcessCell(
                world,
                workspace,
                position,
                options,
                ref transferOperations,
                ref successfulTransfers,
                ref movedLiquid);
        }

        var dropped = workspace.ConsumeDroppedCounts();
        var changedRegions = workspace.BuildChangedRegions();
        if (dropped.Activations > 0)
        {
            for (var index = 0; index < changedRegions.Count; index++)
            {
                workspace.ActivateRegion(changedRegions[index]);
            }
        }

        var recoveryDrops = workspace.ConsumeDroppedCounts();
        dropped = (
            dropped.Activations + recoveryDrops.Activations,
            dropped.Regions + recoveryDrops.Regions);
        var pendingActive = workspace.PendingActiveCellCount;
        var pendingRegions = workspace.PendingSeedRegionCount;
        var transferBudgetExhausted =
            transferOperations >= options.MaxTransferOperationsPerStep &&
            pendingActive > 0;
        var cellBudgetExhausted =
            processedCells >= options.MaxCellsPerStep &&
            pendingActive > 0;
        var seedBudgetExhausted =
            seedTilesChecked >= options.MaxSeedTileChecksPerStep &&
            pendingRegions > 0;

        return new LiquidSimulationResult(
            workspace.ChangedTileCount,
            movedLiquid,
            changedRegions)
        {
            ActiveCellsAtStart = activeAtStart,
            SeedTilesChecked = seedTilesChecked,
            SeededCells = seededCells,
            ProcessedCells = processedCells,
            TransferOperations = transferOperations,
            SuccessfulTransfers = successfulTransfers,
            PendingActiveCells = pendingActive,
            PendingSeedRegions = pendingRegions,
            DroppedActivations = dropped.Activations,
            DroppedSeedRegions = dropped.Regions,
            CellBudgetExhausted = cellBudgetExhausted,
            TransferBudgetExhausted = transferBudgetExhausted,
            SeedBudgetExhausted = seedBudgetExhausted
        };
    }

    private static void SeedActiveCells(
        World world,
        LiquidSimulationWorkspace workspace,
        int scanBudget,
        ref int scannedTiles,
        ref int seededCells)
    {
        while (scannedTiles < scanBudget && workspace.PendingSeedRegionCount > 0)
        {
            var cursor = workspace.DequeueSeedRegion();
            var complete = false;
            while (scannedTiles < scanBudget)
            {
                scannedTiles++;
                if (world.TryGetTile(cursor.X, cursor.Y, out var tile) && tile.HasLiquid)
                {
                    seededCells += workspace.Activate(new TilePos(cursor.X, cursor.Y)) ? 1 : 0;
                }

                if (!cursor.MoveNext())
                {
                    complete = true;
                    break;
                }
            }

            if (complete)
            {
                workspace.CompleteSeedRegion(cursor.Region);
            }
            else
            {
                workspace.RequeueSeedRegion(cursor);
            }
        }
    }

    private static void ProcessCell(
        World world,
        LiquidSimulationWorkspace workspace,
        TilePos position,
        LiquidSimulationOptions options,
        ref int transferOperations,
        ref int successfulTransfers,
        ref int movedLiquid)
    {
        if (!world.TryGetTile(position.X, position.Y, out var source) || !source.HasLiquid)
        {
            return;
        }

        if (!TryConsumeTransferBudget(options, ref transferOperations))
        {
            workspace.Activate(position);
            return;
        }

        var moved = TryTransfer(
            world,
            workspace,
            position,
            new TilePos(position.X, position.Y + 1),
            options.MaxLiquid,
            options);
        RecordTransfer(moved, ref successfulTransfers, ref movedLiquid);

        if (!world.TryGetTile(position.X, position.Y, out source) || !source.HasLiquid)
        {
            return;
        }

        var firstDirection = ((position.X ^ position.Y) & 1) == 0 ? -1 : 1;
        if (!TryBalanceSide(
                world,
                workspace,
                position,
                firstDirection,
                options,
                ref transferOperations,
                ref successfulTransfers,
                ref movedLiquid))
        {
            workspace.Activate(position);
            return;
        }

        if (!TryBalanceSide(
                world,
                workspace,
                position,
                -firstDirection,
                options,
                ref transferOperations,
                ref successfulTransfers,
                ref movedLiquid))
        {
            workspace.Activate(position);
        }
    }

    private static bool TryBalanceSide(
        World world,
        LiquidSimulationWorkspace workspace,
        TilePos sourcePosition,
        int direction,
        LiquidSimulationOptions options,
        ref int transferOperations,
        ref int successfulTransfers,
        ref int movedLiquid)
    {
        if (!TryConsumeTransferBudget(options, ref transferOperations))
        {
            return false;
        }

        if (!world.TryGetTile(sourcePosition.X, sourcePosition.Y, out var source))
        {
            return true;
        }

        if ((direction < 0 && sourcePosition.X == int.MinValue) ||
            (direction > 0 && sourcePosition.X == int.MaxValue))
        {
            return true;
        }

        var targetPosition = new TilePos(sourcePosition.X + direction, sourcePosition.Y);
        if (!world.TryGetTile(targetPosition.X, targetPosition.Y, out var target) ||
            !CanContainLiquid(target))
        {
            return true;
        }

        var difference = source.LiquidAmount - target.LiquidAmount;
        if (difference <= options.MinimumHorizontalDifference)
        {
            return true;
        }

        var requested = Math.Min(options.MaxHorizontalFlow, difference / 2);
        var moved = TryTransfer(
            world,
            workspace,
            sourcePosition,
            targetPosition,
            requested,
            options);
        RecordTransfer(moved, ref successfulTransfers, ref movedLiquid);
        return true;
    }

    private static int TryTransfer(
        World world,
        LiquidSimulationWorkspace workspace,
        TilePos sourcePosition,
        TilePos targetPosition,
        int requestedAmount,
        LiquidSimulationOptions options)
    {
        if (requestedAmount <= 0 ||
            !world.TryGetTile(sourcePosition.X, sourcePosition.Y, out var source) ||
            !world.TryGetTile(targetPosition.X, targetPosition.Y, out var target) ||
            !source.HasLiquid ||
            !CanContainLiquid(target))
        {
            return 0;
        }

        var capacity = options.MaxLiquid - target.LiquidAmount;
        var moved = Math.Min(Math.Min(source.LiquidAmount, requestedAmount), capacity);
        if (moved <= 0)
        {
            return 0;
        }

        WriteLoadedTile(world, sourcePosition, WithLiquid(source, source.LiquidAmount - moved));
        WriteLoadedTile(world, targetPosition, WithLiquid(target, target.LiquidAmount + moved));
        workspace.MarkChanged(sourcePosition);
        workspace.MarkChanged(targetPosition);
        ActivateAffected(workspace, sourcePosition, targetPosition);
        return moved;
    }

    private static void ActivateAffected(
        LiquidSimulationWorkspace workspace,
        TilePos source,
        TilePos target)
    {
        // Bottom-up activation prevents freshly moved liquid from cascading
        // through several vertical cells in one simulation step.
        ActivateVertical(workspace, target.X, target.Y, offset: 1);
        ActivateHorizontal(workspace, target.X, target.Y, direction: -1);
        ActivateHorizontal(workspace, target.X, target.Y, direction: 1);
        workspace.Activate(target);
        workspace.Activate(source);
        ActivateVertical(workspace, source.X, source.Y, offset: -1);
        ActivateHorizontal(workspace, source.X, source.Y, direction: -1);
        ActivateHorizontal(workspace, source.X, source.Y, direction: 1);
    }

    private static void ActivateHorizontal(
        LiquidSimulationWorkspace workspace,
        int x,
        int y,
        int direction)
    {
        if ((direction < 0 && x == int.MinValue) ||
            (direction > 0 && x == int.MaxValue))
        {
            return;
        }

        workspace.Activate(new TilePos(x + direction, y));
    }

    private static void ActivateVertical(
        LiquidSimulationWorkspace workspace,
        int x,
        int y,
        int offset)
    {
        if ((offset < 0 && y == int.MinValue) ||
            (offset > 0 && y == int.MaxValue))
        {
            return;
        }

        workspace.Activate(new TilePos(x, y + offset));
    }

    private static void WriteLoadedTile(World world, TilePos position, TileInstance tile)
    {
        var chunkPosition = CoordinateUtils.TileToChunk(position);
        if (!world.TryGetChunk(chunkPosition, out var chunk) || chunk is null)
        {
            return;
        }

        var local = CoordinateUtils.LocalTileInChunk(position);
        chunk.SetTile(local.X, local.Y, tile);
        MarkBoundaryNeighborsDirty(world, chunkPosition, local);
    }

    private static void MarkBoundaryNeighborsDirty(World world, ChunkPos chunkPosition, TilePos local)
    {
        if (local.X == 0)
        {
            MarkChunkDirty(world, new ChunkPos(chunkPosition.X - 1, chunkPosition.Y));
        }
        else if (local.X == GameConstants.ChunkSize - 1)
        {
            MarkChunkDirty(world, new ChunkPos(chunkPosition.X + 1, chunkPosition.Y));
        }

        if (local.Y == 0)
        {
            MarkChunkDirty(world, new ChunkPos(chunkPosition.X, chunkPosition.Y - 1));
        }
        else if (local.Y == GameConstants.ChunkSize - 1)
        {
            MarkChunkDirty(world, new ChunkPos(chunkPosition.X, chunkPosition.Y + 1));
        }
    }

    private static void MarkChunkDirty(World world, ChunkPos position)
    {
        if (world.TryGetChunk(position, out var chunk) && chunk is not null)
        {
            chunk.MarkDirty(needsMeshRebuild: true, needsLightUpdate: true);
        }
    }

    private static bool TryConsumeTransferBudget(
        LiquidSimulationOptions options,
        ref int transferOperations)
    {
        if (transferOperations >= options.MaxTransferOperationsPerStep)
        {
            return false;
        }

        transferOperations++;
        return true;
    }

    private static void RecordTransfer(
        int moved,
        ref int successfulTransfers,
        ref int movedLiquid)
    {
        if (moved <= 0)
        {
            return;
        }

        successfulTransfers++;
        movedLiquid += moved;
    }

    private static bool CanContainLiquid(TileInstance tile)
    {
        return !tile.IsSolid;
    }

    private static TileInstance WithLiquid(TileInstance tile, int amount)
    {
        var clamped = (byte)Math.Clamp(amount, 0, byte.MaxValue);
        tile.LiquidAmount = clamped;

        if (clamped == 0)
        {
            tile.Flags &= ~TileFlags.HasLiquid;
        }
        else
        {
            tile.Flags |= TileFlags.HasLiquid;
        }

        return tile;
    }

    private static void ValidateOptions(LiquidSimulationOptions options)
    {
        if (options.MaxCellsPerStep <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxCellsPerStep must be positive.");
        }

        if (options.MaxTransferOperationsPerStep <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxTransferOperationsPerStep must be positive.");
        }

        if (options.MaxSeedTileChecksPerStep <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxSeedTileChecksPerStep must be positive.");
        }
    }
}
