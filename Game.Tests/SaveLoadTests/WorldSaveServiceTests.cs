using Game.Core.Saving;
using Game.Core.World;
using Xunit;

namespace Game.Tests.SaveLoadTests;

public sealed class WorldSaveServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "terraria-like-save-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsGeneratedWorldExactly()
    {
        var world = new SimpleWorldGenerator().Generate(96, 64, seed: 12345);
        world.SetTile(10, 10, KnownTileIds.Dirt);
        world.RemoveTile(20, 40);

        var service = new WorldSaveService();
        service.Save(world, _tempDirectory);

        var loaded = service.Load(_tempDirectory);

        Assert.Equal(world.WidthTiles, loaded.WidthTiles);
        Assert.Equal(world.HeightTiles, loaded.HeightTiles);
        Assert.Equal(world.Metadata.Seed, loaded.Metadata.Seed);
        Assert.Equal(world.Metadata.SpawnTile, loaded.Metadata.SpawnTile);
        AssertWorldTilesEqual(world, loaded);
        Assert.All(loaded.Chunks.Values, chunk => Assert.False(chunk.IsDirty));
    }

    [Fact]
    public void Save_DirtyChunksOnlyWritesOnlyChangedChunks()
    {
        var world = new SimpleWorldGenerator().Generate(64, 64, seed: 5);
        var service = new WorldSaveService();
        service.Save(world, _tempDirectory);

        foreach (var file in Directory.EnumerateFiles(Path.Combine(_tempDirectory, "chunks"), "*.bin"))
        {
            File.SetLastWriteTimeUtc(file, DateTime.UnixEpoch);
        }

        world.SetTile(1, 1, KnownTileIds.Dirt);
        service.Save(world, _tempDirectory, WorldSaveMode.DirtyChunksOnly);

        var rewrittenFiles = Directory
            .EnumerateFiles(Path.Combine(_tempDirectory, "chunks"), "*.bin")
            .Where(file => File.GetLastWriteTimeUtc(file) > DateTime.UnixEpoch)
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Contains("0_0.bin", rewrittenFiles);
        Assert.True(rewrittenFiles.Length < world.Chunks.Count);
    }

    [Fact]
    public void Save_UpdatesSavedChunkMetadataTick()
    {
        var world = new World(32, 32, WorldMetadata.CreateDefault(seed: 2));
        world.SetTile(1, 1, KnownTileIds.Dirt);

        new WorldSaveService().Save(world, _tempDirectory);

        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));
        Assert.True(chunk.Metadata.LastSavedTick > 0);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsHorizontallyInfiniteWorldChunks()
    {
        var world = new World(32, 96, WorldMetadata.CreateDefault(seed: 77), isHorizontallyInfinite: true);
        world.SetTile(-41, 20, KnownTileIds.Dirt);
        world.SetTile(87, 44, KnownTileIds.Stone);
        world.SetTile(-1, 63, TileInstance.Liquid(255));

        var service = new WorldSaveService();
        service.Save(world, _tempDirectory);

        var loaded = service.Load(_tempDirectory);

        Assert.True(loaded.IsHorizontallyInfinite);
        Assert.True(loaded.IsInBounds(-10_000, 10));
        Assert.Equal(KnownTileIds.Dirt, loaded.GetTile(-41, 20).TileId);
        Assert.Equal(KnownTileIds.Stone, loaded.GetTile(87, 44).TileId);
        Assert.True(loaded.GetTile(-1, 63).HasLiquid);
        Assert.All(loaded.Chunks.Values, chunk => Assert.False(chunk.IsDirty));
    }

    [Fact]
    public void SaveChunkAndTryLoadChunk_RoundTripsSingleNegativeChunk()
    {
        var source = new World(32, 96, WorldMetadata.CreateDefault(seed: 77), isHorizontallyInfinite: true);
        source.SetTile(-40, 20, KnownTileIds.Dirt);
        source.SetTile(-39, 21, TileInstance.Liquid(255));
        var position = CoordinateUtils.TileToChunk(-40, 20);
        var service = new WorldSaveService();

        var saved = service.SaveChunk(source, _tempDirectory, position);
        var target = new World(32, 96, WorldMetadata.CreateDefault(seed: 77), isHorizontallyInfinite: true);
        var loaded = service.TryLoadChunk(target, _tempDirectory, position);

        Assert.True(saved);
        Assert.True(loaded);
        Assert.Equal(KnownTileIds.Dirt, target.GetTile(-40, 20).TileId);
        Assert.True(target.GetTile(-39, 21).HasLiquid);
        Assert.False(target.Chunks[position].IsDirty);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static void AssertWorldTilesEqual(World expected, World actual)
    {
        for (var y = 0; y < expected.HeightTiles; y++)
        {
            for (var x = 0; x < expected.WidthTiles; x++)
            {
                var expectedTile = expected.GetTile(x, y);
                var actualTile = actual.GetTile(x, y);

                Assert.Equal(expectedTile.TileId, actualTile.TileId);
                Assert.Equal(expectedTile.WallId, actualTile.WallId);
                Assert.Equal(expectedTile.LiquidAmount, actualTile.LiquidAmount);
                Assert.Equal(expectedTile.Light, actualTile.Light);
                Assert.Equal(expectedTile.Flags, actualTile.Flags);
            }
        }
    }
}
