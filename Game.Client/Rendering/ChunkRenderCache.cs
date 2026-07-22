using Game.Core;
using Game.Core.Tiles;
using Game.Core.World;

namespace Game.Client.Rendering;

public sealed class ChunkRenderCache
{
    private readonly Dictionary<ChunkPos, CachedChunk> _chunks = new();
    private readonly Dictionary<ChunkPos, ChunkDependencyRevision> _dependencyRevisions = new();
    private readonly List<TrimCandidate> _trimCandidates = new();
    private int[][] _textureBucketByTileMask = Array.Empty<int[]>();
    private int _textureBucketCount = 1;
    private long _useTick;
    private ulong _nextDependencyRevision;

    public int CachedChunkCount => _chunks.Count;

    private readonly AutoTileSystem _autoTiles = new();

    internal void ConfigureTextureBuckets(int[][] textureBucketByTileMask, int textureBucketCount)
    {
        ArgumentNullException.ThrowIfNull(textureBucketByTileMask);
        if (textureBucketCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(textureBucketCount));
        }

        for (var tileId = 0; tileId < textureBucketByTileMask.Length; tileId++)
        {
            var buckets = textureBucketByTileMask[tileId];
            if (buckets is null)
            {
                continue;
            }

            if (buckets.Length < 16 || buckets.Length % 16 != 0)
            {
                throw new ArgumentException(
                    "Each configured tile must provide one or more complete sets of 16 auto-tile mask buckets.",
                    nameof(textureBucketByTileMask));
            }

            for (var mask = 0; mask < buckets.Length; mask++)
            {
                if ((uint)buckets[mask] >= textureBucketCount)
                {
                    throw new ArgumentException("A tile texture bucket index is outside the configured bucket count.", nameof(textureBucketByTileMask));
                }
            }
        }

