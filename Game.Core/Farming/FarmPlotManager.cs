using Game.Core.World;

namespace Game.Core.Farming;

public sealed class FarmPlotManager
{
    private readonly Dictionary<TilePos, FarmPlot> _plots = new();

    public IReadOnlyCollection<FarmPlot> Plots => _plots.Values;

    public bool TryGetPlot(TilePos position, out FarmPlot plot)
    {
        return _plots.TryGetValue(position, out plot!);
    }

    public FarmPlot GetOrCreatePlot(TilePos position)
    {
        if (_plots.TryGetValue(position, out var plot))
        {
            return plot;
        }

        plot = new FarmPlot(position);
        _plots.Add(position, plot);
        return plot;
    }

    public bool RemovePlot(TilePos position)
    {
        return _plots.Remove(position);
    }

    public void ClearEmptyUntilledPlots()
    {
        foreach (var position in _plots
                     .Where(pair => !pair.Value.IsTilled && !pair.Value.IsWatered && !pair.Value.HasCrop)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _plots.Remove(position);
        }
    }
}
