using Game.Core.Inventory;
using Game.Core.Events;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Interaction;

public sealed class MiningSystem
{
    private const float ProgressEventStep = 0.05f;
    /// <summary>
    /// Global duration factor applied after tool and equipment modifiers. A value
    /// of 0.25 makes every valid mining action complete four times faster.
    /// </summary>
    public const float GlobalDurationFactor = 0.25f;

    private const float MaximumAccumulatedDeltaSeconds = 0.25f;


    private TilePos? _currentTarget;
    private float _progress;
    private float _lastPublishedProgress;
    private TilePos? _lastBlockedTarget;
    private GameplayActionFailureReason _lastBlockedReason;
    private GameEventBus? _lastBlockedEventBus;
    private readonly MiningTuning _tuning;

    public MiningSystem(MiningTuning? tuning = null)
    {
        _tuning = tuning ?? MiningTuning.Default;
        _tuning.Validate();
    }

    public float Progress => _progress;

    public TilePos? CurrentTarget => _currentTarget;

    public MiningResult Update(
        World.World world,
        TileRegistry tiles,
        TilePos target,
        Vector2 actorCenterWorld,
        float reachPixels,
        int toolPower,
        float deltaSeconds,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);

        if (!world.IsInBounds(target.X, target.Y))
        {
            return Block(target, 0, GameplayActionFailureReason.InvalidTarget, events);
        }

        var tile = world.GetTile(target.X, target.Y);
        if (tile.IsAir && tile.WallId == 0)
        {
            return Block(target, 0, GameplayActionFailureReason.InvalidTarget, events);
        }

        var minedTileId = tile.IsAir ? tile.WallId : tile.TileId;
        var definition = tiles.GetByNumericId(minedTileId);
        if (!IsWithinReach(actorCenterWorld, target, reachPixels))
        {
            return Block(target, minedTileId, GameplayActionFailureReason.OutOfReach, events);
        }

        if (toolPower < definition.MiningPowerRequired)
        {
            return Block(target, minedTileId, GameplayActionFailureReason.InsufficientToolPower, events);
        }

        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
        {
            return Block(target, minedTileId, GameplayActionFailureReason.InvalidTarget, events);
        }

        ClearBlockedFeedback();
        var started = _currentTarget != target;
        if (_currentTarget != target)
        {
            _currentTarget = target;
            _progress = 0;
            _lastPublishedProgress = 0;
            events?.Publish(new MiningStartedEvent(target, minedTileId));
        }

        var speed = MiningProgressCalculator.GetProgressPerSecond(definition, toolPower, _tuning);
        var previousProgress = Math.Clamp(_progress, 0f, 1f);
        var accumulatedDeltaSeconds = Math.Min(deltaSeconds, MaximumAccumulatedDeltaSeconds);
        var durationScaledDeltaSeconds = accumulatedDeltaSeconds / GlobalDurationFactor;
        _progress += durationScaledDeltaSeconds * speed;
        var currentProgress = Math.Clamp(_progress, 0f, 1f);

        if (_progress < 1f)
        {
            if (currentProgress - _lastPublishedProgress >= ProgressEventStep)
            {
                events?.Publish(new MiningProgressEvent(target, minedTileId, _lastPublishedProgress, currentProgress));
                _lastPublishedProgress = currentProgress;
            }

            return MiningResult.Progressed(target, minedTileId, previousProgress, currentProgress, started);
        }

        if (tile.IsAir)
        {
            world.SetWall(target.X, target.Y, 0);
        }
        else
        {
            world.RemoveTile(target.X, target.Y);
        }

        var drop = string.IsNullOrWhiteSpace(definition.DropItemId)
            ? ItemStack.Empty
            : new ItemStack(definition.DropItemId, 1);
        var result = MiningResult.CompletedResult(target, minedTileId, drop, previousProgress);
        events?.Publish(new MiningCompletedEvent(target, minedTileId, drop));
        events?.Publish(new TileMinedEvent(target, minedTileId, drop));
        ResetProgress();
        return result;
    }

    public void Reset()
    {
        ResetProgress();
        ClearBlockedFeedback();
    }

    private MiningResult Block(
        TilePos target,
        ushort tileId,
        GameplayActionFailureReason reason,
        GameEventBus? events)
    {
        ResetProgress();
        if (_lastBlockedTarget != target ||
            _lastBlockedReason != reason ||
            !ReferenceEquals(_lastBlockedEventBus, events))
        {
            events?.Publish(new MiningBlockedEvent(target, tileId, reason));
            _lastBlockedTarget = target;
            _lastBlockedReason = reason;
            _lastBlockedEventBus = events;
        }

        return MiningResult.BlockedResult(target, tileId, reason);
    }

    private void ResetProgress()
    {
        _currentTarget = null;
        _progress = 0;
        _lastPublishedProgress = 0;
    }

    private void ClearBlockedFeedback()
    {
        _lastBlockedTarget = null;
        _lastBlockedReason = GameplayActionFailureReason.None;
        _lastBlockedEventBus = null;
    }

    private static bool IsWithinReach(Vector2 actorCenterWorld, TilePos target, float reachPixels)
    {
        var tileCenter = CoordinateUtils.TileToWorld(target) + new Vector2(GameConstants.TileSize * 0.5f);
        return Vector2.Distance(actorCenterWorld, tileCenter) <= reachPixels;
    }
}
