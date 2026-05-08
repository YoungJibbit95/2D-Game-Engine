using Game.Core.World;
using Game.Core.World.Structures;
using Xunit;

namespace Game.Tests.StructureTests;

public sealed class StructurePlacerTests
{
    [Fact]
    public void FromRows_MapsCharactersToTiles()
    {
        var template = StructureTemplate.FromRows(
            new[] { "W.", ".S" },
            new Dictionary<char, ushort>
            {
                ['W'] = KnownTileIds.Wood,
                ['S'] = KnownTileIds.Stone
            });

        Assert.Equal(KnownTileIds.Wood, template.GetTile(0, 0));
        Assert.Null(template.GetTile(1, 0));
        Assert.Equal(KnownTileIds.Stone, template.GetTile(1, 1));
    }

    [Fact]
    public void TryPlace_WritesTemplateTilesIntoWorld()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        var template = StructureTemplate.FromRows(
            new[] { "WW", "W." },
            new Dictionary<char, ushort> { ['W'] = KnownTileIds.Wood });

        var result = new StructurePlacer().TryPlace(world, new TilePos(4, 5), template);

        Assert.True(result.Placed);
        Assert.Equal(3, result.TilesWritten);
        Assert.Equal(KnownTileIds.Wood, world.GetTile(4, 5).TileId);
        Assert.Equal(KnownTileIds.Wood, world.GetTile(5, 5).TileId);
        Assert.Equal(KnownTileIds.Wood, world.GetTile(4, 6).TileId);
        Assert.True(world.GetTile(5, 6).IsAir);
    }

    [Fact]
    public void TryPlace_RejectsNonAirTilesInReplaceAirOnlyMode()
    {
        var world = new World(16, 16, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(4, 5, KnownTileIds.Stone);
        var template = StructureTemplate.FromRows(
            new[] { "W" },
            new Dictionary<char, ushort> { ['W'] = KnownTileIds.Wood });

        var result = new StructurePlacer().TryPlace(world, new TilePos(4, 5), template, StructurePlacementMode.ReplaceAirOnly);

        Assert.False(result.Placed);
        Assert.Equal(KnownTileIds.Stone, world.GetTile(4, 5).TileId);
    }
}
