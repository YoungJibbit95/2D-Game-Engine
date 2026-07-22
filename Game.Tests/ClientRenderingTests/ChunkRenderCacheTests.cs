using Game.Client.Rendering;
using Game.Core.Tiles;
using Game.Core.World;
using Xunit;

namespace Game.Tests.ClientRenderingTests;

public sealed class ChunkRenderCacheTests
{
    private readonly TileRegistry _tiles = TileRegistry.Create(Array.Empty<TileDefinition>());

    [Fact]
    public void TrimToLoadedChunks_RemovesMissingChunkAndInvalidatesAdjacentDependencies()
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
        Assert.True(cache.GetOrBuild(world, _tiles, first).Rebuilt);
        Assert.True(cache.GetOrBuild(world, _tiles, third).Rebuilt);
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
        Assert.False(cache.NeedsBuild(chunk));
        Assert.True(cache.TryGet(chunk.Position, out var prepared));
        Assert.Same(built.Commands, prepared);
        Assert.Equal(1, cache.CachedChunkCount);

        chunk.MarkDirty(needsMeshRebuild: true, needsLightUpdate: false);
        Assert.True(cache.NeedsBuild(chunk));
        var rebuilt = cache.GetOrBuild(world, _tiles, chunk);

        Assert.True(rebuilt.Rebuilt);
        Assert.NotSame(hit.Commands, rebuilt.Commands);
        Assert.Equal(1, cache.CachedChunkCount);
    }

    [Fact]
    public void Rebuild_PreparesTextureBucketsAndSeparateLiquidCommands()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var textureBuckets = new int[4][];
        textureBuckets[1] = CreateMaskBuckets(1);
        textureBuckets[2] = CreateMaskBuckets(2);
        cache.ConfigureTextureBuckets(textureBuckets, textureBucketCount: 3);

        world.SetTile(0, 0, tileId: 1);
        world.SetTile(1, 0, tileId: 2);
        var wetTile = TileInstance.FromTileId(1);
        wetTile.LiquidAmount = 192;
        wetTile.Flags |= TileFlags.HasLiquid;
        world.SetTile(2, 0, wetTile);
        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));

        cache.GetOrBuild(world, _tiles, chunk);

        Assert.True(cache.TryGetPrepared(chunk.Position, out var prepared));
        Assert.Equal(3, prepared.TileCommands.Length);
        Assert.Equal(3, prepared.TextureBuckets.Length);
        Assert.Equal(0, prepared.TextureBuckets[0].Count);
        Assert.Equal(2, prepared.TextureBuckets[1].Count);
        Assert.Equal(1, prepared.TextureBuckets[2].Count);
        Assert.All(
            prepared.TileCommands[prepared.TextureBuckets[1].StartIndex..prepared.TextureBuckets[1].EndIndex],
            command => Assert.Equal((ushort)1, command.Tile.TileId));
        Assert.All(
            prepared.TileCommands[prepared.TextureBuckets[2].StartIndex..prepared.TextureBuckets[2].EndIndex],
            command => Assert.Equal((ushort)2, command.Tile.TileId));
        var liquid = Assert.Single(prepared.LiquidCommands);
        Assert.Equal(2, liquid.LocalX);
        Assert.Equal(0, liquid.LocalY);
        Assert.True(liquid.Tile.HasLiquid);
    }

    [Fact]
    public void PreparedCacheHit_DoesNotAllocate()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));
        world.SetTile(0, 0, tileId: 1);
        cache.GetOrBuild(world, _tiles, chunk);
        Assert.True(cache.TryGetPrepared(chunk.Position, out _));

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = cache.TryGetPrepared(chunk.Position, out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Rebuild_UsesVisualVariantOffsetForTreeTextureBuckets()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var textureBuckets = new int[8][];
        textureBuckets[KnownTileIds.Wood] = CreateVariantBuckets(1, 2, 3);
        textureBuckets[KnownTileIds.Leaves] = CreateVariantBuckets(1, 2, 3);
        cache.ConfigureTextureBuckets(textureBuckets, textureBucketCount: 4);
        for (var y = 6; y <= 15; y++)
        {
            world.SetTile(12, y, KnownTileIds.Wood);
        }

        world.SetTile(11, 6, KnownTileIds.Leaves);
        world.SetTile(13, 6, KnownTileIds.Leaves);
        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));

        var result = cache.GetOrBuild(world, _tiles, chunk);
        var trunk = Assert.Single(result.Commands, command => command.LocalX == 12 && command.LocalY == 10);
        Assert.True(cache.TryGetPrepared(chunk.Position, out var prepared));
        var expectedBucket = trunk.VisualVariant + 1;

        Assert.Contains(
            prepared.TileCommands[prepared.TextureBuckets[expectedBucket].StartIndex..prepared.TextureBuckets[expectedBucket].EndIndex],
            command => command.LocalX == trunk.LocalX && command.LocalY == trunk.LocalY);
    }

    [Fact]
    public void Rebuild_UsesTransformedFoliageMaskForTextureBucket()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var textureBuckets = new int[KnownTileIds.MarshLeaves + 1][];
        textureBuckets[KnownTileIds.Leaves] = Enumerable.Range(1, 16).ToArray();
        cache.ConfigureTextureBuckets(textureBuckets, textureBucketCount: 17);
        var tiles = TileRegistry.Create(
        [
            new TileDefinition
            {
                NumericId = KnownTileIds.Leaves,
                Id = "leaves",
                DisplayName = "Leaves",
                TexturePath = "tiles/leaves",
                MergeGroup = "tree-canopy"
            }
        ]);
        var tileX = Enumerable.Range(2, 24).First(
            x => TreeTileVisualSelector.ResolveTransform(world, x, 10, KnownTileIds.Leaves) ==
                TileVisualTransform.FlipHorizontal);
        world.SetTile(tileX, 10, KnownTileIds.Leaves);
        world.SetTile(tileX - 1, 10, KnownTileIds.Leaves);
        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));

        _ = cache.GetOrBuild(world, tiles, chunk);
        Assert.True(cache.TryGetPrepared(chunk.Position, out var prepared));
        var command = Assert.Single(prepared.TileCommands, tile => tile.LocalX == tileX && tile.LocalY == 10);
        Assert.Equal(TileVisualTransform.FlipHorizontal, command.VisualTransform);
        Assert.Equal(AutoTileMask.Left, command.AutoTileMask);
        var sourceMask = TreeTileVisualSelector.ResolveSourceMask(command.AutoTileMask, command.VisualTransform);
        Assert.Equal(AutoTileMask.Right, sourceMask);
        var expectedBucket = (int)sourceMask + 1;

        Assert.Contains(
            prepared.TileCommands[prepared.TextureBuckets[expectedBucket].StartIndex..prepared.TextureBuckets[expectedBucket].EndIndex],
            tile => tile.LocalX == command.LocalX && tile.LocalY == command.LocalY);
    }

    [Fact]
    public void Rebuild_UsesCrossChunkTreeSocketsAndBoundaryDirtyFlags()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var tiles = TileRegistry.Create(
        [
            new TileDefinition
            {
                NumericId = KnownTileIds.OakTrunk,
                Id = "oak_trunk",
                DisplayName = "Oak Trunk",
                TexturePath = "tiles/oak_trunk",
                MergeGroup = "tree-trunk"
            },
            new TileDefinition
            {
                NumericId = KnownTileIds.OakLeaves,
                Id = "oak_leaves",
                DisplayName = "Oak Leaves",
                TexturePath = "tiles/oak_leaves",
                MergeGroup = "tree-canopy"
            }
        ]);
        world.SetTile(31, 10, KnownTileIds.OakTrunk);
        world.SetTile(32, 10, KnownTileIds.OakLeaves);
        var leftChunk = world.GetOrCreateChunk(new ChunkPos(0, 0));
        var rightChunk = world.GetOrCreateChunk(new ChunkPos(1, 0));

        var leftBuild = cache.GetOrBuild(world, tiles, leftChunk);
        var rightBuild = cache.GetOrBuild(world, tiles, rightChunk);
        var left = Assert.Single(leftBuild.Commands, command => command.LocalX == 31 && command.LocalY == 10);
        var right = Assert.Single(rightBuild.Commands, command => command.LocalX == 0 && command.LocalY == 10);

        Assert.Equal(AutoTileMask.Right, left.AutoTileMask & AutoTileMask.Right);
        Assert.Equal(AutoTileMask.Left, right.AutoTileMask & AutoTileMask.Left);
        Assert.False(leftChunk.NeedsMeshRebuild);
        Assert.False(rightChunk.NeedsMeshRebuild);

        world.SetTile(32, 10, KnownTileIds.Air);

        Assert.True(leftChunk.NeedsMeshRebuild);
        Assert.True(rightChunk.NeedsMeshRebuild);
        var rebuiltLeft = cache.GetOrBuild(world, tiles, leftChunk);
        var disconnected = Assert.Single(
            rebuiltLeft.Commands,
            command => command.LocalX == 31 && command.LocalY == 10);
        Assert.Equal(AutoTileMask.None, disconnected.AutoTileMask & AutoTileMask.Right);
        Assert.True(cache.GetOrBuild(world, tiles, rightChunk).Rebuilt);
    }

    [Fact]
    public void Rebuild_InvalidatesCrossChunkTreeAnchorDependencyAfterSourceFlagWasCleared()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        for (var y = 10; y <= 19; y++)
        {
            world.SetTile(29, y, KnownTileIds.OakTrunk);
        }

        for (var y = 8; y <= 12; y++)
        {
            for (var x = 33; x <= 36; x++)
            {
                world.SetTile(x, y, KnownTileIds.OakLeaves);
            }
        }

        var sourceChunk = world.GetOrCreateChunk(new ChunkPos(0, 0));
        var dependentChunk = world.GetOrCreateChunk(new ChunkPos(1, 0));
        _ = cache.GetOrBuild(world, _tiles, sourceChunk);
        var initial = cache.GetOrBuild(world, _tiles, dependentChunk);
        var tracked = initial.Commands.First(command =>
            command.Tile.TileId == KnownTileIds.OakLeaves &&
            command.VisualVariant != 0);

        for (var y = 10; y <= 19; y++)
        {
            world.SetTile(29, y, KnownTileIds.Air);
        }

        Assert.False(dependentChunk.NeedsMeshRebuild);
        Assert.True(cache.GetOrBuild(world, _tiles, sourceChunk).Rebuilt);
        var rebuilt = cache.GetOrBuild(world, _tiles, dependentChunk);
        var fallback = Assert.Single(
            rebuilt.Commands,
            command => command.LocalX == tracked.LocalX && command.LocalY == tracked.LocalY);

        Assert.True(rebuilt.Rebuilt);
        Assert.Equal(0, fallback.VisualVariant);
    }

    [Fact]
    public void PreparedCacheHit_WithDependencyValidation_DoesNotAllocate()
    {
        var world = CreateWorld();
        var cache = new ChunkRenderCache();
        var chunk = world.GetOrCreateChunk(new ChunkPos(0, 0));
        world.SetTile(0, 0, tileId: 1);
        _ = cache.GetOrBuild(world, _tiles, chunk);
        for (var warmup = 0; warmup < 256; warmup++)
        {
            _ = cache.GetOrBuild(world, _tiles, chunk);
        }

        var rebuilds = 0;
        var checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var result = cache.GetOrBuild(world, _tiles, chunk);
            rebuilds += result.Rebuilt ? 1 : 0;
            checksum += result.Commands.Count;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, rebuilds);
        Assert.Equal(10_000, checksum);
        Assert.Equal(0, allocated);
    }

    private static int[] CreateMaskBuckets(int bucketIndex)
    {
        var buckets = new int[16];
        Array.Fill(buckets, bucketIndex);
        return buckets;
    }

    private static int[] CreateVariantBuckets(params int[] bucketIndices)
    {
        var buckets = new int[bucketIndices.Length * 16];
        for (var variant = 0; variant < bucketIndices.Length; variant++)
        {
            Array.Fill(buckets, bucketIndices[variant], variant * 16, 16);
        }

        return buckets;
    }

    private static World CreateWorld()
    {
        return new World(96, 32, WorldMetadata.CreateDefault(seed: 1));
    }
}
