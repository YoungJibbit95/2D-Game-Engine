using System.Text.Json;

using Game.Core.Biomes;
using Game.Core.Weather;
using Game.Core.WorldEvents;
using System.Runtime.CompilerServices;
using Xunit;

namespace Game.Tests.WeatherTests;

public sealed class DeterministicWeatherSystemTests
{
    [Fact]
    public void Snapshot_RoundTripsAndReplaysWeatherTransitions()
    {
        var biome = CreateBiome();
        var system = new DeterministicWeatherSystem(7123);
        var initial = system.CreateSnapshot(biome);
        var transitionTick = initial.Current.EndTickExclusive;
        var transitioned = system.Advance(biome, initial, transitionTick);

        Assert.NotEqual(transitioned.Previous.Kind, transitioned.Current.Kind);
        Assert.Equal(0f, system.ResolveTransition(transitioned, transitionTick).Progress);
        Assert.InRange(
            system.ResolveTransition(transitioned, transitionTick + 2).Progress,
            0.39f,
            0.41f);

        var json = JsonSerializer.Serialize(transitioned);
        var restored = JsonSerializer.Deserialize<WeatherSystemSnapshot>(json);
        Assert.Equal(transitioned, restored);
        Assert.Equal(
            system.Advance(biome, transitioned, transitionTick + 7),
            system.Advance(biome, restored, transitionTick + 7));
    }

    [Fact]
    public void FrameStateContracts_AreValueTypesAndSteadyStateCallsAllocateNothing()
    {
        Assert.True(typeof(WeatherState).IsValueType);
        Assert.True(typeof(AmbientState).IsValueType);
        Assert.True(typeof(WorldEventState).IsValueType);

        var biome = CreateBiome();
        var weatherSystem = new DeterministicWeatherSystem(42);
        var ambientSystem = new AmbientStateService();
        var eventSystem = new DeterministicWorldEventSystem(
            42,
            [
                new WorldEventDefinition
                {
                    Id = "test_event",
                    ChancePerWindow = 1f,
                    MinDurationTicks = 60,
                    MaxDurationTicks = 60
                }
            ],
            windowTicks: 60);

        ExerciseFrameState(biome, weatherSystem, ambientSystem, eventSystem, 0, 256);
        Assert.Equal(0, MeasureUntilConsecutiveAllocationFreeWindows(
            biome,
            weatherSystem,
            ambientSystem,
            eventSystem));
    }

    private static long MeasureUntilConsecutiveAllocationFreeWindows(
        BiomeDefinition biome,
        DeterministicWeatherSystem weatherSystem,
        AmbientStateService ambientSystem,
        DeterministicWorldEventSystem eventSystem)
    {
        const int maximumWindows = 6;
        const int iterationsPerWindow = 1_000;
        var consecutiveAllocationFreeWindows = 0;
        var lastAllocated = long.MaxValue;
        for (var window = 0; window < maximumWindows; window++)
        {
            var startTick = window * iterationsPerWindow;
            lastAllocated = MeasureAllocationWindow(
                biome,
                weatherSystem,
                ambientSystem,
                eventSystem,
                startTick,
                iterationsPerWindow);
            consecutiveAllocationFreeWindows = lastAllocated == 0
                ? consecutiveAllocationFreeWindows + 1
                : 0;
            if (consecutiveAllocationFreeWindows == 2)
            {
                return 0;
            }
        }

        return lastAllocated;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureAllocationWindow(
        BiomeDefinition biome,
        DeterministicWeatherSystem weatherSystem,
        AmbientStateService ambientSystem,
        DeterministicWorldEventSystem eventSystem,
        long startTick,
        int iterations)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        ExerciseFrameState(
            biome,
            weatherSystem,
            ambientSystem,
            eventSystem,
            startTick,
            iterations);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExerciseFrameState(
        BiomeDefinition biome,
        DeterministicWeatherSystem weatherSystem,
        AmbientStateService ambientSystem,
        DeterministicWorldEventSystem eventSystem,
        long startTick,
        int iterations)
    {
        var endTick = startTick + iterations;
        for (var tick = startTick; tick < endTick; tick++)
        {
            var weather = weatherSystem.GetState(biome, tick);
            _ = ambientSystem.Resolve(biome, null, weather, false, 0.8f);
            _ = eventSystem.GetState(tick, -3, biome.Id, null);
        }
    }

    [Fact]
    public void AmbientState_CaveBaseLightIsIndependentFromSurfaceDaylight()
    {
        var biome = CreateBiome() with
        {
            Ambient = CreateBiome().Ambient with { BaseLight = 0.42f }
        };
        var weather = new WeatherState(
            WeatherKind.Clear,
            StartTick: 0,
            EndTickExclusive: 10,
            Intensity: 0f,
            Wind: 0f,
            CloudCover: 0f,
            Visibility: 1f,
            AmbientLightMultiplier: 0.8f);
        var ambient = new AmbientStateService();

        var caveAtNight = ambient.Resolve(biome, null, weather, isCave: true, daylight: 0f);
        var caveAtNoon = ambient.Resolve(biome, null, weather, isCave: true, daylight: 1f);
        var surfaceAtNight = ambient.Resolve(biome, null, weather, isCave: false, daylight: 0f);

        Assert.Equal(0.336f, caveAtNight.Light, precision: 3);
        Assert.Equal(caveAtNight.Light, caveAtNoon.Light);
        Assert.Equal(0f, surfaceAtNight.Light);
    }

    [Fact]
    public void StatelessPeriods_ContainEveryQueriedTickAtTransitionBoundaries()
    {
        var biome = CreateBiome() with
        {
            Weather = CreateBiome().Weather with
            {
                MinDurationTicks = 17,
                MaxDurationTicks = 31,
                TransitionDurationTicks = 4
            }
        };
        var system = new DeterministicWeatherSystem(1881);

        for (var tick = 0L; tick < 5_000; tick++)
        {
            var state = system.GetState(biome, tick);
            Assert.InRange(tick, state.StartTick, state.EndTickExclusive - 1);
        }
    }

    private static BiomeDefinition CreateBiome()
    {
        return new BiomeDefinition
        {
            Id = "forest",
            DisplayName = "Forest",
            SurfaceTile = "grass",
            UndergroundTile = "dirt",
            Weather = new BiomeWeatherProfile
            {
                ClearWeight = 1,
                RainWeight = 1,
                StormWeight = 1,
                FogWeight = 1,
                MinDurationTicks = 20,
                MaxDurationTicks = 20,
                TransitionDurationTicks = 5
            }
        };
    }
}
