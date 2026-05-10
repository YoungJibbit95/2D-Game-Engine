using Game.Core.Events;

namespace Game.Core.Maps;

public sealed class TopDownMapObjectInteractionSystem
{
    private readonly TopDownMapInteractionService _interactions;
    private readonly TopDownMapTransitionSystem _transitions;

    public TopDownMapObjectInteractionSystem(
        TopDownMapInteractionService? interactions = null,
        TopDownMapTransitionSystem? transitions = null)
    {
        _interactions = interactions ?? new TopDownMapInteractionService();
        _transitions = transitions ?? new TopDownMapTransitionSystem();
    }

    public TopDownMapObjectActionResult Interact(
        MapRegistry maps,
        TopDownMapSession session,
        TopDownMapRuntimeStateStore runtimeStates,
        int reachTiles = 1,
        long tick = 0,
        GameEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(runtimeStates);

        if (!maps.TryGetById(session.CurrentMapId, out var map))
        {
            return TopDownMapObjectActionResult.Failed(session.CurrentMapId, null, "current_map_missing");
        }

        var mapState = runtimeStates.GetOrCreateMap(map.Id);
        var targeting = _interactions.FindInteraction(map, session.Body, reachTiles, runtimeState: mapState);
        if (!targeting.Success || targeting.Object is null)
        {
            return TopDownMapObjectActionResult.Failed(map.Id, targeting, targeting.FailureReason ?? "no_interactable");
        }

        var mapObject = targeting.Object;
        var objectState = mapState.GetOrCreateObject(mapObject.Id);
        objectState.InteractionCount++;
        objectState.LastInteractionTick = tick;

        var result = Execute(maps, session, map, mapObject, objectState, targeting);
        if (result.Success)
        {
            events?.Publish(new TopDownMapObjectInteractedEvent(
                map.Id,
                mapObject.Id,
                mapObject.Kind,
                result.ActionKind,
                result.InteractionId,
                result.PayloadId));

            if (result.Transition is { Success: true } transition)
            {
                events?.Publish(new TopDownMapTransitionedEvent(transition));
            }
        }

        return result;
    }

    private TopDownMapObjectActionResult Execute(
        MapRegistry maps,
        TopDownMapSession session,
        MapDefinition map,
        MapObjectDefinition mapObject,
        TopDownMapObjectRuntimeState objectState,
        TopDownMapInteractionResult targeting)
    {
        var action = ResolveActionKind(mapObject);
        return action switch
        {
            TopDownMapObjectActionKind.ShowMessage => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                action,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "messageId", "textKey", "messageKey"),
                text: ResolveText(mapObject)),

            TopDownMapObjectActionKind.OpenContainer => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                action,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "containerId", "inventoryId")),

            TopDownMapObjectActionKind.OpenShippingBin => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                action,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "shippingBinId", "containerId", "inventoryId")),

            TopDownMapObjectActionKind.OpenShop => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                action,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "shopId", "storeId")),

            TopDownMapObjectActionKind.ToggleDoor => ToggleDoor(map, mapObject, objectState, targeting),

            TopDownMapObjectActionKind.StartDialogue => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                action,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "dialogueId", "npcId")),

            TopDownMapObjectActionKind.Warp => Warp(maps, session, map, mapObject, targeting),

            TopDownMapObjectActionKind.UseFarmArea => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                action,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "farmAreaId", "areaId", "soil")),

            TopDownMapObjectActionKind.Trigger => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                action,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "triggerId", "scriptId", "eventId")),

            _ => TopDownMapObjectActionResult.Succeeded(
                map.Id,
                TopDownMapObjectActionKind.Inspect,
                mapObject,
                targeting,
                payloadId: ResolvePayloadId(mapObject, "inspectId"))
        };
    }

    private static TopDownMapObjectActionResult ToggleDoor(
        MapDefinition map,
        MapObjectDefinition mapObject,
        TopDownMapObjectRuntimeState objectState,
        TopDownMapInteractionResult targeting)
    {
        objectState.IsOpen = !objectState.IsOpen;
        return TopDownMapObjectActionResult.Succeeded(
            map.Id,
            TopDownMapObjectActionKind.ToggleDoor,
            mapObject,
            targeting,
            payloadId: ResolvePayloadId(mapObject, "doorId", "gateId"),
            isOpen: objectState.IsOpen);
    }

    private TopDownMapObjectActionResult Warp(
        MapRegistry maps,
        TopDownMapSession session,
        MapDefinition map,
        MapObjectDefinition mapObject,
        TopDownMapInteractionResult targeting)
    {
        var transition = _transitions.TryApplyWarpObject(maps, session, map, mapObject);
        return transition.Success
            ? TopDownMapObjectActionResult.Succeeded(
                map.Id,
                TopDownMapObjectActionKind.Warp,
                mapObject,
                targeting,
                payloadId: transition.TargetMapId,
                transition: transition)
            : TopDownMapObjectActionResult.Failed(map.Id, targeting, transition.FailureReason ?? "warp_failed");
    }

    private static TopDownMapObjectActionKind ResolveActionKind(MapObjectDefinition mapObject)
    {
        if (mapObject.Properties.TryGetValue("action", out var action) &&
            Enum.TryParse<TopDownMapObjectActionKind>(action, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return mapObject.Kind switch
        {
            MapObjectKind.Sign => TopDownMapObjectActionKind.ShowMessage,
            MapObjectKind.Container => TopDownMapObjectActionKind.OpenContainer,
            MapObjectKind.ShippingBin => TopDownMapObjectActionKind.OpenShippingBin,
            MapObjectKind.Shop => TopDownMapObjectActionKind.OpenShop,
            MapObjectKind.Door or MapObjectKind.Gate => TopDownMapObjectActionKind.ToggleDoor,
            MapObjectKind.NpcSpawn => TopDownMapObjectActionKind.StartDialogue,
            MapObjectKind.Warp => TopDownMapObjectActionKind.Warp,
            MapObjectKind.FarmArea => TopDownMapObjectActionKind.UseFarmArea,
            MapObjectKind.Trigger => TopDownMapObjectActionKind.Trigger,
            _ when !string.IsNullOrWhiteSpace(mapObject.InteractionId) => TopDownMapObjectActionKind.Trigger,
            _ => TopDownMapObjectActionKind.Inspect
        };
    }

    private static string ResolvePayloadId(MapObjectDefinition mapObject, params string[] propertyKeys)
    {
        foreach (var key in propertyKeys)
        {
            if (mapObject.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return !string.IsNullOrWhiteSpace(mapObject.InteractionId)
            ? mapObject.InteractionId!
            : mapObject.Id;
    }

    private static string? ResolveText(MapObjectDefinition mapObject)
    {
        return TryGetProperty(mapObject, "text", out var text) ||
               TryGetProperty(mapObject, "message", out text)
            ? text
            : null;
    }

    private static bool TryGetProperty(MapObjectDefinition mapObject, string key, out string value)
    {
        return mapObject.Properties.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
    }
}
