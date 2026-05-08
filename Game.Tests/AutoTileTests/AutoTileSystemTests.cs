using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.AutoTileTests;

public sealed class AutoTileSystemTests
{
    [Fact]
    public void ComputeAutoTileMask_ConnectsToSameTileNeighbors()
    {
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);
        world.SetTile(2, 1, KnownTileIds.Dirt);
        world.SetTile(3, 2, KnownTileIds.Dirt);

        var mask = new AutoTileSystem().ComputeAutoTileMask(world, CreateTiles(), 2, 2);

        Assert.True(mask.HasFlag(AutoTileMask.Top));
        Assert.True(mask.HasFlag(AutoTileMask.Right));
        Assert.False(mask.HasFlag(AutoTileMask.Bottom));
        Assert.False(mask.HasFlag(AutoTileMask.Left));
    }

    [Fact]
    public void ComputeAutoTileMask_ConnectsTilesInSameMergeGroup()
    {
        var world = CreateWorld();
        world.SetTile(2, 2, KnownTileIds.Dirt);
        world.SetTile(2, 1, KnownTileIds.Grass);

        var mask = new AutoTileSystem().ComputeAutoTileMask(world, CreateTiles(), 2, 2);

        Assert.Equal(AutoTileMask.Top, mask);
    }

    [Fact]
    public void GetSourceRectForMask_MapsMaskToAtlasColumn()
    {
        var rect = new AutoTileSystem().GetSourceRectForMask(AutoTileMask.Top | AutoTileMask.Left, GameConstants.TileSize);

        Assert.Equal(9 * GameConstants.TileSize, rect.X);
        Assert.Equal(GameConstants.TileSize, rect.Width);
    }

    private static World CreateWorld()
    {
        return new World(8, 8, WorldMetadata.CreateDefault(seed: 1));
    }

    private static TileRegistry CreateTiles()
    {
        return TileRegistry.Create(new[]
        {
            new TileDefinition
            {
                NumericId = KnownTileIds.Dirt,
                Id = "dirt",
                DisplayName = "Dirt",
                TexturePath = "tiles/dirt",
                Solid = true,
                BlocksLight = true,
                Hardness = 1,
                MiningPowerRequired = 0,
                MergeGroup = "soil"
            },
            new TileDefinition
            {
                NumericId = KnownTileIds.Grass,
                Id = "grass",
                DisplayName = "Grass",
                TexturePath = "tiles/grass",
                Solid = true,
                BlocksLight = true,
                Hardness = 1,
                MiningPowerRequired = 0,
                MergeGroup = "soil"
            }
        });
    }
}
