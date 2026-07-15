using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;

namespace Game.Client.Rendering;

public sealed class ChunkRenderCache
{
    private readonly Dictionary<ChunkPos, CachedChunk> _chunks = new();
    private readonly List<TrimCandidate> _trimCandidates = new();
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

    public int TrimToLoadedChunks(IReadOnlyDictionary<ChunkPos, Chunk> loadedChunks)
    {
        ArgumentNullException.ThrowIfNull(loadedChunks);

        _trimCandidates.Clear();
        foreach (var (position, cached) in _chunks)
        {
            if (loadedChunks.ContainsKey(position))
            {
                continue;
            }

            _trimCandidates.Add(new TrimCandidate(position, cached.LastUsedTick));
        }

        return RemoveTrimCandidates(_trimCandidates.Count);
    }

    public int TrimToBudget(int maxCachedChunks)
    {
        if (maxCachedChunks <= 0 || _chunks.Count <= maxCachedChunks)
        {
            return 0;
        }

        _trimCandidates.Clear();
        foreach (var (position, cached) in _chunks)
        {
            _trimCandidates.Add(new TrimCandidate(position, cached.LastUsedTick));
        }

        _trimCandidates.Sort();
        return RemoveTrimCandidates(_chunks.Count - maxCachedChunks);
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

    private int RemoveTrimCandidates(int count)
    {
        for (var index = 0; index < count; index++)
        {
            _chunks.Remove(_trimCandidates[index].Position);
        }

        _trimCandidates.Clear();
        return count;
    }

    private readonly record struct TrimCandidate(ChunkPos Position, long LastUsedTick) : IComparable<TrimCandidate>
    {
        public int CompareTo(TrimCandidate other)
        {
            return LastUsedTick.CompareTo(other.LastUsedTick);
        }
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
