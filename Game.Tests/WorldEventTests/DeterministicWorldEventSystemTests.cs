using System.Text.Json;

using Game.Core.WorldEvents;
using Xunit;

namespace Game.Tests.WorldEventTests;

public sealed class DeterministicWorldEventSystemTests
{
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(long.MaxValue)]
    public void GetState_IsDeterministicAcrossExtremeRegionIndices(long regionIndex)
    {
        var first = CreateSystem();
        var second = CreateSystem();

        Assert.Equal(
            first.GetState(123, regionIndex, "forest", null),
            second.GetState(123, regionIndex, "forest", null));
    }

    [Fact]
    public void Snapshot_RoundTripsAndContinuesDeterministicReplay()
    {
        var system = CreateSystem();
        var snapshot = system.CreateSnapshot(123, -8, "forest", null);
        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<WorldEventSystemSnapshot>(json);

        Assert.Equal(snapshot, restored);
        Assert.Equal(
            system.Advance(snapshot, 456, -8, "forest", null),
            system.Advance(restored, 456, -8, "forest", null));
    }

    private static DeterministicWorldEventSystem CreateSystem()
    {
        return new DeterministicWorldEventSystem(
            9182,
            [
                new WorldEventDefinition
                {
                    Id = "firefly_bloom",
                    ChancePerWindow = 1f,
                    MinDurationTicks = 600,
                    MaxDurationTicks = 600,
                    Intensity = 0.75f,
                    AllowedBiomeIds = ["forest"]
                }
            ],
            windowTicks: 600);
    }
}
