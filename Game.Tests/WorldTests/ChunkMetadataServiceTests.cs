using Game.Core.Inventory;
using Game.Core.Items;
using Game.Core.World;
using Game.Core.World.TileEntities;
using Xunit;

namespace Game.Tests.WorldTests;

public sealed class ChunkMetadataServiceTests
{
    [Fact]
    public void RefreshChunk_CountsLiquidLightAndTileEntities()
    {
        var world = new World(64, 64, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 1, TileInstance.Liquid(128));
        world.SetTileLight(2, 2, 44);
        var tileEntities = new TileEntityManager();
        tileEntities.Add(new ChestTileEntity(new TilePos(3, 3), CreateItems()));

        var refreshed = new ChunkMetadataService().RefreshChunk(world, new ChunkPos(0, 0), tileEntities);

        Assert.True(refreshed);
        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));
        Assert.Equal(1, chunk.Metadata.ActiveLiquidTiles);
        Assert.Equal(1, chunk.Metadata.ActiveLightTiles);
        Assert.Equal(1, chunk.Metadata.TileEntityCount);
        Assert.True(chunk.Metadata.HasActiveSimulation);
    }

    [Fact]
    public void RefreshRegions_RefreshesOnlyTouchedLoadedChunks()
    {
        var world = new World(96, 64, WorldMetadata.CreateDefault(seed: 1));
        world.SetTile(1, 1, TileInstance.Liquid(128));
        world.SetTile(40, 1, TileInstance.Liquid(128));
        var service = new ChunkMetadataService();

        var refreshed = service.RefreshRegions(world, new[] { new RectI(0, 0, 4, 4) });

        Assert.Equal(1, refreshed);
        Assert.Equal(1, world.GetOrCreateChunk(new ChunkPos(0, 0)).Metadata.ActiveLiquidTiles);
        Assert.Equal(0, world.GetOrCreateChunk(new ChunkPos(1, 0)).Metadata.ActiveLiquidTiles);
    }

    [Fact]
    public void RefreshChunk_CountsNegativeChunksInHorizontallyInfiniteWorld()
    {
        var world = new World(32, 64, WorldMetadata.CreateDefault(seed: 1), isHorizontallyInfinite: true);
        world.SetTile(-2, 10, TileInstance.Liquid(128));
        world.SetTileLight(-3, 10, 44);
        var position = CoordinateUtils.TileToChunk(-2, 10);

        var refreshed = new ChunkMetadataService().RefreshChunk(world, position);

        Assert.True(refreshed);
        Assert.Equal(1, world.Chunks[position].Metadata.ActiveLiquidTiles);
        Assert.Equal(1, world.Chunks[position].Metadata.ActiveLightTiles);
    }

    [Fact]
    public void MarkSaved_UpdatesLastSavedTick()
    {
        var chunk = new Chunk(new ChunkPos(0, 0));

        new ChunkMetadataService().MarkSaved(chunk, 42);

        Assert.Equal(42, chunk.Metadata.LastSavedTick);
    }

    private static ItemRegistry CreateItems()
    {
        return ItemRegistry.Create(new[]
        {
            new ItemDefinition
            {
                Id = "gel",
                DisplayName = "Gel",
                Type = ItemType.Material,
                TexturePath = "items/gel",
                MaxStack = 999
            }
        });
    }
}
