using System.Diagnostics;
using Game.Core.Diagnostics.Performance;
using Game.Core.World.Generation;

namespace Game.Core.World.Streaming.Diagnostics;

/// <summary>
/// Measures complete camera-window settlement from negative to positive X and back again.
/// The first pass is cold; callers can retain the route's chunks to make the reverse pass warm.
/// </summary>
public sealed class BidirectionalCameraTraversalHarness
{
    private readonly World _world;
    private readonly WorldGenerationProfile _profile;
    private readonly ChunkStreamingService _streaming;
    private readonly ChunkStreamingOptions _streamingOptions;
    private readonly BidirectionalCameraTraversalOptions _traversalOptions;
    private readonly string? _worldDirectory;
    private int _maxResidentChunks;

    public BidirectionalCameraTraversalHarness(
        World world,
        WorldGenerationProfile profile,
        ChunkStreamingService streaming,
        ChunkStreamingOptions streamingOptions,
        BidirectionalCameraTraversalOptions traversalOptions,
        string? worldDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(streaming);
        ArgumentNullException.ThrowIfNull(streamingOptions);
        ArgumentNullException.ThrowIfNull(traversalOptions);
        if (!world.IsHorizontallyInfinite)
        {
            throw new ArgumentException("Camera traversal distribution requires a horizontally infinite world.", nameof(world));
        }

        ChunkStreamingPlanner.ValidateOptions(streamingOptions);
        traversalOptions.Validate();
        _world = world;
        _profile = profile;
        _streaming = streaming;
        _streamingOptions = streamingOptions;
        _traversalOptions = traversalOptions;
        _worldDirectory = worldDirectory;
    }

    public BidirectionalCameraTraversalResult Run()
    {
        var sampleCount = _traversalOptions.CameraPositionCount;
        var cold = new LongSessionDistributionCollector(
            LongSessionDistributionLabels.StreamingColdBidirectionalSettleMilliseconds,
            sampleCount,
            _traversalOptions.ColdBudgetMilliseconds);
        var warm = new LongSessionDistributionCollector(
            LongSessionDistributionLabels.StreamingWarmBidirectionalSettleMilliseconds,
            sampleCount,
            _traversalOptions.WarmBudgetMilliseconds);

        var initialTelemetry = _streaming.Telemetry;
        var coldUpdateCount = RunPass(cold, reverse: false, out var afterColdTelemetry);
        var warmUpdateCount = RunPass(warm, reverse: true, out var afterWarmTelemetry);

        return new BidirectionalCameraTraversalResult(
            cold.Capture(),
            warm.Capture(),
            sampleCount,
            coldUpdateCount,
            warmUpdateCount,
            NonNegativeDelta(initialTelemetry.GenerateOperations, afterColdTelemetry.GenerateOperations),
            NonNegativeDelta(afterColdTelemetry.GenerateOperations, afterWarmTelemetry.GenerateOperations),
            NonNegativeDelta(initialTelemetry.ApplyOperations, afterColdTelemetry.ApplyOperations),
            NonNegativeDelta(afterColdTelemetry.ApplyOperations, afterWarmTelemetry.ApplyOperations),
            _maxResidentChunks,
            afterWarmTelemetry.FailedJobs,
            _traversalOptions.MinimumCenterTileX < 0,
            _traversalOptions.MaximumCenterTileX > 0);
    }

    private int RunPass(
        LongSessionDistributionCollector collector,
        bool reverse,
        out ChunkStreamingTelemetry finalTelemetry)
    {
        var updateCount = 0;
        finalTelemetry = _streaming.Telemetry;
        for (var index = 0; index < _traversalOptions.CameraPositionCount; index++)
        {
            var offset = (long)index * _traversalOptions.StepTiles;
            var centerTileX = checked((int)(reverse
                ? _traversalOptions.MaximumCenterTileX - offset
                : _traversalOptions.MinimumCenterTileX + offset));
            var elapsed = SettleWindow(centerTileX, out var windowUpdates, out finalTelemetry);
            collector.Add(elapsed);
            updateCount += windowUpdates;
        }

        return updateCount;
    }

