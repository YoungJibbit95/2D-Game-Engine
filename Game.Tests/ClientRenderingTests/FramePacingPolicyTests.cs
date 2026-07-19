using Game.Client.Rendering.Performance;
using Game.Core.Settings;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class FramePacingPolicyTests
{
    [Fact]
    public void Resolve_LowLatencyModeUsesStableDefaultWithoutVSync()
    {
        var result = FramePacingPolicy.Resolve(new VideoSettings
        {
            VSync = true,
            LowLatencyFramePacing = true,
            FrameRateLimit = 0
        });

        Assert.False(result.SynchronizeWithVerticalRetrace);
        Assert.Equal(165, result.SoftwareFrameRateLimit);
    }

    [Fact]
    public void Resolve_LowLatencyModeHonorsExplicitFrameLimit()
    {
        var result = FramePacingPolicy.Resolve(new VideoSettings
        {
            LowLatencyFramePacing = true,
            FrameRateLimit = 120
        });

        Assert.False(result.SynchronizeWithVerticalRetrace);
        Assert.Equal(120, result.SoftwareFrameRateLimit);
    }

    [Fact]
    public void Resolve_ClassicVSyncLetsPresentationOwnCadence()
    {
        var result = FramePacingPolicy.Resolve(new VideoSettings
        {
            VSync = true,
            LowLatencyFramePacing = false,
            FrameRateLimit = 144
        });

        Assert.True(result.SynchronizeWithVerticalRetrace);
        Assert.Equal(0, result.SoftwareFrameRateLimit);
    }
}
