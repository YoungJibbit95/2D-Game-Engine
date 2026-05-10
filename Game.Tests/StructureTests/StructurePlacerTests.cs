using Game.Core;
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
        Assert.Equal(new RectI(4, 5, 2, 2), result.ChangedBounds);
        Assert.NotEmpty(result.DirtyChunks);
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

    [Fact]
    public void TryPlace_AllowsNegativeXInHorizontallyInfiniteWorld()
    {
        var world = new World(GameConstants.ChunkSize, 16, WorldMetadata.CreateDefault(seed: 1), isHorizontallyInfinite: true);
        var template = StructureTemplate.FromRows(
            new[] { "WW" },
            new Dictionary<char, ushort> { ['W'] = KnownTileIds.Wood });

        var result = new StructurePlacer().TryPlace(world, new TilePos(-1, 5), template);

        Assert.True(result.Placed);
        Assert.Equal(2, result.TilesWritten);
        Assert.Equal(KnownTileIds.Wood, world.GetTile(-1, 5).TileId);
        Assert.Equal(KnownTileIds.Wood, world.GetTile(0, 5).TileId);
        Assert.Contains(new ChunkPos(-1, 0), result.DirtyChunks);
        Assert.Contains(new ChunkPos(0, 0), result.DirtyChunks);
    }
}
