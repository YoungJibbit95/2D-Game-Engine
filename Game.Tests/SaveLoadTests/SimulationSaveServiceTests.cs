using Game.Core.Saving;
using Game.Core.Time;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class SimulationSaveServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yjse-simulation-save-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveThenLoad_RoundTripsVersionedWorldTimeExactly()
    {
        var service = new SimulationSaveService();
        var path = Path.Combine(_root, "simulation.json");
        var time = new WorldTime(dayLengthSeconds: 90.5);
        time.Update(276.25);

        service.Save(time, path);
        var loaded = service.Load(path);

        Assert.Equal(SimulationSaveData.CurrentFormatVersion, 1);
        Assert.Equal(time.Day, loaded.Day);
        Assert.Equal(time.TimeOfDaySeconds, loaded.TimeOfDaySeconds);
        Assert.Equal(time.DayLengthSeconds, loaded.DayLengthSeconds);
    }

    [Fact]
    public void Load_RejectsFutureFormatAndInvalidClockState()
    {
        var service = new SimulationSaveService();
        Directory.CreateDirectory(_root);
        var futurePath = Path.Combine(_root, "future.json");
        File.WriteAllText(futurePath, """{"FormatVersion":99,"Day":1,"TimeOfDaySeconds":0,"DayLengthSeconds":10}""");
        var invalidPath = Path.Combine(_root, "invalid.json");
        File.WriteAllText(invalidPath, """{"FormatVersion":1,"Day":0,"TimeOfDaySeconds":12,"DayLengthSeconds":10}""");

        Assert.Throws<InvalidDataException>(() => service.Load(futurePath));
        Assert.Throws<InvalidDataException>(() => service.Load(invalidPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
