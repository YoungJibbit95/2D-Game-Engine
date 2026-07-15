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
    [InlineData("121")]
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
}
