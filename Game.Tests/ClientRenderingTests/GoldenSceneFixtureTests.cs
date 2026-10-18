using Game.Client;
using Game.Client.Diagnostics;
using Game.Client.Configuration;
using Game.Client.GameStates;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class GoldenSceneFixtureTests
{
    [Theory]
    [InlineData(double.NaN, 3)]
    [InlineData(0.1, double.PositiveInfinity)]
    [InlineData(-0.1, 0)]
    public void Clock_RejectsInvalidFixtureTime(double delta, double total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameTimeService().BeginFrame(delta, total));
    }

    [Fact]
    public void Clock_RetainsExactSixtyHzWithoutTimeSpanQuantization()
    {
        var time = new GameTimeService();
        for (var frame = 1; frame <= 180; frame++)
        {
            time.BeginFrame(1d / 60d, frame / 60d);
            Assert.Equal(1d / 60d, time.FrameDeltaSeconds);
        }
        Assert.Equal(3, time.TotalSeconds);
    }

    [Fact]
    public void UserRoot_RejectsExistingSettingsOrSaves()
    {
        using var temp = new FixtureRoot();
        GoldenSceneFixture.RequireEmptyUserRoot(temp.Root);
        File.WriteAllText(Path.Combine(temp.Root, "settings.json"), "{}");
        Assert.Throws<InvalidDataException>(() => GoldenSceneFixture.RequireEmptyUserRoot(temp.Root));
        Assert.Throws<InvalidDataException>(() => GoldenSceneFixture.RequireEmptyUserRoot("relative"));
        Assert.Throws<InvalidDataException>(() => GoldenSceneFixture.RequireEmptyUserRoot(null));
    }

    [Fact]
    public void FixtureLoader_BindsClockViewportAndSceneAndRejectsUnknownFields()
    {
        using var temp = new FixtureRoot();
        var path = Path.Combine(temp.Root, "fixture.json");
        File.WriteAllText(path, """{"SceneId":"forest_night","TimeOfDay":0,"DynamicLights":true,"Frames":180,"WarmupFrames":60}""");
        var options = ClientSmokeOptions.Parse(["--smoke", "--frames", "3", "--resolution", "640x360", "--golden-fixture", path])!;
        Assert.Equal(180, options.Frames);
        Assert.Equal(60, options.WarmupFrames);
        Assert.Equal(960, options.Width);
        Assert.Equal(0, options.ForcedTimeOfDay);
        Assert.True(options.GoldenFixture!.DynamicLights);
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(["--golden-fixture", path]));
        File.WriteAllText(path, """{"SceneId":"forest","Unrecognized":true}""");
        Assert.Throws<System.Text.Json.JsonException>(() => GoldenSceneFixture.Load(path));
    }

    [Fact]
    public void ContentFingerprint_IgnoresJsonLineEndingsButDetectsContentChanges()
    {
        using var temp = new FixtureRoot();
        var path = Path.Combine(temp.Root, "content.json");
        File.WriteAllText(path, "{\r\n  \"seed\": 1337\r\n}");
        var expected = GoldenSceneFixture.ContentFingerprint(temp.Root);
        File.WriteAllText(path, "{\"seed\":1337}");
        Assert.Equal(expected, GoldenSceneFixture.ContentFingerprint(temp.Root));
        File.WriteAllText(path, "{\"seed\":1338}");
        Assert.NotEqual(expected, GoldenSceneFixture.ContentFingerprint(temp.Root));
    }

    [Fact]
    public void FixedScene_ReplaysOneSimulationWithStableCameraWeatherAndLiveProjectileLights()
    {
        var fixture = new GoldenSceneFixture { SceneId = "forest_night", TimeOfDay = 0, DynamicLights = true };
        var first = RunScene(fixture);
        var second = RunScene(fixture);
        Assert.Equal(180L, first["SimulationTick"]);
        Assert.Equal("forest", first["BiomeId"]);
        Assert.Equal("Clear", first["Weather"]);
        Assert.Equal(0d, first["TimeOfDay"]);
        Assert.Equal(first["StateHash"], second["StateHash"]);
        Assert.Equal(first["CameraX"], second["CameraX"]);
        Assert.Equal(first["CameraY"], second["CameraY"]);
        Assert.Equal(first["LoadedChunks"], second["LoadedChunks"]);
    }

    private static Dictionary<string, object> RunScene(GoldenSceneFixture fixture)
    {
        using var temp = new FixtureRoot();
        var previous = Environment.GetEnvironmentVariable(ClientPaths.UserDataRootEnvironmentVariable);
        Environment.SetEnvironmentVariable(ClientPaths.UserDataRootEnvironmentVariable, temp.Root);
        try
        {
            using var states = new GameStateManager();
            using var playing = new PlayingState(states, goldenFixture: fixture,
                forcedBiomeId: fixture.BiomeId, forcedTimeOfDay: fixture.TimeOfDay, forcedWeather: fixture.Weather);
            playing.Initialize();
            for (var tick = 0; tick < fixture.Frames; tick++)
            {
                playing.Update(1d / 60d);
                playing.FixedUpdate(1f / 60f);
            }
            var gameplay = playing.CaptureGameplaySmokeTelemetry();
            Assert.Equal(2, gameplay.TotalActiveEntities);
            Assert.Contains("arcane_mote", gameplay.VisibleContentIds);
            Assert.Contains("firebolt", gameplay.VisibleContentIds);
            var data = new Dictionary<string, object>();
            playing.CaptureGoldenScene(data);
            return data;
        }
        finally
        {
            Environment.SetEnvironmentVariable(ClientPaths.UserDataRootEnvironmentVariable, previous);
        }
    }

    private sealed class FixtureRoot : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "yjse-golden-tests", Guid.NewGuid().ToString("N"));
        public FixtureRoot() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
