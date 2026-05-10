namespace Game.Core.Maps;

public sealed record TopDownMapObjectActionResult(
    bool Success,
    TopDownMapObjectActionKind ActionKind,
    string MapId,
    MapObjectDefinition? Object,
    TopDownMapInteractionResult? Targeting,
    TopDownMapTransitionResult? Transition,
    string? InteractionId,
    string? PayloadId,
    string? Text,
    bool? IsOpen,
    string? FailureReason)
{
    public static TopDownMapObjectActionResult Failed(string mapId, TopDownMapInteractionResult? targeting, string reason)
    {
        return new TopDownMapObjectActionResult(
            false,
            TopDownMapObjectActionKind.None,
            mapId,
            targeting?.Object,
            targeting,
            null,
            targeting?.Object?.InteractionId,
            null,
            null,
            null,
            reason);
    }

    public static TopDownMapObjectActionResult Succeeded(
        string mapId,
        TopDownMapObjectActionKind actionKind,
        MapObjectDefinition mapObject,
        TopDownMapInteractionResult targeting,
        string? payloadId = null,
        string? text = null,
        bool? isOpen = null,
        TopDownMapTransitionResult? transition = null)
    {
        return new TopDownMapObjectActionResult(
            true,
            actionKind,
            mapId,
            mapObject,
            targeting,
            transition,
            mapObject.InteractionId,
            payloadId,
            text,
            isOpen,
            null);
    }
}
