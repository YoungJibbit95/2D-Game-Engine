namespace Game.Core.Maps;

public sealed class TopDownMapRuntimeStateStore
{
    private readonly Dictionary<string, TopDownMapRuntimeState> _maps =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<TopDownMapRuntimeState> Maps => _maps.Values;

    public TopDownMapRuntimeState GetOrCreateMap(string mapId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);

        if (!_maps.TryGetValue(mapId, out var state))
        {
            state = new TopDownMapRuntimeState(mapId);
            _maps.Add(mapId, state);
        }

        return state;
    }

    public bool TryGetMap(string mapId, out TopDownMapRuntimeState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        return _maps.TryGetValue(mapId, out state!);
    }
}
