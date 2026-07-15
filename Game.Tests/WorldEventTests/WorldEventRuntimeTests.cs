using System.Text.Json;
using Game.Core.World;
using Game.Core.WorldEvents;
using Xunit;

namespace Game.Tests.WorldEventTests;

public sealed class WorldEventRuntimeTests
{
    [Fact]
    public void DataRegistry_LoadsProductionEventsWithPhasesAndModifiers()
    {
        var dataRoot = FindGameDataRoot();
        var registry = new WorldEventDefinitionJsonLoader().LoadRegistryFromDirectory(
            Path.Combine(dataRoot, "world-events"));

        var fireflies = registry.GetById("firefly_bloom");
        var crystals = registry.GetById("crystal_surge");

        Assert.Equal(5, registry.Definitions.Count);
        Assert.Equal(3, fireflies.Phases.Count);
        Assert.True(fireflies.RequiresNight);
        Assert.True(crystals.RequiresUnderground);
        Assert.Contains(WorldEventPlayerActionKind.Mine, crystals.PlayerActionTriggers);
        Assert.Contains(WorldEventPlayerActionKind.Build, registry.GetById("amberfall").PlayerActionTriggers);
        Assert.Equal("effects/ambient/crystal_glints", crystals.Modifiers.ParticleSpriteId);
        Assert.True(crystals.Modifiers.RareLootChanceMultiplier > 1f);
        Assert.Equal(3, registry.GetById("amberfall").Phases.Count);
        Assert.True(registry.GetById("lantern_tide").RequiresNight);
        Assert.Equal(3, registry.GetById("wildlife_migration").Phases.Count);
    }

    [Theory]
    [InlineData(-99L)]
    [InlineData(0L)]
    [InlineData(99L)]
    public void Executor_IsDeterministicAcrossNegativeAndPositiveRegions(long regionIndex)
    {
        var first = CreateExecutor();
        var second = CreateExecutor();
        var firstSnapshot = WorldEventRuntimeSnapshot.Inactive(Context(0, regionIndex));
        var secondSnapshot = WorldEventRuntimeSnapshot.Inactive(Context(0, regionIndex));

        for (var tick = 0L; tick < 48; tick++)
        {
            firstSnapshot = first.Advance(firstSnapshot, Context(tick, regionIndex)).Snapshot;
            secondSnapshot = second.Advance(secondSnapshot, Context(tick, regionIndex)).Snapshot;
            AssertSnapshotsEqual(firstSnapshot, secondSnapshot);
        }
    }

    [Fact]
    public void Executor_ActivatesAdvancesPhasesCompletesAndStartsCooldown()
    {
        var executor = CreateExecutor();
        var snapshot = WorldEventRuntimeSnapshot.Inactive(Context(0, -7));
        WorldEventExecutionResult activation = default;

        for (var tick = 0L; tick < 16; tick++)
        {
            activation = executor.Advance(snapshot, Context(tick, -7));
            snapshot = activation.Snapshot;
            if (snapshot.Status == WorldEventRuntimeStatus.Active)
            {
                break;
            }
        }

        Assert.Equal(WorldEventRuntimeStatus.Active, snapshot.Status);
        Assert.Contains(activation.Events, value => value.Kind == WorldEventDomainEventKind.Activated);
        Assert.Equal("awakening", snapshot.PhaseId);

        var middleTick = snapshot.StartTick + 6;
        var middle = executor.Advance(snapshot, Context(middleTick, -7));
        Assert.Equal("surge", middle.Snapshot.PhaseId);
        Assert.True(middle.Snapshot.EffectiveModifiers.SpawnWeightMultiplier > 1f);
        Assert.Contains(middle.Events, value => value.Kind == WorldEventDomainEventKind.PhaseChanged);

        var completed = executor.Advance(
            middle.Snapshot,
            Context(middle.Snapshot.EndTickExclusive, -7));

        Assert.Equal(WorldEventRuntimeStatus.Cooldown, completed.Snapshot.Status);
        Assert.Null(completed.Snapshot.ActiveEventId);
        Assert.Equal("test_event", completed.Snapshot.LastEventId);
        Assert.Single(completed.Snapshot.Cooldowns);
        Assert.Contains(completed.Events, value => value.Kind == WorldEventDomainEventKind.Completed);
        Assert.Contains(completed.Events, value => value.Kind == WorldEventDomainEventKind.CooldownStarted);
    }

    [Fact]
    public void RuntimeSnapshot_RoundTripsAndContinuesWithoutDivergence()
    {
        var executor = CreateExecutor();
        var snapshot = WorldEventRuntimeSnapshot.Inactive(Context(0, -12));
        for (var tick = 0L; tick < 12 && snapshot.Status != WorldEventRuntimeStatus.Active; tick++)
        {
            snapshot = executor.Advance(snapshot, Context(tick, -12)).Snapshot;
        }

        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<WorldEventRuntimeSnapshot>(json)!;
        var tickToAdvance = snapshot.LastAdvancedTick + 1;
        var expected = executor.Advance(snapshot, Context(tickToAdvance, -12)).Snapshot;
        var actual = executor.Advance(restored, Context(tickToAdvance, -12)).Snapshot;

        AssertSnapshotsEqual(expected, actual);
    }

