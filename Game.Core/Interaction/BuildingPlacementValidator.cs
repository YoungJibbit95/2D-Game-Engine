using Game.Core.Items;
using Game.Core.Tiles;
using Game.Core.World;
using Game.Core.World.Queries;
using System.Numerics;
using GameWorld = Game.Core.World.World;

namespace Game.Core.Interaction;

public sealed class BuildingPlacementValidator
{
    private readonly WorldQueryService _worldQueries;

    public BuildingPlacementValidator(WorldQueryService? worldQueries = null)
    {
        _worldQueries = worldQueries ?? new WorldQueryService();
    }

    public BuildingPlacementValidationResult Validate(
        GameWorld world,
        ItemRegistry items,
        TileRegistry tiles,
        in BuildingPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(tiles);
        var options = request.Options ?? BuildingPlacementOptions.Strict;
        if (request.Stack.IsEmpty || !float.IsFinite(request.ReachPixels) || request.ReachPixels <= 0f ||
            !float.IsFinite(request.ActorCenterWorld.X) || !float.IsFinite(request.ActorCenterWorld.Y) ||
            request.ActorBoundsWorld.IsEmpty)
        {
            return BuildingPlacementValidationResult.Rejected(request, BuildingPlacementFailure.InvalidRequest);
        }

        if (!world.IsInBounds(request.Target.X, request.Target.Y))
        {
            return BuildingPlacementValidationResult.Rejected(request, BuildingPlacementFailure.OutOfBounds);
        }

        var chunk = CoordinateUtils.TileToChunk(request.Target);
        if (options.RequireLoadedChunk && !world.TryGetChunk(chunk, out _))
        {
            return BuildingPlacementValidationResult.Rejected(request, BuildingPlacementFailure.ChunkNotLoaded);
        }

        var hasTile = world.TryGetTile(request.Target.X, request.Target.Y, out var existing);
        if (!hasTile && options.RequireLoadedChunk)
        {
            return BuildingPlacementValidationResult.Rejected(request, BuildingPlacementFailure.ChunkNotLoaded);
        }

        if (!existing.IsAir)
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.Occupied,
                existing);
        }

        if (existing.HasLiquid && !options.AllowReplaceLiquid)
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.LiquidOccupied,
                existing);
        }

        var targetBounds = CreateTargetBounds(request.Target);
        if (targetBounds.Intersects(request.ActorBoundsWorld))
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.ActorCollision,
                existing);
        }

        var targetCenter = new Vector2(
            targetBounds.X + targetBounds.Width * 0.5f,
            targetBounds.Y + targetBounds.Height * 0.5f);
        if (Vector2.Distance(request.ActorCenterWorld, targetCenter) > request.ReachPixels)
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.OutOfReach,
                existing);
        }

        if (options.RequireLineOfSight)
        {
            var obstruction = _worldQueries.RaycastTiles(
                world,
                request.ActorCenterWorld,
                targetCenter,
                static tile => tile.IsSolid);
            if (obstruction.Hit && obstruction.TilePosition != request.Target)
            {
                return BuildingPlacementValidationResult.Rejected(
                    request,
                    BuildingPlacementFailure.Obstructed,
                    existing);
            }
        }

        if (!items.TryGetById(request.Stack.ItemId, out var item))
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.UnknownItem,
                existing);
        }

        if (item.Type != ItemType.PlaceableTile || string.IsNullOrWhiteSpace(item.PlacesTileId))
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.ItemNotPlaceable,
                existing);
        }

        if (!tiles.TryGetById(item.PlacesTileId, out var tile) || tile.NumericId == KnownTileIds.Air)
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.UnknownTile,
                existing);
        }

        if (!SatisfiesSupport(world, request.Target, item.PlacementSupport, options.RequireLoadedChunk))
        {
            return BuildingPlacementValidationResult.Rejected(
                request,
                BuildingPlacementFailure.Unsupported,
                existing);
        }

        return new BuildingPlacementValidationResult(
            true,
            BuildingPlacementFailure.None,
            request.Target,
            item.Id,
            tile.NumericId,
            existing);
    }

    private static bool SatisfiesSupport(
        GameWorld world,
        TilePos target,
        PlacementSupportRule rule,
        bool requireLoaded)
    {
        return rule switch
        {
            PlacementSupportRule.None => true,
            PlacementSupportRule.AdjacentSolid => HasAdjacent(world, target, requireLoaded, static tile => tile.IsSolid),
            PlacementSupportRule.AdjacentSolidOrWall => HasAdjacent(
                world,
                target,
                requireLoaded,
                static tile => tile.IsSolid || tile.WallId != 0),
            PlacementSupportRule.OnSolidGround => TryGetSupport(
                world,
                new TilePos(target.X, target.Y + 1),
                requireLoaded,
                out var below) && below.IsSolid,
            _ => false
        };
    }

    private static bool HasAdjacent(
        GameWorld world,
        TilePos target,
        bool requireLoaded,
        Func<TileInstance, bool> predicate)
    {
        ReadOnlySpan<TilePos> offsets =
        [
            new(-1, 0),
            new(1, 0),
            new(0, -1),
            new(0, 1)
        ];
        for (var index = 0; index < offsets.Length; index++)
        {
            var neighbor = new TilePos(target.X + offsets[index].X, target.Y + offsets[index].Y);
            if (TryGetSupport(world, neighbor, requireLoaded, out var tile) && predicate(tile))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSupport(
        GameWorld world,
        TilePos position,
        bool requireLoaded,
        out TileInstance tile)
    {
        if (!world.IsInBounds(position.X, position.Y))
        {
            tile = TileInstance.Air;
            return false;
        }

        if (world.TryGetTile(position.X, position.Y, out tile))
        {
            return true;
        }

        tile = TileInstance.Air;
        return !requireLoaded;
    }

    private static RectI CreateTargetBounds(TilePos target)
    {
        var world = CoordinateUtils.TileToWorld(target);
        return new RectI(
            (int)Math.Clamp(world.X, int.MinValue, int.MaxValue),
            (int)Math.Clamp(world.Y, int.MinValue, int.MaxValue),
            GameConstants.TileSize,
            GameConstants.TileSize);
    }
}
