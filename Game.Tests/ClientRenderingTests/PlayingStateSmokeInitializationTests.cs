using Game.Client.GameStates;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class PlayingStateSmokeInitializationTests
{
    [Fact]
    public void Initialize_OpenCraftingRequestIsAppliedOnceAfterSceneSetup()
    {
        using var states = new GameStateManager();
        using var playing = new PlayingState(
            states,
            openCraftingOnInitialize: true);

        playing.Initialize();

        Assert.True(playing.InitialOverlayRequestApplied);
        Assert.True(playing.IsCraftingOverlayOpen);

        playing.Initialize();

        Assert.True(playing.IsCraftingOverlayOpen);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Constructor_RejectsCraftingWithExclusiveInitialOverlay(
        bool openConsole,
        bool openPause)
    {
        using var states = new GameStateManager();

        Assert.Throws<ArgumentException>(() => new PlayingState(
            states,
            openConsoleOnInitialize: openConsole,
            openPauseOnInitialize: openPause,
            openCraftingOnInitialize: true));
    }
}
