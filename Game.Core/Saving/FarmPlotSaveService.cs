using Game.Core.Farming;
using Game.Core.World;
using System.Text.Json;

namespace Game.Core.Saving;

public sealed class FarmPlotSaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public void Save(FarmPlotManager manager, string filePath)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = manager.Plots
            .OrderBy(plot => plot.Position.Y)
            .ThenBy(plot => plot.Position.X)
            .Select(ToSaveData)
            .ToArray();

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public FarmPlotManager Load(string filePath, CropRegistry crops)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(crops);

        var manager = new FarmPlotManager();
        if (!File.Exists(filePath))
        {
            return manager;
        }

        var data = JsonSerializer.Deserialize<FarmPlotSaveData[]>(File.ReadAllText(filePath), Options)
            ?? Array.Empty<FarmPlotSaveData>();

        foreach (var item in data)
        {
            var plot = manager.GetOrCreatePlot(new TilePos(item.TileX, item.TileY));
            plot.IsTilled = item.IsTilled;
            plot.IsWatered = item.IsWatered;

            if (item.Crop is null)
            {
                continue;
            }

            if (!crops.TryGetById(item.Crop.CropId, out _))
            {
                throw new InvalidDataException($"Unknown crop id '{item.Crop.CropId}' in farm plot save.");
            }

            plot.Crop = new CropInstance(
                item.Crop.CropId,
                item.Crop.PlantedDay,
                Math.Max(0, item.Crop.DaysUntilHarvest),
                Math.Max(0, item.Crop.HarvestCount));
        }

        manager.ClearEmptyUntilledPlots();
        return manager;
    }

    private static FarmPlotSaveData ToSaveData(FarmPlot plot)
    {
        return new FarmPlotSaveData
        {
            TileX = plot.Position.X,
            TileY = plot.Position.Y,
            IsTilled = plot.IsTilled,
            IsWatered = plot.IsWatered,
            Crop = plot.Crop is null
                ? null
                : new FarmCropSaveData
                {
                    CropId = plot.Crop.CropId,
                    PlantedDay = plot.Crop.PlantedDay,
                    DaysUntilHarvest = plot.Crop.DaysUntilHarvest,
                    HarvestCount = plot.Crop.HarvestCount
                }
        };
    }
}
