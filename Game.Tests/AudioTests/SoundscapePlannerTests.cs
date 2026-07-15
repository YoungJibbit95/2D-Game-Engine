using Game.Core.Audio;
using Game.Core.Runtime;
using Game.Core.Weather;
using Xunit;

namespace Game.Tests.AudioTests;

public sealed class SoundscapePlannerTests
{
    [Fact]
    public void Resolver_SelectsDayNightCaveWeatherAndEventLayers()
    {
        var resolver = new SoundscapeResolver(CreateCatalog());

        var day = resolver.Resolve(CreateLiving(), CreateTime(isNight: false));
        var night = resolver.Resolve(CreateLiving(), CreateTime(isNight: true));
        var cave = resolver.Resolve(CreateLiving(isUnderground: true), CreateTime(isNight: false));
        var worldEvent = resolver.Resolve(
            CreateLiving(isWorldEventActive: true, worldEventId: "firefly_bloom"),
            CreateTime(isNight: false));

        Assert.Equal("music.forest.day", day.MusicLoopId);
        Assert.Equal("music.forest.night", night.MusicLoopId);
        Assert.Equal("music.forest.cave", cave.MusicLoopId);
        Assert.Equal("ambient.forest.cave", cave.AmbientLoopId);
        Assert.Null(cave.WeatherLoopId);
        Assert.Equal("ambient.event.fireflies", worldEvent.WorldEventLoopId);
        Assert.Equal("stinger.event.fireflies", worldEvent.WorldEventStingerId);
    }

    [Fact]
    public void Planner_EmitsTransitionsAndOneShotStingersOnlyOnEdges()
    {
        var planner = new SoundscapeCommandPlanner(new SoundscapeResolver(CreateCatalog()));
        var commands = new AudioPresentationCommand[SoundscapeCommandPlanner.MaximumCommandsPerFrame];

        var initialCount = planner.Plan(CreateLiving(weather: WeatherKind.Rain), CreateTime(false), commands);
        var stableCount = planner.Plan(CreateLiving(weather: WeatherKind.Rain), CreateTime(false), commands);
        var stormCount = planner.Plan(CreateLiving(weather: WeatherKind.Storm), CreateTime(false), commands);
        var emittedStormStinger = commands[..stormCount].Any(command =>
            command.Kind == AudioPresentationCommandKind.PlayStinger &&
            command.AudioId == "stinger.weather.storm");
        var eventCount = planner.Plan(
            CreateLiving(
                weather: WeatherKind.Storm,
                isWorldEventActive: true,
                worldEventId: "firefly_bloom"),
            CreateTime(false),
            commands);
        var emittedEventStinger = commands[..eventCount].Any(command =>
            command.Kind == AudioPresentationCommandKind.PlayStinger &&
            command.AudioId == "stinger.event.fireflies");
        var stableEventCount = planner.Plan(
            CreateLiving(
                weather: WeatherKind.Storm,
                isWorldEventActive: true,
                worldEventId: "firefly_bloom"),
            CreateTime(false),
            commands);

        Assert.Equal(3, initialCount);
        Assert.Equal(0, stableCount);
        Assert.Equal(2, stormCount);
        Assert.True(emittedStormStinger);
        Assert.Equal(2, eventCount);
        Assert.True(emittedEventStinger);
        Assert.Equal(0, stableEventCount);
        Assert.Equal(2, planner.Telemetry.Stingers);
    }

    [Fact]
    public void Planner_MissingDefinitionStopsPreviousLoopsAndReportsTelemetry()
    {
        var planner = new SoundscapeCommandPlanner(new SoundscapeResolver(CreateCatalog()));
        var commands = new AudioPresentationCommand[SoundscapeCommandPlanner.MaximumCommandsPerFrame];
        _ = planner.Plan(CreateLiving(), CreateTime(false), commands);

        var count = planner.Plan(CreateLiving(soundscapeId: "unknown"), CreateTime(false), commands);

        Assert.Equal(3, count);
        Assert.All(commands[..count], command => Assert.Equal(AudioPresentationCommandKind.StopLoop, command.Kind));
        Assert.Equal(1, planner.Telemetry.MissingDefinitions);
        Assert.Equal("unknown", planner.Telemetry.LastMissingSoundscapeId);
    }

    [Fact]
    public void Catalog_ValidatesDefinitionsAndDuplicateIds()
    {
        var catalog = new SoundscapeCatalog();
        var definition = new SoundscapeDefinition { Id = "forest" };
        catalog.Register(definition);

        Assert.Throws<InvalidDataException>(() => catalog.Register(definition));
        Assert.Throws<InvalidDataException>(() => SoundscapeDefinition.Validate(new SoundscapeDefinition
        {
            Id = "bad",
            MusicVolume = 2f
        }));
    }

