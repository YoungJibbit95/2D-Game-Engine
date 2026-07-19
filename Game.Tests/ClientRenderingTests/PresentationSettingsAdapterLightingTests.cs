using Game.Client.Rendering.Effects;
using Game.Core.Runtime;
using Game.Core.Settings;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class PresentationSettingsAdapterLightingTests
{
    [Fact]
    public void ResolveCaveResidualLight_PreservesAuthoredLuminousCaveAmbience()
    {
        var settings = new RenderingSettings { CaveAmbientLight = 0.22f };
        var crystal = default(LivingWorldFrameSnapshot) with
        {
            IsUnderground = true,
            AmbientLight = 0.42f,
            BiomeId = "crystal_depths"
        };

        var residual = PresentationSettingsAdapter.ResolveCaveResidualLight(settings, crystal);

        Assert.InRange(residual, 0.377f, 0.379f);
    }

    [Fact]
    public void ResolveCaveResidualLight_DarkCaveKeepsConfiguredPlayerFloor()
    {
        var settings = new RenderingSettings { CaveAmbientLight = 0.22f };
        var deepCave = default(LivingWorldFrameSnapshot) with
        {
            IsUnderground = true,
            AmbientLight = 0.16f,
            BiomeId = "deep_cave"
        };

        Assert.Equal(0.22f, PresentationSettingsAdapter.ResolveCaveResidualLight(settings, deepCave));
    }

    [Fact]
    public void ResolveCaveResidualLight_SurfaceUsesConfiguredFloorWithoutBiomeOverride()
    {
        var settings = new RenderingSettings { CaveAmbientLight = 0.18f };
        var surface = default(LivingWorldFrameSnapshot) with
        {
            IsUnderground = false,
            AmbientLight = 1f,
            BiomeId = "forest"
        };

        Assert.Equal(0.18f, PresentationSettingsAdapter.ResolveCaveResidualLight(settings, surface));
    }
}
