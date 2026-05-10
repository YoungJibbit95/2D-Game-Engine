namespace Game.Core.Maps;

public sealed class TopDownMapRuntimeState
{
    private readonly Dictionary<string, TopDownMapObjectRuntimeState> _objects =
        new(StringComparer.OrdinalIgnoreCase);

    public TopDownMapRuntimeState(string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        MapId = mapId;
    }

    public string MapId { get; }

    public IReadOnlyCollection<TopDownMapObjectRuntimeState> Objects => _objects.Values;

    public TopDownMapObjectRuntimeState GetOrCreateObject(string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        if (!_objects.TryGetValue(objectId, out var state))
        {
            state = new TopDownMapObjectRuntimeState(objectId);
            _objects.Add(objectId, state);
        }

        return state;
    }

    public bool TryGetObject(string objectId, out TopDownMapObjectRuntimeState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        return _objects.TryGetValue(objectId, out state!);
    }

    public bool IsObjectEnabled(MapObjectDefinition mapObject)
    {
        ArgumentNullException.ThrowIfNull(mapObject);
        return !TryGetObject(mapObject.Id, out var state) || state.IsEnabled;
    }

    public bool IsObjectOpen(MapObjectDefinition mapObject)
    {
        ArgumentNullException.ThrowIfNull(mapObject);
        return TryGetObject(mapObject.Id, out var state) && state.IsOpen;
    }

    public bool IsObjectBlocking(MapObjectDefinition mapObject)
    {
        ArgumentNullException.ThrowIfNull(mapObject);

        if (!mapObject.BlocksMovement || !IsObjectEnabled(mapObject))
        {
            return false;
        }

        return !IsOpenPassage(mapObject);
    }

    private bool IsOpenPassage(MapObjectDefinition mapObject)
    {
        return IsObjectOpen(mapObject) &&
               (mapObject.Kind == MapObjectKind.Door || mapObject.Kind == MapObjectKind.Gate);
    }
}