    [Fact]
    public void JsonLoader_LoadsValidatedEventAndWeatherRoutes()
    {
        const string Json = """
            {
              "id": "meadow",
              "dayMusicLoopId": "music.meadow.day",
              "rainLoopId": "ambient.rain",
              "worldEvents": {
                "meteor": {
                  "loopId": "ambient.meteor",
                  "startStingerId": "stinger.meteor",
                  "volume": 0.7
                }
              }
            }
            """;

        var definition = new SoundscapeDefinitionJsonLoader().LoadDefinitionFromJson(Json);

        Assert.Equal("meadow", definition.Id);
        Assert.Equal("ambient.rain", definition.RainLoopId);
        Assert.Equal("stinger.meteor", definition.WorldEvents["meteor"].StartStingerId);
    }

    [Fact]
    public void Planner_EmitsEventStingerWhenLoadingIntoActiveEvent()
    {
        var planner = new SoundscapeCommandPlanner(new SoundscapeResolver(CreateCatalog()));
        var commands = new AudioPresentationCommand[SoundscapeCommandPlanner.MaximumCommandsPerFrame];

        var count = planner.Plan(
            CreateLiving(isWorldEventActive: true, worldEventId: "FIREFLY_BLOOM"),
            CreateTime(false),
            commands);

        Assert.Contains(commands[..count], command => command.AudioId == "stinger.event.fireflies");
    }

    private static SoundscapeCatalog CreateCatalog()
    {
        var catalog = new SoundscapeCatalog();
        catalog.Register(new SoundscapeDefinition
        {
            Id = "forest",
            DayMusicLoopId = "music.forest.day",
            NightMusicLoopId = "music.forest.night",
            UndergroundMusicLoopId = "music.forest.cave",
            SurfaceAmbientLoopId = "ambient.forest.surface",
            UndergroundAmbientLoopId = "ambient.forest.cave",
            RainLoopId = "ambient.weather.rain",
            StormLoopId = "ambient.weather.storm",
            RainStartStingerId = "stinger.weather.rain",
            StormStartStingerId = "stinger.weather.storm",
            WorldEvents = new Dictionary<string, SoundscapeEventDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["firefly_bloom"] = new SoundscapeEventDefinition
                {
                    LoopId = "ambient.event.fireflies",
                    StartStingerId = "stinger.event.fireflies",
                    Volume = 0.8f
                }
            }
        });
        return catalog;
    }

    internal static LivingWorldFrameSnapshot CreateLiving(
        string soundscapeId = "forest",
        bool isUnderground = false,
        WeatherKind weather = WeatherKind.Rain,
        bool isWorldEventActive = false,
        string? worldEventId = null)
    {
        return new LivingWorldFrameSnapshot(
            RegionIndex: 0,
            RegionStartTileX: 0,
            RegionEndTileXInclusive: 255,
            BiomeId: "forest",
            BiomeDisplayName: "Forest",
            SubBiomeId: null,
            SubBiomeDisplayName: null,
            BiomeLayerId: isUnderground ? "cave" : "surface",
            CaveProfileId: isUnderground ? "forest_cave" : null,
            IsUnderground: isUnderground,
            SoundscapeId: soundscapeId,
            AmbientLight: 0.6f,
            Visibility: 1f,
            Temperature: 0.5f,
            Humidity: 0.6f,
            ColorGradeId: "forest",
            SkyLightMultiplier: 1f,
            EmissiveLightMultiplier: 1f,
            FogDensity: 0f,
            SpawnDensityMultiplier: 1f,
            OreDensityMultiplier: 1f,
            VegetationDensityMultiplier: 1f,
            ForageDensityMultiplier: 1f,
            Weather: weather,
            WeatherIntensity: weather == WeatherKind.Clear ? 0f : 0.8f,
            Wind: 0.2f,
            CloudCover: 0.5f,
            WeatherStartTick: 0,
            WeatherEndTickExclusive: 3_600,
            IsWorldEventActive: isWorldEventActive,
            WorldEventId: worldEventId,
            WorldEventProgress: isWorldEventActive ? 0.5f : 0f,
            WorldEventIntensity: isWorldEventActive ? 0.75f : 0f,
            Presentation: new LivingWorldPresentationFrameSnapshot(
                null,
                null,
                null,
                null,
                null,
                0.25f,
                isUnderground ? 0.7f : 0f,
                0.25f,
                1f));
    }

    internal static WorldTimeFrameSnapshot CreateTime(bool isNight)
    {
        return new WorldTimeFrameSnapshot(1, isNight ? 50_000 : 10_000, 86_400, isNight ? 0.8 : 0.2, isNight);
    }
}