    [Fact]
    public void Journal_IsBoundedOrderedAndSaveReady()
    {
        var journal = new WorldEventJournal(capacity: 3);
        for (var tick = 0L; tick < 5; tick++)
        {
            var snapshot = WorldEventRuntimeSnapshot.Inactive(Context(tick, 0));
            journal.Append(new WorldEventExecutionResult(
                snapshot,
                [new WorldEventDomainEvent(0, tick, 0, "test_event", WorldEventDomainEventKind.Progressed, "surge", 0.5f, 0)]));
        }

        var captured = journal.Capture();
        var json = JsonSerializer.Serialize(captured);
        var restoredSnapshot = JsonSerializer.Deserialize<WorldEventJournalSnapshot>(json)!;
        var restored = new WorldEventJournal(capacity: 3);
        restored.Restore(restoredSnapshot);

        Assert.Equal(5, captured.NextSequence);
        Assert.Equal(new long[] { 2, 3, 4 }, captured.Entries.Select(value => value.Sequence));
        var restoredCapture = restored.Capture();
        Assert.Equal(captured.FormatVersion, restoredCapture.FormatVersion);
        Assert.Equal(captured.NextSequence, restoredCapture.NextSequence);
        Assert.Equal(captured.Entries.ToArray(), restoredCapture.Entries.ToArray());
    }

    [Fact]
    public void ModifierApplier_ClampsRuntimeOutputs()
    {
        var modifiers = WorldEventModifierSet.Identity with
        {
            SpawnWeightMultiplier = 2f,
            AmbientLightAdd = 0.4f,
            WeatherIntensityMultiplier = 3f,
            RareLootChanceMultiplier = 4f
        };

        var values = WorldEventModifierApplier.Apply(modifiers, 2f, 0.8f, 0.8f, 0.7f, 1f, 0.4f);

        Assert.Equal(4f, values.SpawnWeight);
        Assert.Equal(1f, values.AmbientLight);
        Assert.Equal(1f, values.WeatherIntensity);
        Assert.Equal(1f, values.RareLootChance);
    }

    private static DeterministicWorldEventExecutor CreateExecutor()
    {
        var definition = new WorldEventDefinition
        {
            Id = "test_event",
            ChancePerWindow = 1f,
            MinDurationTicks = 12,
            MaxDurationTicks = 12,
            CooldownTicks = 20,
            Intensity = 1f,
            AllowedBiomeIds = ["forest"],
            Modifiers = WorldEventModifierSet.Identity with { SpawnWeightMultiplier = 1.5f },
            Phases =
            [
                new WorldEventPhaseDefinition
                {
                    Id = "awakening",
                    StartProgress = 0f,
                    EndProgress = 0.25f
                },
                new WorldEventPhaseDefinition
                {
                    Id = "surge",
                    StartProgress = 0.25f,
                    EndProgress = 0.75f,
                    Modifiers = WorldEventModifierSet.Identity with { SpawnWeightMultiplier = 1.25f }
                },
                new WorldEventPhaseDefinition
                {
                    Id = "fade",
                    StartProgress = 0.75f,
                    EndProgress = 1f
                }
            ]
        };
        return new DeterministicWorldEventExecutor(
            9182,
            WorldEventDefinitionRegistry.Create([definition]),
            windowTicks: 16);
    }

    private static WorldEventExecutionContext Context(long tick, long region)
    {
        return new WorldEventExecutionContext(
            tick,
            region,
            "forest",
            null,
            "clear",
            0f,
            true,
            false,
            0.1f,
            new TilePos((int)Math.Clamp(region * 32, int.MinValue, int.MaxValue), 40));
    }

    private static void AssertSnapshotsEqual(
        WorldEventRuntimeSnapshot expected,
        WorldEventRuntimeSnapshot actual)
    {
        Assert.Equal(expected.FormatVersion, actual.FormatVersion);
        Assert.Equal(expected.LastAdvancedTick, actual.LastAdvancedTick);
        Assert.Equal(expected.RegionIndex, actual.RegionIndex);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.ActiveEventId, actual.ActiveEventId);
        Assert.Equal(expected.PhaseId, actual.PhaseId);
        Assert.Equal(expected.Progress, actual.Progress);
        Assert.Equal(expected.EffectiveModifiers, actual.EffectiveModifiers);
        Assert.Equal(expected.Cooldowns.ToArray(), actual.Cooldowns.ToArray());
    }

    private static string FindGameDataRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