        _textureBucketByTileMask = textureBucketByTileMask;
        _textureBucketCount = textureBucketCount;
        Clear();
    }

    public bool NeedsBuild(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return chunk.NeedsMeshRebuild || !_chunks.ContainsKey(chunk.Position);
    }

    public bool TryGet(ChunkPos position, out IReadOnlyList<ChunkRenderCommand> commands)
    {
        if (_chunks.TryGetValue(position, out var cached))
        {
            cached.LastUsedTick = ++_useTick;
            commands = cached.Commands;
            return true;
        }

        commands = Array.Empty<ChunkRenderCommand>();
        return false;
    }

    internal bool TryGetPrepared(ChunkPos position, out PreparedChunkRenderCommands commands)
    {
        if (_chunks.TryGetValue(position, out var cached))
        {
            cached.LastUsedTick = ++_useTick;
            commands = cached.PreparedCommands;
            return true;
        }

        commands = null!;
        return false;
    }

    public ChunkRenderCacheResult GetOrBuild(World world, TileRegistry tiles, Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(chunk);

        var dependencyStamp = ObserveDependencyStamp(world, chunk.Position);
        if (_chunks.TryGetValue(chunk.Position, out var cached) &&
            ReferenceEquals(cached.SourceChunk, chunk) &&
            !chunk.NeedsMeshRebuild &&
            cached.DependencyStamp == dependencyStamp)
        {
            cached.LastUsedTick = ++_useTick;
            return new ChunkRenderCacheResult(cached.Commands, Rebuilt: false);
        }

        var rebuilt = Build(world, tiles, chunk, dependencyStamp);
        rebuilt.LastUsedTick = ++_useTick;
        _chunks[chunk.Position] = rebuilt;
        chunk.ClearMeshRebuildFlag();
        MarkMeshClean(chunk);
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

        var removed = RemoveTrimCandidates(_trimCandidates.Count);
        foreach (var (position, _) in _dependencyRevisions)
        {
            if (!loadedChunks.ContainsKey(position))
            {
                _trimCandidates.Add(new TrimCandidate(position, 0));
            }
        }

        for (var index = 0; index < _trimCandidates.Count; index++)
        {
            _dependencyRevisions.Remove(_trimCandidates[index].Position);
        }

        _trimCandidates.Clear();
        return removed;
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
        _dependencyRevisions.Clear();
        _nextDependencyRevision = 0;
    }

    private CachedChunk Build(
        World world,
        TileRegistry tiles,
        Chunk chunk,
        ChunkDependencyStamp dependencyStamp)
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
                mask = TreeAutoTilePresentation.AddCompatibleMaterialConnections(
                    world,
                    tileX,
                    tileY,
                    tile.TileId,
                    mask);
                var visualVariant = TreeTileVisualSelector.Resolve(world, tileX, tileY, tile.TileId);
                var visualTransform = TreeTileVisualSelector.ResolveTransform(world, tileX, tileY, tile.TileId);
                commands.Add(new ChunkRenderCommand(localX, localY, tile, mask, visualVariant, visualTransform));
            }
        }

        var commandArray = commands.ToArray();
        var preparedCommands = BuildPreparedCommands(commandArray);
        return new CachedChunk(chunk, dependencyStamp, commandArray, preparedCommands);
    }

    private ChunkDependencyStamp ObserveDependencyStamp(World world, ChunkPos center)
    {
        return new ChunkDependencyStamp(
            ObserveDependencyRevision(world, new ChunkPos(center.X - 1, center.Y - 1)),
            ObserveDependencyRevision(world, new ChunkPos(center.X, center.Y - 1)),
            ObserveDependencyRevision(world, new ChunkPos(center.X + 1, center.Y - 1)),
            ObserveDependencyRevision(world, new ChunkPos(center.X - 1, center.Y)),
            ObserveDependencyRevision(world, center),
            ObserveDependencyRevision(world, new ChunkPos(center.X + 1, center.Y)),
            ObserveDependencyRevision(world, new ChunkPos(center.X - 1, center.Y + 1)),
            ObserveDependencyRevision(world, new ChunkPos(center.X, center.Y + 1)),
            ObserveDependencyRevision(world, new ChunkPos(center.X + 1, center.Y + 1)));
    }

    private ulong ObserveDependencyRevision(World world, ChunkPos position)
    {
        world.Chunks.TryGetValue(position, out var chunk);
        if (!_dependencyRevisions.TryGetValue(position, out var state))
        {
            state = new ChunkDependencyRevision();
            _dependencyRevisions.Add(position, state);
        }

        var meshDirty = chunk?.NeedsMeshRebuild == true;
        if (!ReferenceEquals(state.SourceChunk, chunk) || (meshDirty && !state.WasMeshDirty))
        {
            state.SourceChunk = chunk;
            state.Revision = ++_nextDependencyRevision;
        }

        state.WasMeshDirty = meshDirty;
        return state.Revision;
    }

    private void MarkMeshClean(Chunk chunk)
    {
        if (_dependencyRevisions.TryGetValue(chunk.Position, out var state) &&
            ReferenceEquals(state.SourceChunk, chunk))
        {
            state.WasMeshDirty = false;
        }
    }

    private PreparedChunkRenderCommands BuildPreparedCommands(ChunkRenderCommand[] commands)
    {
        var bucketCounts = new int[_textureBucketCount];
        var tileCommandCount = 0;
        var liquidCommandCount = 0;
        for (var index = 0; index < commands.Length; index++)
        {
            var command = commands[index];
            if (!command.Tile.IsAir)
            {
                bucketCounts[ResolveTextureBucket(command)]++;
                tileCommandCount++;
            }

            if (command.Tile.HasLiquid)
            {
                liquidCommandCount++;
            }
        }

        var textureBuckets = new ChunkRenderBucket[_textureBucketCount];
        var bucketWriteOffsets = new int[_textureBucketCount];
        var nextStart = 0;
        for (var bucketIndex = 0; bucketIndex < textureBuckets.Length; bucketIndex++)
        {
            var count = bucketCounts[bucketIndex];
            textureBuckets[bucketIndex] = new ChunkRenderBucket(nextStart, count);
            bucketWriteOffsets[bucketIndex] = nextStart;
            nextStart += count;
        }

        var tileCommands = new ChunkRenderCommand[tileCommandCount];
        var liquidCommands = new ChunkRenderCommand[liquidCommandCount];
        var liquidWriteIndex = 0;
        for (var index = 0; index < commands.Length; index++)
        {
            var command = commands[index];
            if (!command.Tile.IsAir)
            {
                var bucketIndex = ResolveTextureBucket(command);
                tileCommands[bucketWriteOffsets[bucketIndex]++] = command;
            }

            if (command.Tile.HasLiquid)
            {
                liquidCommands[liquidWriteIndex++] = command;
            }
        }

        return new PreparedChunkRenderCommands(tileCommands, textureBuckets, liquidCommands);
    }

    private int ResolveTextureBucket(ChunkRenderCommand command)
    {
        var tileId = command.Tile.TileId;
        if (tileId >= _textureBucketByTileMask.Length ||
            _textureBucketByTileMask[tileId] is not { } buckets)
        {
            return 0;
        }

        var variantOffset = Math.Min(command.VisualVariant, buckets.Length / 16 - 1) * 16;
        var sourceMask = TreeTileVisualSelector.ResolveSourceMask(command.AutoTileMask, command.VisualTransform);
        return buckets[variantOffset + ((int)sourceMask & 15)];
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

    private readonly record struct ChunkDependencyStamp(
        ulong TopLeft,
        ulong Top,
        ulong TopRight,
        ulong Left,
        ulong Center,
        ulong Right,
        ulong BottomLeft,
        ulong Bottom,
        ulong BottomRight);

    private sealed class ChunkDependencyRevision
    {
        public Chunk? SourceChunk { get; set; }

        public ulong Revision { get; set; }

        public bool WasMeshDirty { get; set; }
    }

    private sealed class CachedChunk
    {
        public CachedChunk(
            Chunk sourceChunk,
            ChunkDependencyStamp dependencyStamp,
            IReadOnlyList<ChunkRenderCommand> commands,
            PreparedChunkRenderCommands preparedCommands)
        {
            SourceChunk = sourceChunk;
            DependencyStamp = dependencyStamp;
            Commands = commands;
            PreparedCommands = preparedCommands;
        }

        public Chunk SourceChunk { get; }

        public ChunkDependencyStamp DependencyStamp { get; }

        public IReadOnlyList<ChunkRenderCommand> Commands { get; }

        public PreparedChunkRenderCommands PreparedCommands { get; }

        public long LastUsedTick { get; set; }
    }
}

internal readonly record struct ChunkRenderBucket(int StartIndex, int Count)
{
    public int EndIndex => StartIndex + Count;
}

internal sealed class PreparedChunkRenderCommands
{
    public PreparedChunkRenderCommands(
        ChunkRenderCommand[] tileCommands,
        ChunkRenderBucket[] textureBuckets,
        ChunkRenderCommand[] liquidCommands)
    {
        TileCommands = tileCommands;
        TextureBuckets = textureBuckets;
        LiquidCommands = liquidCommands;
    }

    public ChunkRenderCommand[] TileCommands { get; }

    public ChunkRenderBucket[] TextureBuckets { get; }

    public ChunkRenderCommand[] LiquidCommands { get; }
}
