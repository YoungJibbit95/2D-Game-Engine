using Game.Core.Time;
using System.Text.Json;

namespace Game.Core.Saving;

public sealed class SimulationSaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public void Save(WorldTime worldTime, string filePath)
    {
        ArgumentNullException.ThrowIfNull(worldTime);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new SimulationSaveData
        {
            Day = worldTime.Day,
            TimeOfDaySeconds = worldTime.TimeOfDaySeconds,
            DayLengthSeconds = worldTime.DayLengthSeconds
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public WorldTime Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var data = JsonSerializer.Deserialize<SimulationSaveData>(File.ReadAllText(filePath), Options)
            ?? throw new InvalidDataException("Simulation save data is empty or invalid.");
        if (data.FormatVersion < 1 || data.FormatVersion > SimulationSaveData.CurrentFormatVersion)
        {
            throw new InvalidDataException($"Unsupported simulation save format version {data.FormatVersion}.");
        }

        if (!double.IsFinite(data.DayLengthSeconds) || data.DayLengthSeconds <= 0)
        {
            throw new InvalidDataException("Simulation day length must be finite and greater than zero.");
        }

        var worldTime = new WorldTime(data.DayLengthSeconds);
        try
        {
            worldTime.RestoreState(data.Day, data.TimeOfDaySeconds);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Simulation time state is outside the supported range.", exception);
        }

        return worldTime;
    }
}
