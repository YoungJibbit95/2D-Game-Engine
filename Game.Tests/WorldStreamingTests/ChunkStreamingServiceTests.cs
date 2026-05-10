using Game.Core;
using Game.Core.Saving;
using Game.Core.World;
using Game.Core.World.Generation;
using Game.Core.World.Streaming;
using Xunit;

namespace Game.Tests.WorldStreamingTests;

public sealed class ChunkStreamingServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "terraria-like-streaming-tests", Guid.NewGuid().ToString("N"));
    private readonly InfiniteWorldChunkGenerator _generator = new();
    private readonly WorldGenerationProfile _profile = WorldGenerationProfile.Small;

    [Fact]
    public void Update_LoadsSavedChunkBeforeGenerating()
    {
        var position = new ChunkPos(-2, 1);
        var tile = new TilePos(position.X * GameConstants.ChunkSize, position.Y * GameConstants.ChunkSize + 1);
        var source = _generator.CreateWorld(_profile, seed: 42);
        _generator.EnsureChunk(source, _profile, position);
        source.SetTile(tile.X, tile.Y, KnownTileIds.CopperOre);
        new WorldSaveService(WorldChunkStorageMode.RegionFiles).SaveChunk(source, _tempDirectory, position);

        var target = _generator.CreateWorld(_profile, seed: 42);
        var result = new ChunkStreamingService().Update(
            target,
            _profile,
            CoordinateUtils.ChunkTileBounds(position),
            _tempDirectory,
            NoMarginOptions());

        Assert.Equal(1, result.LoadedChunks);
        Assert.Equal(0, result.GeneratedChunks);
        Assert.Equal(KnownTileIds.CopperOre, target.GetTile(tile.X, tile.Y).TileId);
        Assert.False(target.Chunks[position].IsDirty);
    }

    [Fact]
    public void Update_GeneratesMissingRequiredChunks()
    {
        var position = new ChunkPos(3, 1);
        var world = _generator.CreateWorld(_profile, seed: 42);

        var result = new ChunkStreamingService().Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(position),
            _tempDirectory,
            NoMarginOptions());

        Assert.Equal(0, result.LoadedChunks);
        Assert.Equal(1, result.GeneratedChunks);
        Assert.True(world.TryGetChunk(position, out _));
    }

    [Fact]
    public void Update_SavesDirtyChunksBeforeUnloading()
    {
        var dirtyChunk = new ChunkPos(4, 1);
        var dirtyTile = new TilePos(dirtyChunk.X * GameConstants.ChunkSize, dirtyChunk.Y * GameConstants.ChunkSize + 3);
        var visibleChunk = new ChunkPos(0, 0);
        var world = _generator.CreateWorld(_profile, seed: 99);
        _generator.EnsureChunk(world, _profile, dirtyChunk);
        world.SetTile(dirtyTile.X, dirtyTile.Y, KnownTileIds.IronOre);

        var result = new ChunkStreamingService().Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(visibleChunk),
            _tempDirectory,
            NoMarginOptions() with { KeepDirtyChunksLoaded = false });

        Assert.Equal(1, result.SavedChunksBeforeUnload);
        Assert.Equal(1, result.UnloadedChunks);
        Assert.False(world.TryGetChunk(dirtyChunk, out _));

        var loaded = _generator.CreateWorld(_profile, seed: 99);
        Assert.True(new WorldSaveService().TryLoadChunk(loaded, _tempDirectory, dirtyChunk));
        Assert.Equal(KnownTileIds.IronOre, loaded.GetTile(dirtyTile.X, dirtyTile.Y).TileId);
    }

    [Fact]
    public void Update_SkipsDirtyUnloadWhenNoSaveDirectoryIsAvailable()
    {
        var dirtyChunk = new ChunkPos(4, 1);
        var visibleChunk = new ChunkPos(0, 0);
        var world = _generator.CreateWorld(_profile, seed: 99);
        _generator.EnsureChunk(world, _profile, dirtyChunk);
        world.SetTile(dirtyChunk.X * GameConstants.ChunkSize, dirtyChunk.Y * GameConstants.ChunkSize + 3, KnownTileIds.IronOre);

        var result = new ChunkStreamingService().Update(
            world,
            _profile,
            CoordinateUtils.ChunkTileBounds(visibleChunk),
            worldDirectory: null,
            NoMarginOptions() with { KeepDirtyChunksLoaded = false });

        Assert.Equal(1, result.SkippedDirtyUnloads);
        Assert.True(world.TryGetChunk(dirtyChunk, out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static ChunkStreamingOptions NoMarginOptions()
    {
        return new ChunkStreamingOptions
        {
            LoadMarginChunks = 0,
            UnloadMarginChunks = 0,
            KeepDirtyChunksLoaded = true
        };
    }
}
