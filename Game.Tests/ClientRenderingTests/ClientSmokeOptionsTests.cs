using Game.Client.Diagnostics;
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
