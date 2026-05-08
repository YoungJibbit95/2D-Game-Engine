using Game.Core.Inventory;
using Game.Core.Events;
using Game.Core.Tiles;
using Game.Core.World;
using System.Numerics;

namespace Game.Core.Interaction;

public sealed class MiningSystem
{
    private TilePos? _currentTarget;
    private float _progress;

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

        if (deltaSeconds <= 0 || !world.IsInBounds(target.X, target.Y))
        {
            Reset();
            return MiningResult.None;
        }

        var tile = world.GetTile(target.X, target.Y);
        if (tile.IsAir)
        {
            Reset();
            return MiningResult.None;
        }

        var definition = tiles.GetByNumericId(tile.TileId);
        if (!IsWithinReach(actorCenterWorld, target, reachPixels) || toolPower < definition.MiningPowerRequired)
        {
            Reset();
            return MiningResult.None;
        }

        if (_currentTarget != target)
        {
            _currentTarget = target;
            _progress = 0;
        }

        var hardness = Math.Max(0.05f, definition.Hardness);
        var speed = 1f + Math.Max(0, toolPower - definition.MiningPowerRequired) / 100f;
        _progress += deltaSeconds * speed / hardness;

        if (_progress < 1f)
        {
            return MiningResult.None;
        }

        var minedTileId = tile.TileId;
        world.RemoveTile(target.X, target.Y);
        var drop = string.IsNullOrWhiteSpace(definition.DropItemId)
            ? ItemStack.Empty
            : new ItemStack(definition.DropItemId, 1);
        var result = new MiningResult(true, target, drop);
        events?.Publish(new TileMinedEvent(target, minedTileId, drop));
        Reset();
        return result;
    }

    public void Reset()
    {
        _currentTarget = null;
        _progress = 0;
    }

    private static bool IsWithinReach(Vector2 actorCenterWorld, TilePos target, float reachPixels)
    {
        var tileCenter = CoordinateUtils.TileToWorld(target) + new Vector2(GameConstants.TileSize * 0.5f);
        return Vector2.Distance(actorCenterWorld, tileCenter) <= reachPixels;
    }
}
