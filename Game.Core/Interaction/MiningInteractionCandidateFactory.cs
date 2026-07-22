using Game.Core.Events;
using Game.Core.Tiles;
using Game.Core.World;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Interaction;

public readonly record struct MiningInteractionCandidateResult(
    bool Success,
    InteractionCandidate Candidate,
    GameplayActionFailureReason Failure);

public static class MiningInteractionCandidateFactory
{
    public static MiningInteractionCandidateResult Create(
        GameWorld world,
        TileRegistry tiles,
        TilePos target,
        int toolPower,
        MiningTuning tuning,
        int fixedTicksPerSecond,
        float miningSpeedMultiplier = 1f)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);
        if (!world.IsInBounds(target.X, target.Y) ||
            !world.TryGetTile(target.X, target.Y, out var tile) || (tile.IsAir && tile.WallId == 0))
        {
            return new MiningInteractionCandidateResult(
                false,
                default,
                GameplayActionFailureReason.InvalidTarget);
        }

        var minedTileId = tile.IsAir ? tile.WallId : tile.TileId;
        var definition = tiles.GetByNumericId(minedTileId);
        if (toolPower < definition.MiningPowerRequired)
        {
            return new MiningInteractionCandidateResult(
                false,
                default,
                GameplayActionFailureReason.InsufficientToolPower);
        }

        var requiredTicks = MiningProgressCalculator.GetRequiredFixedTicks(
            definition,
            toolPower,
            tuning,
            fixedTicksPerSecond,
            miningSpeedMultiplier);
        return new MiningInteractionCandidateResult(
            true,
            InteractionCandidate.AtTile(
                InteractionTargetKind.Tile,
                target,
                definition.Id,
                requiredHoldTicks: requiredTicks),
            GameplayActionFailureReason.None);
    }
}
