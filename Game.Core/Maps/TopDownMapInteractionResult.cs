using Game.Core.World;

namespace Game.Core.Maps;

public sealed record TopDownMapInteractionResult(
    bool Success,
    MapObjectDefinition? Object,
    TilePos ActorTile,
    TilePos TargetTile,
    RectI QueryRegion,
    string? FailureReason)
{
    public static TopDownMapInteractionResult Hit(MapObjectDefinition mapObject, TilePos actorTile, TilePos targetTile, RectI queryRegion)
    {
        return new TopDownMapInteractionResult(true, mapObject, actorTile, targetTile, queryRegion, null);
    }

    public static TopDownMapInteractionResult Miss(TilePos actorTile, TilePos targetTile, RectI queryRegion, string reason)
    {
        return new TopDownMapInteractionResult(false, null, actorTile, targetTile, queryRegion, reason);
    }
}
