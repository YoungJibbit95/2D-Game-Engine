using Game.Core.Lighting;
using Game.Core.World;
using Xunit;

namespace Game.Tests.LightingTests;

public sealed class LightingSystemTests
{
    [Fact]
    public void Recalculate_AppliesSunlightFromTop()
    {
        var world = new SimpleWorldGenerator().Generate(32, 32, seed: 22);

        new LightingSystem().Recalculate(world, Array.Empty<LightSource>());

        Assert.Equal(255, world.GetTile(0, 0).Light);
        Assert.True(world.GetTile(0, 31).Light < 255);
    }

    [Fact]
    public void Recalculate_UsesConfiguredSunlightIntensity()
    {
        var world = new World(8, 8, WorldMetadata.CreateDefault(seed: 1));

        new LightingSystem().Recalculate(world, Array.Empty<LightSource>(), sunlight: 64);

        Assert.Equal(64, world.GetTile(0, 0).Light);
    }

    [Fact]
    public void Recalculate_PropagatesPointLight()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 1));
        AddStoneRoof(world);
        var source = new LightSource(new TilePos(16, 16), 220, Radius: 5);

        new LightingSystem().Recalculate(world, new[] { source });

        Assert.True(world.GetTile(16, 16).Light >= 220);
        Assert.True(world.GetTile(18, 16).Light > world.GetTile(22, 16).Light);
    }

    [Fact]
    public void Recalculate_SolidTilesAttenuatePointLight()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 1));
        AddStoneRoof(world);
        world.SetTile(17, 16, KnownTileIds.Stone);
        var source = new LightSource(new TilePos(16, 16), 220, Radius: 5);

        new LightingSystem().Recalculate(world, new[] { source });

        Assert.True(world.GetTile(18, 16).Light < world.GetTile(16, 16).Light);
    }

    [Fact]
    public void Recalculate_LeavesUndergroundCavesDarkWithoutLightSources()
    {
        var world = new World(8, 10, WorldMetadata.CreateDefault(seed: 1));
        for (var x = 0; x < world.WidthTiles; x++)
        {
            for (var y = 0; y < 5; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }

        new LightingSystem().Recalculate(world, Array.Empty<LightSource>());

        Assert.InRange(world.GetTile(4, 7).Light, 0, 12);
    }

    [Fact]
    public void Recalculate_UsesAmbientFloorUnderground()
    {
        var world = new World(4, 8, WorldMetadata.CreateDefault(seed: 1));
        for (var y = 0; y < 8; y++)
        {
            world.SetTile(0, y, KnownTileIds.Stone);
        }

        new LightingSystem().Recalculate(world, Array.Empty<LightSource>(), options: new LightingOptions(MinimumAmbientLight: 9));

        Assert.Equal(9, world.GetTile(0, 7).Light);
    }

    private static void AddStoneRoof(World world)
    {
        for (var x = 0; x < world.WidthTiles; x++)
        {
            for (var y = 0; y < 6; y++)
            {
                world.SetTile(x, y, KnownTileIds.Stone);
            }
        }
    }
}
