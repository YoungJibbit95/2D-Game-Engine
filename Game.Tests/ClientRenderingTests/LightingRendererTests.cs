using Game.Client.Rendering;
using Game.Core.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class LightingRendererTests
{
    [Fact]
    public void ComposeLightMap_ProducesTransparentLightAndBoundedPremultipliedDarkness()
    {
        var world = new World(4, 4, WorldMetadata.CreateDefault(7) with { SpawnTile = new TilePos(0, 1) });
        world.SetTileLight(0, 0, 255);
        world.SetTileLight(1, 0, 0);
        var pixels = new Color[4];

        LightingRenderer.ComposeLightMap(world, 0, 0, 2, 2, 1f, pixels);

        Assert.Equal(0, pixels[0].A);
        Assert.InRange(pixels[1].A, 230, 240);
        Assert.True(pixels[1].R <= pixels[1].A);
        Assert.True(pixels[1].G <= pixels[1].A);
        Assert.True(pixels[1].B <= pixels[1].A);
    }

    [Fact]
    public void ComposeLightMap_RespectsBlendStrengthAndDestinationContract()
    {
        var world = new World(2, 2, WorldMetadata.CreateDefault(8));
        var full = new Color[1];
        var half = new Color[1];

        LightingRenderer.ComposeLightMap(world, 0, 0, 1, 1, 1f, full);
        LightingRenderer.ComposeLightMap(world, 0, 0, 1, 1, 0.5f, half);

        Assert.True(full[0].A > half[0].A);
        Assert.Throws<ArgumentException>(() =>
            LightingRenderer.ComposeLightMap(world, 0, 0, 2, 2, 1f, Array.Empty<Color>()));
    }
}
