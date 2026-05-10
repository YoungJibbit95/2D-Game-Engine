using Game.Core.World;

namespace Game.Core.Maps;

public sealed class TopDownMapInteractionService
{
    private readonly TopDownMapQueryService _queries;

    public TopDownMapInteractionService(TopDownMapQueryService? queries = null)
    {
        _queries = queries ?? new TopDownMapQueryService();
    }

    public TopDownMapInteractionResult FindInteraction(
        MapDefinition map,
        TopDownMapBody body,
        int reachTiles = 1,
        bool includeOverlap = true)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(body);

        if (reachTiles < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reachTiles), "Reach must not be negative.");
        }

        var actorTile = body.CenterTile(map.TileSize);
        var facing = body.Facing.ToVector();
        var targetTile = new TilePos(
            actorTile.X + (int)facing.X * Math.Max(1, reachTiles),
            actorTile.Y + (int)facing.Y * Math.Max(1, reachTiles));

        var region = BuildQueryRegion(actorTile, targetTile, reachTiles, includeOverlap);
        var candidates = _queries.QueryObjects(map, region, interactableOnly: true);
        if (candidates.Count == 0)
        {
            return TopDownMapInteractionResult.Miss(actorTile, targetTile, region, "no_interactable_in_reach");
        }

        var selected = candidates
            .OrderBy(item => FacingPriority(targetTile, item.Bounds))
            .ThenBy(item => DistanceSquaredTo(actorTile, item.Bounds))
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .First();

        return TopDownMapInteractionResult.Hit(selected, actorTile, targetTile, region);
    }

    private static RectI BuildQueryRegion(TilePos actorTile, TilePos targetTile, int reachTiles, bool includeOverlap)
    {
        var left = Math.Min(actorTile.X, targetTile.X);
        var right = Math.Max(actorTile.X, targetTile.X);
        var top = Math.Min(actorTile.Y, targetTile.Y);
        var bottom = Math.Max(actorTile.Y, targetTile.Y);

        if (includeOverlap)
        {
            left -= 1;
            top -= 1;
            right += 1;
            bottom += 1;
        }

        var padding = Math.Max(0, reachTiles - 1);
        return RectI.FromInclusiveTileBounds(left - padding, top - padding, right + padding, bottom + padding);
    }

    private static int DistanceSquaredTo(TilePos tile, RectI bounds)
    {
        var closestX = Math.Clamp(tile.X, bounds.Left, bounds.Right - 1);
        var closestY = Math.Clamp(tile.Y, bounds.Top, bounds.Bottom - 1);
        var dx = tile.X - closestX;
        var dy = tile.Y - closestY;
        return dx * dx + dy * dy;
    }

    private static int FacingPriority(TilePos targetTile, RectI bounds)
    {
        return bounds.Contains(targetTile) ? 0 : 1;
    }
}
