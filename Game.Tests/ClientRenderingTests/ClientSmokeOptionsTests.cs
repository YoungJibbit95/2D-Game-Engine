using Game.Client.Diagnostics;
using Game.Core.Weather;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class ClientSmokeOptionsTests
{
    [Fact]
    public void Parse_WithoutSmokeSwitchLeavesNormalClientModeUntouched()
    {
        Assert.Null(ClientSmokeOptions.Parse(Array.Empty<string>()));
    }

    [Fact]
    public void Parse_ReadsBoundedFrameCountAndOutput()
    {
        var options = ClientSmokeOptions.Parse(
            ["--smoke", "--frames", "5", "--output", "artifacts/frame.png", "--timeout-seconds", "17", "--start-state", "world-select"]);

        Assert.NotNull(options);
        Assert.Equal(5, options.Frames);
        Assert.Equal("artifacts/frame.png", options.ScreenshotPath);
        Assert.Equal(17, options.TimeoutSeconds);
        Assert.Equal(ClientSmokeStartState.WorldSelect, options.StartState);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3601")]
    [InlineData("invalid")]
    public void Parse_RejectsInvalidFrameCounts(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(["--smoke", "--frames", value]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("301")]
    [InlineData("invalid")]
    public void Parse_RejectsInvalidTimeouts(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(["--smoke", "--timeout-seconds", value]));
    }

    [Theory]
    [InlineData("--frames")]
    [InlineData("--output")]
    [InlineData("--timeout-seconds")]
    [InlineData("--start-state")]
    [InlineData("--scene-biome")]
    [InlineData("--scene-time")]
    [InlineData("--scene-weather")]
    [InlineData("--scene-weather-intensity")]
    [InlineData("--resolution")]
    [InlineData("--warmup-frames")]
    [InlineData("--frame-limit")]
    public void Parse_RejectsOptionsWithoutValues(string option)
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(["--smoke", option]));
    }

    [Fact]
    public void Parse_RejectsUnknownStartState()
    {
        Assert.Throws<ArgumentException>(() =>
            ClientSmokeOptions.Parse(["--smoke", "--start-state", "unknown"]));
    }

    [Fact]
    public void Parse_AllowsOpenedConsolePlayingSmoke()
    {
        var options = ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--open-console"]);

        Assert.NotNull(options);
        Assert.Equal(ClientSmokeStartState.Playing, options.StartState);
        Assert.True(options.OpenConsole);
    }

    [Fact]
    public void Parse_AllowsOpenedPausePlayingSmoke()
    {
        var options = ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--open-pause"]);

        Assert.NotNull(options);
        Assert.True(options.OpenPause);
    }

    [Fact]
    public void Parse_RejectsOpenedPauseOutsidePlayingSmoke()
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "main-menu", "--open-pause"]));
    }

    [Fact]
    public void Parse_AllowsOpenedCraftingPlayingSmoke()
    {
        var options = ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--open-crafting"]);

        Assert.NotNull(options);
        Assert.True(options.OpenCrafting);
    }

    [Fact]
    public void Parse_RejectsOpenedCraftingOutsidePlayingSmoke()
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "main-menu", "--open-crafting"]));
    }

    [Theory]
    [InlineData("--open-pause")]
    [InlineData("--open-console")]
    public void Parse_RejectsOpenedCraftingWithExclusiveOverlay(string conflictingOption)
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--open-crafting", conflictingOption]));
    }

    [Theory]
    [InlineData("meadow", "meadow")]
    [InlineData("forest", "forest")]
    [InlineData("mushroom-cave", "mushroom_cave")]
    [InlineData("crystal-depths", "crystal_depths")]
    [InlineData("amber-grove", "amber_grove")]
    [InlineData("twilight_marsh", "twilight_marsh")]
    public void Parse_AllowsForcedLivingWorldScenes(string input, string expected)
    {
        var options = ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--scene-biome", input]);

        Assert.NotNull(options);
        Assert.Equal(expected, options.ForcedBiomeId);
    }

    [Theory]
    [InlineData("../forest")]
    [InlineData("_forest")]
    [InlineData("forest/")]
    [InlineData("forest cave")]
    public void Parse_RejectsUnsafeForcedBiomeIds(string input)
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--scene-biome", input]));
    }

    [Fact]
    public void Parse_RejectsForcedBiomeOutsidePlayingSmoke()
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "main-menu", "--scene-biome", "forest"]));
    }

    [Theory]
    [InlineData("midnight", 0d)]
    [InlineData("dawn", 0.25d)]
    [InlineData("day", 0.5d)]
    [InlineData("dusk", 0.75d)]
    [InlineData("0.625", 0.625d)]
    public void Parse_AllowsDeterministicPlayingSceneTime(string value, double expected)
    {
        var options = Assert.IsType<ClientSmokeOptions>(ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--scene-time", value]));

        Assert.Equal(expected, options.ForcedTimeOfDay);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1")]
    [InlineData("later")]
    public void Parse_RejectsInvalidSceneTime(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--scene-time", value]));
    }

    [Fact]
    public void Parse_RejectsForcedTimeOutsidePlayingSmoke()
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--scene-time", "day"]));
    }

    [Theory]
    [InlineData("clear", WeatherKind.Clear)]
    [InlineData("rain", WeatherKind.Rain)]
    [InlineData("snow", WeatherKind.Snow)]
    [InlineData("blizzard", WeatherKind.Blizzard)]
    public void Parse_AllowsDeterministicPlayingSceneWeather(string value, WeatherKind expected)
    {
        var options = Assert.IsType<ClientSmokeOptions>(ClientSmokeOptions.Parse(
        [
            "--smoke",
            "--start-state", "playing",
            "--scene-weather", value,
            "--scene-weather-intensity", "0.625"
        ]));

        Assert.Equal(expected, options.ForcedWeather);
        Assert.Equal(0.625f, options.ForcedWeatherIntensity, 3);
    }

    [Theory]
    [InlineData("hail")]
    [InlineData("snowstorm")]
    public void Parse_RejectsUnknownSceneWeather(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--scene-weather", value]));
    }

    [Fact]
    public void Parse_RejectsWeatherIntensityWithoutWeather()
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--start-state", "playing", "--scene-weather-intensity", "0.5"]));
    }
    [Fact]
    public void Parse_AllowsRepresentativeResolutionAndWarmupWindow()
    {
        var options = ClientSmokeOptions.Parse(
            ["--smoke", "--frames", "600", "--warmup-frames", "120", "--resolution", "1920x1080"]);

        Assert.NotNull(options);
        Assert.Equal(600, options.Frames);
        Assert.Equal(120, options.WarmupFrames);
        Assert.Equal(1920, options.Width);
        Assert.Equal(1080, options.Height);
    }

    [Fact]
    public void Parse_AcceptsConfiguredVideoDebugAndTraversalOptions()
    {
        var options = Assert.IsType<ClientSmokeOptions>(ClientSmokeOptions.Parse(
        [
            "--smoke",
            "--start-state", "playing",
            "--use-configured-video",
            "--include-debug-overlays",
            "--scripted-traversal"
        ]));

        Assert.True(options.UseConfiguredVideoSettings);
        Assert.True(options.IncludeDebugOverlays);
        Assert.True(options.ScriptedTraversal);
    }

    [Theory]
    [InlineData("639x360")]
    [InlineData("1920-1080")]
    [InlineData("7681x4320")]
    public void Parse_RejectsInvalidSmokeResolution(string resolution)
    {
        Assert.Throws<ArgumentException>(() =>
            ClientSmokeOptions.Parse(["--smoke", "--resolution", resolution]));
    }

    [Fact]
    public void Parse_RejectsWarmupThatConsumesTheCaptureWindow()
    {
        Assert.Throws<ArgumentException>(() => ClientSmokeOptions.Parse(
            ["--smoke", "--frames", "60", "--warmup-frames", "60"]));
    }

    [Theory]
    [InlineData("120", 120)]
    [InlineData("144", 144)]
    [InlineData("165", 165)]
    public void Parse_AllowsHighRefreshPerformanceTargets(string value, int expected)
    {
        var options = ClientSmokeOptions.Parse(["--smoke", "--frame-limit", value]);

        Assert.NotNull(options);
        Assert.Equal(expected, options.FrameRateLimit);
    }
}
