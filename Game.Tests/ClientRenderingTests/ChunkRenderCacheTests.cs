using Game.Client.Rendering;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class ChunkRenderCacheTests
{
    private readonly TileRegistry _tiles = TileRegistry.Create(Array.Empty<TileDefinition>());

    [Fact]
    public void TrimToLoadedChunks_RemovesOnlyChunksMissingFromWorld()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var first = world.GetOrCreateChunk(new ChunkPos(0, 0));
        var unloaded = world.GetOrCreateChunk(new ChunkPos(1, 0));
        var third = world.GetOrCreateChunk(new ChunkPos(2, 0));
        cache.GetOrBuild(world, _tiles, first);
        cache.GetOrBuild(world, _tiles, unloaded);
        cache.GetOrBuild(world, _tiles, third);
        Assert.True(world.UnloadChunk(unloaded.Position));

        var removed = cache.TrimToLoadedChunks(world.Chunks);

        Assert.Equal(1, removed);
        Assert.Equal(2, cache.CachedChunkCount);
        Assert.False(cache.GetOrBuild(world, _tiles, first).Rebuilt);
        Assert.False(cache.GetOrBuild(world, _tiles, third).Rebuilt);
        Assert.True(cache.GetOrBuild(world, _tiles, unloaded).Rebuilt);
    }

    [Fact]
    public void TrimToBudget_EvictsLeastRecentlyUsedChunk()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var oldest = world.GetOrCreateChunk(new ChunkPos(0, 0));
        var leastRecentlyUsed = world.GetOrCreateChunk(new ChunkPos(1, 0));
        var newest = world.GetOrCreateChunk(new ChunkPos(2, 0));
        cache.GetOrBuild(world, _tiles, oldest);
        cache.GetOrBuild(world, _tiles, leastRecentlyUsed);
        cache.GetOrBuild(world, _tiles, newest);
        cache.GetOrBuild(world, _tiles, oldest);

        var removed = cache.TrimToBudget(2);

        Assert.Equal(1, removed);
        Assert.Equal(2, cache.CachedChunkCount);
        Assert.False(cache.GetOrBuild(world, _tiles, oldest).Rebuilt);
        Assert.False(cache.GetOrBuild(world, _tiles, newest).Rebuilt);
        Assert.True(cache.GetOrBuild(world, _tiles, leastRecentlyUsed).Rebuilt);
    }

    [Fact]
    public void CacheHit_ReusesCommandsUntilChunkNeedsMeshRebuild()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));
        world.SetTile(0, 0, tileId: 1);

        var built = cache.GetOrBuild(world, _tiles, chunk);
        var hit = cache.GetOrBuild(world, _tiles, chunk);

        Assert.True(built.Rebuilt);
        Assert.False(hit.Rebuilt);
        Assert.Same(built.Commands, hit.Commands);
        Assert.Equal(1, cache.CachedChunkCount);

        chunk.MarkDirty(needsMeshRebuild: true, needsLightUpdate: false);
        var rebuilt = cache.GetOrBuild(world, _tiles, chunk);

        Assert.True(rebuilt.Rebuilt);
        Assert.NotSame(hit.Commands, rebuilt.Commands);
        Assert.Equal(1, cache.CachedChunkCount);
    }

    private static World CreateWorld()
    {
        return new World(96, 32, WorldMetadata.CreateDefault(seed: 1));
    }
}
