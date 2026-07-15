using Game.Core.Randomness;
using Game.Core.Loot;
using Xunit;

namespace Game.Tests.DeterminismTests;

public sealed class SessionRandomRegistryTests
{
    [Fact]
    public void SameSeedAndName_ProduceSameTraceRegardlessOfStreamCreationOrder()
    {
        var firstRegistry = new SessionRandomRegistry(0xCAFE_BABE_1234_5678UL);
        var secondRegistry = new SessionRandomRegistry(0xCAFE_BABE_1234_5678UL);
        _ = secondRegistry.GetStream("loot");

        var firstTrace = DrawTrace(firstRegistry.GetStream("world-events"), 64);
        var secondTrace = DrawTrace(secondRegistry.GetStream("world-events"), 64);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(64UL, firstRegistry.GetStream("world-events").DrawCount);
    }

    [Fact]
    public void Streams_AreIsolatedFromDrawsOnOtherNamedStreams()
    {
        var referenceRegistry = new SessionRandomRegistry(91);
        var perturbedRegistry = new SessionRandomRegistry(91);
        var unrelatedStream = perturbedRegistry.GetStream("combat-effects");

        _ = DrawTrace(unrelatedStream, 1_000);

        var referenceTrace = DrawTrace(referenceRegistry.GetStream("spawning"), 128);
        var perturbedTrace = DrawTrace(perturbedRegistry.GetStream("spawning"), 128);

        Assert.Equal(referenceTrace, perturbedTrace);
        Assert.False(
            DrawTrace(referenceRegistry.GetStream("weather"), 8)
                .SequenceEqual(DrawTrace(referenceRegistry.GetStream("spawning"), 8)));
    }

    [Fact]
    public void ExportThenImport_RestoresExactStateAndPreservesExistingStreamReference()
    {
        var registry = new SessionRandomRegistry(2_024);
        var loot = registry.GetStream("loot");
        var weather = registry.GetStream("weather");
        _ = DrawTrace(loot, 17);
        _ = DrawTrace(weather, 9);
        var snapshot = registry.ExportSnapshot();

        var expectedLoot = DrawTrace(loot, 48);
        var expectedWeather = DrawTrace(weather, 48);
        registry.ImportSnapshot(snapshot);

        Assert.Same(loot, registry.GetStream("loot"));
        Assert.Same(weather, registry.GetStream("weather"));
        Assert.Equal(17UL, loot.DrawCount);
        Assert.Equal(9UL, weather.DrawCount);
        Assert.Equal(expectedLoot, DrawTrace(loot, 48));
        Assert.Equal(expectedWeather, DrawTrace(weather, 48));
    }

    [Fact]
    public void ImportSnapshot_RejectsSeedMismatchDuplicateNamesAndZeroState()
    {
        var registry = new SessionRandomRegistry(10);
        _ = registry.GetStream("loot");
        var validStream = Assert.Single(registry.ExportSnapshot().Streams);

        var seedMismatch = new RandomRegistrySnapshot
        {
            SessionSeed = 11,
            Streams = [validStream]
        };
        var duplicateNames = new RandomRegistrySnapshot
        {
            SessionSeed = 10,
            Streams = [validStream, validStream with { }]
        };
        var zeroState = new RandomRegistrySnapshot
        {
            SessionSeed = 10,
            Streams =
            [
                new RandomStreamSnapshot
                {
                    Name = "invalid",
                    State0 = 0,
                    State1 = 0,
                    State2 = 0,
                    State3 = 0,
                    DrawCount = 0
                }
            ]
        };

        Assert.Throws<InvalidDataException>(() => registry.ImportSnapshot(seedMismatch));
        Assert.Throws<InvalidDataException>(() => registry.ImportSnapshot(duplicateNames));
        Assert.Throws<InvalidDataException>(() => registry.ImportSnapshot(zeroState));
    }

    [Fact]
    public void SystemRandomAdapter_DelegatesToNamedStreamAndSupportsExistingLootApi()
    {
        var firstRegistry = new SessionRandomRegistry(444);
        var secondRegistry = new SessionRandomRegistry(444);
        var firstAdapter = firstRegistry.CreateSystemRandomAdapter("loot");
        var directStream = secondRegistry.GetStream("loot");

        Assert.Equal(directStream.NextInt32(100), firstAdapter.Next(100));
        Assert.Equal(directStream.NextInt32(-50, 75), firstAdapter.Next(-50, 75));
        Assert.Equal(directStream.NextSingle(), firstAdapter.NextSingle());
        Assert.Same(firstRegistry.GetStream("loot"), firstAdapter.Stream);

        var table = new LootTableDefinition
        {
            Id = "adapter-contract",
            Entries =
            [
                new LootEntryDefinition
                {
                    ItemId = "gel",
                    Min = 1,
                    Max = 5,
                    Guaranteed = true
                }
            ]
        };
        var expectedRoller = new LootRoller(secondRegistry.CreateSystemRandomAdapter("loot"));
        var actualRoller = new LootRoller(firstAdapter);

        Assert.Equal(expectedRoller.Roll(table), actualRoller.Roll(table));
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
