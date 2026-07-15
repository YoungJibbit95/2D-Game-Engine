using Game.Client.Rendering.Effects;
using Game.Client.Rendering.Lighting;
using Game.Core.Tiles;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class VisibleLightCollectorTests
{
    [Fact]
    public void CollectTileLights_ProducesWarmBoundedTorchLight()
    {
        var torch = new TileDefinition
        {
            NumericId = 11,
            Id = "torch",
            DisplayName = "Torch",
            TexturePath = "world/objects/torch",
            Solid = false,
            BlocksLight = false,
            EmittedLight = 238,
            LightRadius = 10,
            Hardness = 0.25f,
            MiningPowerRequired = 0
        };
        var tiles = TileRegistry.Create(new[] { torch });
        var world = new World(16, 8, WorldMetadata.CreateDefault(31));
        world.SetTile(4, 3, TileInstance.FromTileId(11, isSolid: false));
        var profile = PresentationQualityProfile.Create(
            PresentationQualityTier.High,
            new Rectangle(0, 0, 256, 128));
        var lights = new ScreenSpaceLight[profile.Budget.MaxPointLights];

        var telemetry = VisibleLightCollector.CollectTileLights(
            world,
            tiles,
            new Rectangle(0, 0, 256, 128),
            profile,
            lights);

        Assert.Equal(1, telemetry.LightsCollected);
        Assert.Equal(new Vector2(72f, 56f), lights[0].WorldPosition);
        Assert.True(lights[0].Color.R > lights[0].Color.B);
        Assert.Equal(160f, lights[0].RadiusPixels);
        Assert.True(lights[0].CastsShadows);
    }
}
