namespace Game.Core.UI;

public sealed class UiLayerStack
{
    private readonly List<UiLayer> _layers = new();
    private readonly UiHitTestService _hitTests = new();

    public IReadOnlyList<UiLayer> Layers => _layers;

    public void Add(UiLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (_layers.Any(existing => string.Equals(existing.Id, layer.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"UI layer '{layer.Id}' already exists.");
        }

        _layers.Add(layer);
    }

    public bool Remove(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _layers.RemoveAll(layer => string.Equals(layer.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    public UiHitTestResult HitTest(UiPoint point)
    {
        foreach (var layer in _layers
                     .Where(layer => layer.IsVisible)
                     .OrderByDescending(layer => layer.ZIndex))
        {
            var hit = _hitTests.HitTest(layer.Root, point);
            if (hit is not null)
            {
                return new UiHitTestResult(hit, layer, BlocksLowerLayers: layer.IsModal);
            }

            if (layer.IsModal)
            {
                return new UiHitTestResult(null, layer, BlocksLowerLayers: true);
            }
        }

        return new UiHitTestResult(null, null, BlocksLowerLayers: false);
    }
}
