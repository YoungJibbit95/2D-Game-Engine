using System.Collections.ObjectModel;

namespace Game.Core.World.Streaming;

public sealed record ChunkStreamingRequestSnapshot(
    long WorldSessionGeneration,
    long RequestSequence,
    RectI VisibleTileArea,
    ChunkPos CenterChunk,
    ChunkWindowSet RequiredChunks,
    ChunkWindowSet RetainedChunks,
    IReadOnlyList<ChunkPos> ChunksToLoad,
    IReadOnlyList<ChunkPos> ChunksToUnload)
{
    public ChunkStreamingPlan ToPlan()
    {
        return new ChunkStreamingPlan(
            RequiredChunks,
            new ChunkPositionListSet(ChunksToLoad),
            new ChunkPositionListSet(ChunksToUnload));
    }
}

public sealed class ChunkStreamingChunkSnapshot
{
    private const int TilePayloadBytes = sizeof(ushort) * 3 + sizeof(byte) * 2;
    private readonly TileInstance[] _tiles;
    private readonly ReadOnlyCollection<TileInstance> _readOnlyTiles;

    public ChunkStreamingChunkSnapshot(ChunkPos position, IReadOnlyList<TileInstance> tiles, ChunkMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(metadata);

        var expectedTiles = GameConstants.ChunkSize * GameConstants.ChunkSize;
        if (tiles.Count != expectedTiles)
        {
            throw new ArgumentException($"Chunk snapshot must contain exactly {expectedTiles} tiles.", nameof(tiles));
        }

        Position = position;
        Metadata = metadata;
        _tiles = tiles.ToArray();
        _readOnlyTiles = Array.AsReadOnly(_tiles);
    }

    public ChunkPos Position { get; }

    public ChunkMetadata Metadata { get; }

    public IReadOnlyList<TileInstance> Tiles => _readOnlyTiles;

    public long DecodedBytes => (long)_tiles.Length * TilePayloadBytes;

    public static ChunkStreamingChunkSnapshot Capture(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return new ChunkStreamingChunkSnapshot(chunk.Position, chunk.Tiles, chunk.Metadata);
    }

    public Chunk Materialize()
    {
        var chunk = new Chunk(Position);
        chunk.LoadTiles(_tiles);
        chunk.UpdateMetadata(Metadata);
        return chunk;
    }

    public void ApplyTo(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Position != Position)
        {
            throw new ArgumentException($"Cannot apply chunk {Position} to {chunk.Position}.", nameof(chunk));
        }

        chunk.LoadTiles(_tiles);
        chunk.UpdateMetadata(Metadata);
    }

    public bool Matches(Chunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return chunk.Position == Position && _tiles.AsSpan().SequenceEqual(chunk.Tiles);
    }
}
