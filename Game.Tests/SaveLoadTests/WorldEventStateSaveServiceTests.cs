using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Core.Saving;
using Game.Core.WorldEvents;
using Game.Core.World;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class WorldEventStateSaveServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yjse-world-event-state-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveThenLoad_RoundTripsRuntimeCooldownsAndBoundedJournal()
    {
        var service = new WorldEventStateSaveService();
        var path = Path.Combine(_root, WorldEventStateSaveService.DefaultFileName);
        var expected = CreateState(lastAdvancedTick: 480, nextSequence: 11);

        service.Save(expected, path);
        var loaded = service.LoadOrDefault(path);

        Assert.Equal(WorldEventStateLoadSource.Primary, loaded.Source);
        Assert.Null(loaded.Warning);
        Assert.NotNull(loaded.State);
        AssertRuntimeStateEqual(expected, loaded.State!);
    }

    [Fact]
    public void Save_WritesVersionedAdditiveJsonAndIgnoresUnknownProperties()
    {
        var service = new WorldEventStateSaveService();
        var path = Path.Combine(_root, WorldEventStateSaveService.DefaultFileName);
        service.Save(CreateState(600, 4), path);

        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(
            WorldEventRuntimeStateSnapshot.CurrentFormatVersion,
            document["FormatVersion"]!.GetValue<int>());
        document["FutureMetadata"] = new JsonObject { ["Revision"] = 2 };
        File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var loaded = service.LoadOrDefault(path);

        Assert.Equal(WorldEventStateLoadSource.Primary, loaded.Source);
        Assert.Equal(600, loaded.State!.Runtime.LastAdvancedTick);
    }

    [Fact]
    public void LoadOrDefault_MissingLegacySidecarReturnsEmptyFallback()
    {
        var loaded = new WorldEventStateSaveService().LoadOrDefault(
            Path.Combine(_root, WorldEventStateSaveService.DefaultFileName));

        Assert.Equal(WorldEventStateLoadSource.LegacyFallback, loaded.Source);
        Assert.Null(loaded.State);
        Assert.Null(loaded.Warning);
    }

    [Fact]
    public void CorruptPrimary_RecoversPreviousAtomicSaveFromBackup()
    {
        var service = new WorldEventStateSaveService();
        var path = Path.Combine(_root, WorldEventStateSaveService.DefaultFileName);
        var first = CreateState(120, 2);
        var expectedBackup = CreateState(240, 5);
        service.Save(first, path);
        service.Save(expectedBackup, path);
        service.Save(CreateState(360, 8), path);
        File.WriteAllText(path, "{ not-valid-json");

        var loaded = service.LoadOrDefault(path);

        Assert.Equal(WorldEventStateLoadSource.BackupRecovery, loaded.Source);
        Assert.NotNull(loaded.Warning);
        AssertRuntimeStateEqual(expectedBackup, loaded.State!);
        Assert.True(File.Exists(WorldEventStateSaveService.GetBackupPath(path)));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
    }

    [Fact]
    public void CorruptOrFuturePrimaryWithoutBackup_IsRejected()
    {
        var service = new WorldEventStateSaveService();
        var path = Path.Combine(_root, WorldEventStateSaveService.DefaultFileName);
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, "{ broken");
        Assert.Throws<InvalidDataException>(() => service.LoadOrDefault(path));

        File.WriteAllText(path, """
            {
              "FormatVersion": 99,
              "Runtime": {},
              "Journal": {}
            }
            """);
        Assert.Throws<InvalidDataException>(() => service.LoadOrDefault(path));
    }

    [Fact]
    public void Load_RejectsJournalNewerThanRuntimeSnapshot()
    {
        var service = new WorldEventStateSaveService();
        var path = Path.Combine(_root, WorldEventStateSaveService.DefaultFileName);
        Directory.CreateDirectory(_root);
        var invalid = CreateState(100, 2) with
        {
            Journal = new WorldEventJournalSnapshot(
                WorldEventJournalSnapshot.CurrentFormatVersion,
                2,
                [CreateEntry(1, 101)])
        };
        File.WriteAllText(path, JsonSerializer.Serialize(invalid));

        Assert.Throws<InvalidDataException>(() => service.LoadOrDefault(path));
    }

    [Fact]
    public void SaveLoadThenAdvance_MatchesUninterruptedEventReplay()
    {
        var service = new WorldEventStateSaveService();
        var path = Path.Combine(_root, WorldEventStateSaveService.DefaultFileName);
        var checkpoint = CreateState(480, 11);
        service.Save(checkpoint, path);
        var loaded = service.LoadOrDefault(path).State!;
        var definition = new WorldEventDefinition
        {
            Id = "firefly_bloom",
            ChancePerWindow = 0f,
            MinDurationTicks = 720,
            MaxDurationTicks = 720,
            Intensity = 0.75f,
            Modifiers = WorldEventModifierSet.Identity with
            {
                RareLootChanceMultiplier = 1.25f
            },
            Phases =
            [
                new WorldEventPhaseDefinition
                {
                    Id = "bloom",
                    StartProgress = 0f,
                    EndProgress = 1f
                }
            ]
        };
        var executor = new DeterministicWorldEventExecutor(
            77,
            WorldEventDefinitionRegistry.Create([definition]));
        var context = new WorldEventExecutionContext(
            540,
            -4,
            "forest",
            null,
            "clear",
            0f,
            true,
            false,
            0.1f,
            new TilePos(-120, 40));

        var expected = executor.Advance(checkpoint.Runtime, context);
        var actual = executor.Advance(loaded.Runtime, context);

        Assert.Equal(expected.Snapshot.LastAdvancedTick, actual.Snapshot.LastAdvancedTick);
        Assert.Equal(expected.Snapshot.Status, actual.Snapshot.Status);
        Assert.Equal(expected.Snapshot.Progress, actual.Snapshot.Progress);
        Assert.Equal(expected.Snapshot.PhaseId, actual.Snapshot.PhaseId);
        Assert.Equal(expected.Snapshot.EffectiveModifiers, actual.Snapshot.EffectiveModifiers);
        Assert.Equal(expected.Events.ToArray(), actual.Events.ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldEventRuntimeStateSnapshot CreateState(long lastAdvancedTick, long nextSequence)
    {
        var firstSequence = Math.Max(0, nextSequence - 2);
        var entries = nextSequence == 0
            ? Array.Empty<WorldEventDomainEvent>()
            : Enumerable.Range(0, (int)Math.Min(2, nextSequence))
                .Select(index => CreateEntry(firstSequence + index, Math.Max(0, lastAdvancedTick - 1 + index)))
                .ToArray();
        return new WorldEventRuntimeStateSnapshot
        {
            Runtime = new WorldEventRuntimeSnapshot
            {
                LastAdvancedTick = lastAdvancedTick,
                RegionIndex = -4,
                BiomeId = "forest",
                Status = WorldEventRuntimeStatus.Active,
                ActiveEventId = "firefly_bloom",
                LastEventId = "firefly_bloom",
                StartTick = Math.Max(0, lastAdvancedTick - 120),
                EndTickExclusive = lastAdvancedTick + 600,
                PhaseId = "bloom",
                PhaseIndex = 1,
                Progress = 0.4f,
                PhaseProgress = 0.25f,
                Intensity = 0.75f,
                EffectiveModifiers = WorldEventModifierSet.Identity with
                {
                    RareLootChanceMultiplier = 1.25f
                },
                Cooldowns = [new WorldEventCooldownState("wildlife_migration", lastAdvancedTick + 900)]
            },
            Journal = new WorldEventJournalSnapshot(
                WorldEventJournalSnapshot.CurrentFormatVersion,
                nextSequence,
                entries)
        };
    }

    private static WorldEventDomainEvent CreateEntry(long sequence, long tick)
    {
        return new WorldEventDomainEvent(
            sequence,
            tick,
            -4,
            "firefly_bloom",
            WorldEventDomainEventKind.Progressed,
            "bloom",
            0.4f,
            0);
    }

    private static void AssertRuntimeStateEqual(
        WorldEventRuntimeStateSnapshot expected,
        WorldEventRuntimeStateSnapshot actual)
    {
        Assert.Equal(expected.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.Runtime.FormatVersion, actual.Runtime.FormatVersion);
        Assert.Equal(expected.Runtime.LastAdvancedTick, actual.Runtime.LastAdvancedTick);
        Assert.Equal(expected.Runtime.RegionIndex, actual.Runtime.RegionIndex);
        Assert.Equal(expected.Runtime.BiomeId, actual.Runtime.BiomeId);
        Assert.Equal(expected.Runtime.SubBiomeId, actual.Runtime.SubBiomeId);
        Assert.Equal(expected.Runtime.Status, actual.Runtime.Status);
        Assert.Equal(expected.Runtime.ActiveEventId, actual.Runtime.ActiveEventId);
        Assert.Equal(expected.Runtime.LastEventId, actual.Runtime.LastEventId);
        Assert.Equal(expected.Runtime.StartTick, actual.Runtime.StartTick);
        Assert.Equal(expected.Runtime.EndTickExclusive, actual.Runtime.EndTickExclusive);
        Assert.Equal(expected.Runtime.PhaseId, actual.Runtime.PhaseId);
        Assert.Equal(expected.Runtime.PhaseIndex, actual.Runtime.PhaseIndex);
        Assert.Equal(expected.Runtime.Progress, actual.Runtime.Progress);
        Assert.Equal(expected.Runtime.PhaseProgress, actual.Runtime.PhaseProgress);
        Assert.Equal(expected.Runtime.Intensity, actual.Runtime.Intensity);
        Assert.Equal(expected.Runtime.EffectiveModifiers, actual.Runtime.EffectiveModifiers);
        Assert.Equal(expected.Runtime.ActivationSource, actual.Runtime.ActivationSource);
        Assert.Equal(expected.Runtime.TriggerAction, actual.Runtime.TriggerAction);
        Assert.Equal(expected.Runtime.TriggerSequence, actual.Runtime.TriggerSequence);
        Assert.Equal(expected.Runtime.Cooldowns.ToArray(), actual.Runtime.Cooldowns.ToArray());
        Assert.Equal(expected.Journal.FormatVersion, actual.Journal.FormatVersion);
        Assert.Equal(expected.Journal.NextSequence, actual.Journal.NextSequence);
        Assert.Equal(expected.Journal.Entries.ToArray(), actual.Journal.Entries.ToArray());
        Assert.Equal(
            expected.LastProcessedPlayerActionSequence,
            actual.LastProcessedPlayerActionSequence);
    }
}
