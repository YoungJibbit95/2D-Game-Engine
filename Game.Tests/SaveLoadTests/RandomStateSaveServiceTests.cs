using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Core.Randomness;
using Game.Core.Saving;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class RandomStateSaveServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yjse-random-state-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveMidTraceThenLoad_ResumesEveryNamedStreamExactly()
    {
        var service = new RandomStateSaveService();
        var path = Path.Combine(_root, RandomStateSaveService.DefaultFileName);
        var registry = new SessionRandomRegistry(77);
        var spawning = registry.GetStream("spawning");
        var loot = registry.GetStream("loot");
        _ = DrawTrace(spawning, 37);
        _ = DrawTrace(loot, 19);

        service.Save(registry, path);
        var expectedSpawning = DrawTrace(spawning, 80);
        var expectedLoot = DrawTrace(loot, 80);
        var loaded = service.LoadOrCreate(path, sessionSeed: 77);

        Assert.Equal(RandomStateLoadSource.Primary, loaded.Source);
        Assert.Null(loaded.Warning);
        Assert.Equal(expectedSpawning, DrawTrace(loaded.Registry.GetStream("spawning"), 80));
        Assert.Equal(expectedLoot, DrawTrace(loaded.Registry.GetStream("loot"), 80));
    }

    [Fact]
    public void Save_WritesVersionedAdditiveJsonAndIgnoresUnknownProperties()
    {
        var service = new RandomStateSaveService();
        var path = Path.Combine(_root, RandomStateSaveService.DefaultFileName);
        var registry = new SessionRandomRegistry(123);
        _ = registry.GetStream("weather").NextUInt64();
        service.Save(registry, path);

        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(RandomRegistrySnapshot.CurrentFormatVersion, document["FormatVersion"]!.GetValue<int>());
        document["FutureMetadata"] = new JsonObject
        {
            ["Source"] = "forward-compatible-test"
        };
        File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var loaded = service.LoadOrCreate(path, sessionSeed: 123);

        Assert.Equal(RandomStateLoadSource.Primary, loaded.Source);
        Assert.Equal(1UL, loaded.Registry.GetStream("weather").DrawCount);
    }

    [Fact]
    public void LoadOrCreate_UsesDeterministicLegacyFallbackWhenSidecarIsMissing()
    {
        var service = new RandomStateSaveService();
        var path = Path.Combine(_root, RandomStateSaveService.DefaultFileName);
        var reference = new SessionRandomRegistry(5_555);

        var loaded = service.LoadOrCreate(path, sessionSeed: 5_555);

        Assert.Equal(RandomStateLoadSource.LegacyFallback, loaded.Source);
        Assert.Equal(0, loaded.Registry.StreamCount);
        Assert.Equal(
            DrawTrace(reference.GetStream("spawning"), 40),
            DrawTrace(loaded.Registry.GetStream("spawning"), 40));
    }

    [Fact]
    public void CorruptPrimary_RecoversPreviousAtomicSaveFromBackup()
    {
        var service = new RandomStateSaveService();
        var path = Path.Combine(_root, RandomStateSaveService.DefaultFileName);
        var registry = new SessionRandomRegistry(8080);
        var stream = registry.GetStream("world-events");
        _ = DrawTrace(stream, 12);
        service.Save(registry, path);

        _ = DrawTrace(stream, 25);
        var previousSaveSnapshot = registry.ExportSnapshot();
        service.Save(registry, path);
        _ = DrawTrace(stream, 7);
        service.Save(registry, path);
        File.WriteAllText(path, "{ definitely-not-json");

        var expectedRegistry = SessionRandomRegistry.FromSnapshot(previousSaveSnapshot);
        var loaded = service.LoadOrCreate(path, sessionSeed: 8080);

        Assert.Equal(RandomStateLoadSource.BackupRecovery, loaded.Source);
        Assert.NotNull(loaded.Warning);
        Assert.True(File.Exists(RandomStateSaveService.GetBackupPath(path)));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
        Assert.Equal(
            DrawTrace(expectedRegistry.GetStream("world-events"), 64),
            DrawTrace(loaded.Registry.GetStream("world-events"), 64));
    }

    [Fact]
    public void CorruptOrFuturePrimaryWithoutBackup_IsRejected()
    {
        var service = new RandomStateSaveService();
        var path = Path.Combine(_root, RandomStateSaveService.DefaultFileName);
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, "{ broken");

        Assert.Throws<InvalidDataException>(() => service.LoadOrCreate(path, sessionSeed: 42));

        File.WriteAllText(path, """
            {
              "FormatVersion": 99,
              "SessionSeed": 42,
              "Streams": []
            }
            """);
        Assert.Throws<InvalidDataException>(() => service.LoadOrCreate(path, sessionSeed: 42));
    }

    [Fact]
    public void LoadOrCreate_RejectsStateCopiedFromAnotherSession()
    {
        var service = new RandomStateSaveService();
        var path = Path.Combine(_root, RandomStateSaveService.DefaultFileName);
        service.Save(new SessionRandomRegistry(100), path);

        Assert.Throws<InvalidDataException>(() => service.LoadOrCreate(path, sessionSeed: 101));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ulong[] DrawTrace(DeterministicRandomStream stream, int length)
    {
        var trace = new ulong[length];
        for (var index = 0; index < trace.Length; index++)
        {
            trace[index] = stream.NextUInt64();
        }

        return trace;
    }
}
