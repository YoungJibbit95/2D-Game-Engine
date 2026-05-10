using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;

namespace Game.Client.Rendering;

public sealed class ChunkRenderCache
{
    private readonly Dictionary<ChunkPos, CachedChunk> _chunks = new();
    private long _useTick;

    public int CachedChunkCount => _chunks.Count;

    private readonly AutoTileSystem _autoTiles = new();

    public ChunkRenderCacheResult GetOrBuild(World world, TileRegistry tiles, Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(chunk);

        if (_chunks.TryGetValue(chunk.Position, out var cached) && !chunk.NeedsMeshRebuild)
        {
            cached.LastUsedTick = ++_useTick;
            return new ChunkRenderCacheResult(cached.Commands, Rebuilt: false);
        }

        var rebuilt = Build(world, tiles, chunk);
        rebuilt.LastUsedTick = ++_useTick;
        _chunks[chunk.Position] = rebuilt;
        chunk.ClearMeshRebuildFlag();
        return new ChunkRenderCacheResult(rebuilt.Commands, Rebuilt: true);
    }

    public int TrimToLoadedChunks(IEnumerable<ChunkPos> loadedChunks)
    {
        ArgumentNullException.ThrowIfNull(loadedChunks);

        var loaded = loadedChunks.ToHashSet();
        var removed = 0;
        foreach (var position in _chunks.Keys.ToArray())
        {
            if (loaded.Contains(position))
            {
                continue;
            }

            _chunks.Remove(position);
            removed++;
        }

        return removed;
    }

    public int TrimToBudget(int maxCachedChunks)
    {
        if (maxCachedChunks <= 0 || _chunks.Count <= maxCachedChunks)
        {
            return 0;
        }

        var removed = 0;
        foreach (var position in _chunks
                     .OrderBy(pair => pair.Value.LastUsedTick)
                     .Select(pair => pair.Key)
                     .Take(_chunks.Count - maxCachedChunks)
                     .ToArray())
        {
            _chunks.Remove(position);
            removed++;
        }

        return removed;
    }

    public void Clear()
    {
        _chunks.Clear();
    }

    private CachedChunk Build(World world, TileRegistry tiles, Chunk chunk)
    {
        var commands = new List<ChunkRenderCommand>(GameConstants.ChunkSize * GameConstants.ChunkSize / 2);
        var chunkBounds = CoordinateUtils.ChunkTileBounds(chunk.Position);

        for (var localY = 0; localY < GameConstants.ChunkSize; localY++)
        {
            for (var localX = 0; localX < GameConstants.ChunkSize; localX++)
            {
                var tile = chunk.GetTile(localX, localY);
                if (tile.IsAir && !tile.HasLiquid)
                {
                    continue;
                }

                var tileX = chunkBounds.Left + localX;
                var tileY = chunkBounds.Top + localY;
                var mask = tile.IsAir
                    ? AutoTileMask.None
                    : _autoTiles.ComputeAutoTileMask(world, tiles, tileX, tileY);
                commands.Add(new ChunkRenderCommand(localX, localY, tile, mask));
            }
        }

        return new CachedChunk(commands.ToArray());
    }

    private sealed class CachedChunk
    {
        public CachedChunk(IReadOnlyList<ChunkRenderCommand> commands)
        {
            Commands = commands;
        }

        public IReadOnlyList<ChunkRenderCommand> Commands { get; }

        public long LastUsedTick { get; set; }
    }
}
