using Game.Core;
using Game.Core.World;

namespace Game.Client.Rendering;

public sealed class ChunkRenderCache
{
    private readonly Dictionary<ChunkPos, CachedChunk> _chunks = new();

    public int CachedChunkCount => _chunks.Count;

    public ChunkRenderCacheResult GetOrBuild(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (_chunks.TryGetValue(chunk.Position, out var cached) && !chunk.NeedsMeshRebuild)
        {
            return new ChunkRenderCacheResult(cached.Commands, Rebuilt: false);
        }

        var rebuilt = Build(chunk);
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

    public void Clear()
    {
        _chunks.Clear();
    }

    private static CachedChunk Build(Chunk chunk)
    {
        var commands = new List<ChunkRenderCommand>(GameConstants.ChunkSize * GameConstants.ChunkSize / 2);

        for (var localY = 0; localY < GameConstants.ChunkSize; localY++)
        {
            for (var localX = 0; localX < GameConstants.ChunkSize; localX++)
            {
                var tile = chunk.GetTile(localX, localY);
                if (tile.IsAir && !tile.HasLiquid)
                {
                    continue;
                }

                commands.Add(new ChunkRenderCommand(localX, localY, tile));
            }
        }

        return new CachedChunk(commands.ToArray());
    }

    private sealed record CachedChunk(IReadOnlyList<ChunkRenderCommand> Commands);
}