    private double SettleWindow(
        int centerTileX,
        out int updateCount,
        out ChunkStreamingTelemetry finalTelemetry)
    {
        var visible = new RectI(
            centerTileX - (_traversalOptions.VisibleWidthTiles / 2),
            _profile.SurfaceBaseY - (_traversalOptions.VisibleHeightTiles / 2),
            _traversalOptions.VisibleWidthTiles,
            _traversalOptions.VisibleHeightTiles);
        var startedAt = Stopwatch.GetTimestamp();
        finalTelemetry = _streaming.Telemetry;

        for (updateCount = 1; updateCount <= _traversalOptions.MaxSettleUpdatesPerPosition; updateCount++)
        {
            var result = _streaming.Update(
                _world,
                _profile,
                visible,
                _worldDirectory,
                _streamingOptions);
            finalTelemetry = result.Telemetry;
            _maxResidentChunks = Math.Max(_maxResidentChunks, _world.Chunks.Count);

            if (IsSettled(result))
            {
                return Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            }

            // A tight diagnostic polling loop can consume its entire update budget
            // before Debug worker jobs receive a scheduler quantum. Periodically
            // surrender one millisecond so the measurement observes real background
            // settlement rather than main-thread polling speed.
            if ((updateCount & 63) == 0)
            {
                Thread.Sleep(1);
            }
            else
            {
                Thread.Yield();
            }
        }

        throw new TimeoutException(
            $"Streaming did not settle at camera tile X {centerTileX} after " +
            $"{_traversalOptions.MaxSettleUpdatesPerPosition} updates. " +
            $"pending={finalTelemetry.PendingLoadJobs}, apply={finalTelemetry.ApplyQueueLength}, " +
            $"deferred={finalTelemetry.DeferredLoadRequests}.");
    }

    private bool IsSettled(ChunkStreamingUpdateResult result)
    {
        foreach (var position in result.Plan.RequiredChunks)
        {
            if (!_world.TryGetChunk(position, out _))
            {
                return false;
            }
        }

        return result.Telemetry.PendingLoadJobs == 0 &&
               result.Telemetry.ApplyQueueLength == 0 &&
               result.Telemetry.DeferredLoadRequests == 0;
    }

    private static long NonNegativeDelta(long before, long after)
    {
        return Math.Max(0, after - before);
    }
}

public sealed record BidirectionalCameraTraversalOptions
{
    public int MinimumCenterTileX { get; init; } = -1024;

    public int MaximumCenterTileX { get; init; } = 1024;

    public int StepTiles { get; init; } = GameConstants.ChunkSize;

    public int VisibleWidthTiles { get; init; } = 96;

    public int VisibleHeightTiles { get; init; } = 48;

    public int MaxSettleUpdatesPerPosition { get; init; } = 4096;

    public double ColdBudgetMilliseconds { get; init; } = 250;

    public double WarmBudgetMilliseconds { get; init; } = 1000d / 60d;

    public int CameraPositionCount
    {
        get
        {
            var distance = (long)MaximumCenterTileX - MinimumCenterTileX;
            return checked((int)(distance / StepTiles) + 1);
        }
    }

    public void Validate()
    {
        if (MinimumCenterTileX >= 0 || MaximumCenterTileX <= 0 || MaximumCenterTileX <= MinimumCenterTileX)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumCenterTileX),
                "Traversal bounds must run from a negative X center to a positive X center.");
        }

        if (StepTiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(StepTiles));
        }

        var distance = (long)MaximumCenterTileX - MinimumCenterTileX;
        if (distance % StepTiles != 0)
        {
            throw new ArgumentException("Traversal distance must be exactly divisible by the tile step.");
        }

        if (distance / StepTiles >= int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(StepTiles), "Traversal contains too many camera positions.");
        }

        if (VisibleWidthTiles < 1 || VisibleHeightTiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(VisibleWidthTiles), "Visible dimensions must be positive.");
        }

        if (MaxSettleUpdatesPerPosition < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxSettleUpdatesPerPosition));
        }

        ValidateBudget(ColdBudgetMilliseconds, nameof(ColdBudgetMilliseconds));
        ValidateBudget(WarmBudgetMilliseconds, nameof(WarmBudgetMilliseconds));
    }

    private static void ValidateBudget(double budget, string parameterName)
    {
        if (!double.IsFinite(budget) || budget < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Traversal budget must be finite and non-negative.");
        }
    }
}

public readonly record struct BidirectionalCameraTraversalResult(
    LongSessionDistributionSnapshot Cold,
    LongSessionDistributionSnapshot Warm,
    int CameraPositionsPerPass,
    int ColdUpdateCount,
    int WarmUpdateCount,
    long ColdGenerateOperations,
    long WarmGenerateOperations,
    long ColdApplyOperations,
    long WarmApplyOperations,
    int MaxResidentChunks,
    long FailedJobs,
    bool TraversedNegativeX,
    bool TraversedPositiveX);
